using AngleSharp;
using AngleSharp.Dom;
using System.Net;
using System.Text.RegularExpressions;
using IkeaDownloader.Core.Models;

namespace IkeaDownloader.Core;

/// <summary>
/// Downloads IKEA product 3D models (.glb) from product page URLs.
/// AOT-compatible — AngleSharp for DOM queries, source-generated Regex for script-embedded URLs.
/// </summary>
public sealed partial class IkeaModelDownloader : IDisposable
{
    private readonly HttpClient _http;
    private readonly IBrowsingContext _browsingContext = BrowsingContext.New(Configuration.Default);
    private bool _disposed;

    public IkeaModelDownloader()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip
                                   | DecompressionMethods.Deflate
                                   | DecompressionMethods.Brotli,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        };

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };

        // Mimic a real browser so IKEA doesn't block the request
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/124.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "Accept-Language", "en-US,en;q=0.5");
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "Accept-Encoding", "gzip, deflate, br");
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fetches the IKEA product page and returns the GLB model URL, or null
    /// if no 3D model is available on that page.
    /// </summary>
    public async Task<string?> FindGlbUrlAsync(
        string productPageUrl,
        CancellationToken ct = default)
    {
        var html = await FetchPageAsync(productPageUrl, ct).ConfigureAwait(false);
        if (html is null) return null;

        using var document = await ParseHtmlAsync(html, ct).ConfigureAwait(false);
        return ExtractGlbUrl(document, html);
    }

    /// <summary>
    /// Downloads the 3D model from an IKEA product page URL and saves it to
    /// <paramref name="outputDirectory"/> (defaults to the user's Downloads folder).
    /// </summary>
    public async Task<DownloadResult> DownloadModelAsync(
        string productPageUrl,
        string? outputDirectory = null,
        CancellationToken ct = default)
    {
        outputDirectory ??= GetDownloadsFolder();

        // ── 1. Fetch product page ──────────────────────────────────────────
        var html = await FetchPageAsync(productPageUrl, ct).ConfigureAwait(false);
        if (html is null)
            return DownloadResult.Fail(
                "Could not retrieve the product page. Check the URL and your internet connection.");

        // ── 2. Parse HTML (reused for both GLB extraction and file naming) ─
        using var document = await ParseHtmlAsync(html, ct).ConfigureAwait(false);

        // ── 3. Locate GLB URL ──────────────────────────────────────────────
        var glbUrl = ExtractGlbUrl(document, html);
        if (glbUrl is null)
            return DownloadResult.Fail(
                "No 3D model found on this page. " +
                "Make sure the product page has a 'View in 3D' button.");

        // ── 4. Download binary ─────────────────────────────────────────────
        byte[] modelBytes;
        try
        {
            modelBytes = await _http.GetByteArrayAsync(glbUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return DownloadResult.Fail($"Failed to download model file: {ex.Message}");
        }

        // ── 5. Build file name & save ──────────────────────────────────────
        Directory.CreateDirectory(outputDirectory);
        var fileName   = BuildFileName(document, glbUrl);
        var outputPath = Path.Combine(outputDirectory, fileName);

        await File.WriteAllBytesAsync(outputPath, modelBytes, ct).ConfigureAwait(false);

        return DownloadResult.Ok(outputPath, fileName, modelBytes.Length);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private async Task<string?> FetchPageAsync(string url, CancellationToken ct)
    {
        try
        {
            return await _http.GetStringAsync(url, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    private Task<IDocument> ParseHtmlAsync(string html, CancellationToken ct) =>
        _browsingContext.OpenAsync(req => req.Content(html), ct);

    /// <summary>
    /// Returns the first GLB URL found, working through strategies from most to least specific.
    /// To adapt to IKEA page changes, add, remove, or reorder entries in <see cref="CandidateGlbUrls"/>.
    /// </summary>
    private static string? ExtractGlbUrl(IDocument document, string rawHtml) =>
        CandidateGlbUrls(document, rawHtml)
            .Select(Unescape)
            .FirstOrDefault();

    /// <summary>
    /// Yields GLB URL candidates in priority order.
    /// Each strategy targets a different way IKEA may embed the model URL.
    /// </summary>
    private static IEnumerable<string> CandidateGlbUrls(IDocument document, string rawHtml)
    {
        // 1) <model-viewer src="..."> — most reliable, direct DOM attribute
        if (document.QuerySelector("model-viewer[src]")?.GetAttribute("src") is { } domSrc && IsGlbUrl(domSrc))
            yield return domSrc;

        // 2) JSON property embedded in a <script> block:  "src":"https://…glb_draco…"
        if (JsonGlbSrcRegex().Match(rawHtml) is { Success: true } m2)
            yield return m2.Groups[1].Value;

        // 3) Any quoted URL ending in .glb (with optional query string)
        if (QuotedGlbUrlRegex().Match(rawHtml) is { Success: true } m3)
            yield return m3.Groups[1].Value;

        // 4) Any quoted URL containing the glb_draco CDN segment
        if (QuotedDracoUrlRegex().Match(rawHtml) is { Success: true } m4)
            yield return m4.Groups[1].Value;
    }

    private static bool IsGlbUrl(string url) =>
        url.Contains(".glb",       StringComparison.OrdinalIgnoreCase) ||
        url.Contains("glb_draco", StringComparison.OrdinalIgnoreCase);

    /// <summary>Decode common JSON/JS escape sequences in URLs.</summary>
    private static string Unescape(string url) =>
        url.Replace("\\u002F", "/", StringComparison.OrdinalIgnoreCase)
           .Replace("\\/", "/");

    private static string BuildFileName(IDocument document, string glbUrl)
    {
        var (productName, colour) = ParseProductTitle(document);

        var productId = ProductIdRegex().Match(glbUrl) is { Success: true } m
            ? m.Groups[1].Value
            : string.Empty;

        var nameParts = new[] { productName, colour, productId is { Length: > 0 } ? $"({productId})" : null }
            .Where(part => !string.IsNullOrEmpty(part));

        return SanitizeFileName(string.Join(" - ", nameParts)) + ".glb";
    }

    /// <summary>
    /// Parses the page title into product name and colour.
    /// Expected formats: "BILLY, white - IKEA"  or  "KALLAX - IKEA"
    /// </summary>
    private static (string productName, string colour) ParseProductTitle(IDocument document)
    {
        var withoutBrand = (document.Title ?? string.Empty)
            .Trim()
            .Split(" - IKEA", 2, StringSplitOptions.None)[0];

        var nameParts = withoutBrand.Split(',', 2, StringSplitOptions.TrimEntries);

        return (
            productName: nameParts.ElementAtOrDefault(0) is { Length: > 0 } n ? n : "ikea_product",
            colour:      nameParts.ElementAtOrDefault(1) ?? string.Empty
        );
    }

    private static string SanitizeFileName(string name) =>
        Path.GetInvalidFileNameChars()
            .Aggregate(name, (current, ch) => current.Replace(ch, '_'));

    private static string GetDownloadsFolder()
    {
        // Works on Windows, macOS and most Linux desktops
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profile, "Downloads");
    }

    // -----------------------------------------------------------------------
    // Source-generated Regex  (AOT-safe, zero reflection)
    // -----------------------------------------------------------------------

    [GeneratedRegex(
        @"""src""\s*:\s*""(https?://[^""]*(?:\.glb|glb_draco)[^""]*)""",
        RegexOptions.IgnoreCase)]
    private static partial Regex JsonGlbSrcRegex();

    [GeneratedRegex(
        @"[""'](https?://[^""']*\.glb(?:\?[^""']*)?)[""']",
        RegexOptions.IgnoreCase)]
    private static partial Regex QuotedGlbUrlRegex();

    [GeneratedRegex(
        @"[""'](https?://[^""']*glb_draco[^""']*)[""']",
        RegexOptions.IgnoreCase)]
    private static partial Regex QuotedDracoUrlRegex();

    [GeneratedRegex(@"/(\d{8,})[_/.]")]
    private static partial Regex ProductIdRegex();

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
        _browsingContext.Dispose();
    }
}
