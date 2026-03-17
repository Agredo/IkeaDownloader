using System.Net;
using System.Text.RegularExpressions;
using IkeaDownloader.Core.Models;

namespace IkeaDownloader.Core;

/// <summary>
/// Downloads IKEA product 3D models (.glb) from product page URLs.
/// Fully AOT-compatible — uses source-generated Regex, no reflection.
/// </summary>
public sealed partial class IkeaModelDownloader : IDisposable
{
    private readonly HttpClient _http;
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
        return html is null ? null : ExtractGlbUrl(html);
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

        // ── 2. Locate GLB URL ──────────────────────────────────────────────
        var glbUrl = ExtractGlbUrl(html);
        if (glbUrl is null)
            return DownloadResult.Fail(
                "No 3D model found on this page. " +
                "Make sure the product page has a 'View in 3D' button.");

        // ── 3. Download binary ─────────────────────────────────────────────
        byte[] modelBytes;
        try
        {
            modelBytes = await _http.GetByteArrayAsync(glbUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return DownloadResult.Fail($"Failed to download model file: {ex.Message}");
        }

        // ── 4. Build file name & save ──────────────────────────────────────
        Directory.CreateDirectory(outputDirectory);
        var fileName   = BuildFileName(html, glbUrl);
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

    /// <summary>
    /// Tries multiple patterns to extract a GLB URL from raw HTML.
    /// Patterns are ordered from most specific to most generic.
    /// </summary>
    private static string? ExtractGlbUrl(string html)
    {
        // 1) <model-viewer src="..."> — most reliable
        var m = ModelViewerSrcRegex().Match(html);
        if (m.Success) return Unescape(m.Groups[1].Value);

        // 2) JSON property:  "src":"https://…glb_draco…"
        m = JsonGlbSrcRegex().Match(html);
        if (m.Success) return Unescape(m.Groups[1].Value);

        // 3) Any quoted URL ending in .glb (with optional query string)
        m = QuotedGlbUrlRegex().Match(html);
        if (m.Success) return Unescape(m.Groups[1].Value);

        // 4) Any quoted URL that contains glb_draco segment
        m = QuotedDracoUrlRegex().Match(html);
        if (m.Success) return Unescape(m.Groups[1].Value);

        return null;
    }

    /// <summary>Decode common JSON/JS escape sequences in URLs.</summary>
    private static string Unescape(string url) =>
        url.Replace("\\u002F", "/", StringComparison.OrdinalIgnoreCase)
           .Replace("\\/", "/");

    private static string BuildFileName(string html, string glbUrl)
    {
        // Product name + colour come from <title>Name, Colour - IKEA</title>
        var titleMatch = PageTitleRegex().Match(html);
        var productName = "ikea_product";
        var colour      = string.Empty;

        if (titleMatch.Success)
        {
            // Title format:  "BILLY, white - IKEA"  or  "KALLAX - IKEA"
            var raw   = titleMatch.Groups[1].Value.Trim();
            var parts = raw.Split(" - IKEA", StringSplitOptions.RemoveEmptyEntries);
            var nameParts = (parts.Length > 0 ? parts[0] : raw)
                            .Split(',', 2, StringSplitOptions.TrimEntries);

            productName = nameParts[0];
            if (nameParts.Length > 1) colour = nameParts[1];
        }

        // Product article number extracted from GLB URL (e.g. /12345678_)
        var idMatch   = ProductIdRegex().Match(glbUrl);
        var productId = idMatch.Success ? idMatch.Groups[1].Value : string.Empty;

        var name = productName;
        if (!string.IsNullOrEmpty(colour))    name += " - " + colour;
        if (!string.IsNullOrEmpty(productId)) name += " (" + productId + ")";

        // Sanitize
        foreach (var ch in Path.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');

        return name + ".glb";
    }

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
        @"<model-viewer[^>]+\bsrc=""(https?://[^""]+(?:\.glb|glb_draco)[^""]*)""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ModelViewerSrcRegex();

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

    [GeneratedRegex(@"<title>([^<]+)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex PageTitleRegex();

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
    }
}
