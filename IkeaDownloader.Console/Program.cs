using IkeaDownloader.Core;

// ── Banner ────────────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════╗");
Console.WriteLine("║       IKEA 3D Model Downloader       ║");
Console.WriteLine("╚══════════════════════════════════════╝");
Console.WriteLine();

// ── Argument / interactive input ─────────────────────────────────────────
string url;

if (args.Length >= 1)
{
    url = args[0].Trim();
}
else
{
    Console.Write("IKEA product URL: ");
    url = Console.ReadLine()?.Trim() ?? string.Empty;
}

if (string.IsNullOrWhiteSpace(url))
{
    WriteError("No URL provided.");
    return 1;
}

if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
{
    WriteError("Invalid URL. Please provide a full IKEA product URL starting with https://");
    return 1;
}

// Optional: custom output directory as second argument
string? outputDir = args.Length >= 2 ? args[1].Trim() : null;

// ── Download ──────────────────────────────────────────────────────────────
Console.WriteLine($"  URL : {url}");
if (outputDir is not null)
    Console.WriteLine($"  Dir : {outputDir}");
Console.WriteLine();

using var cts        = new CancellationTokenSource();
using var downloader = new IkeaModelDownloader();

// Allow Ctrl+C to cancel gracefully
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nCancelling…");
    cts.Cancel();
};

try
{
    Console.Write("  Fetching product page… ");
    var result = await downloader.DownloadModelAsync(url, outputDir, cts.Token);

    if (!result.IsSuccess)
    {
        Console.WriteLine();
        WriteError(result.ErrorMessage ?? "Unknown error.");
        return 2;
    }

    Console.WriteLine("done");
    Console.WriteLine();
    Console.WriteLine($"  ✔  {result.FileName}");
    Console.WriteLine($"     Size : {result.FileSizeFormatted}");
    Console.WriteLine($"     Path : {result.FilePath}");
    Console.WriteLine();
    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine();
    WriteError("Operation cancelled by user.");
    return 3;
}
catch (Exception ex)
{
    Console.WriteLine();
    WriteError($"Unexpected error: {ex.Message}");
    return 4;
}

// ── Helpers ───────────────────────────────────────────────────────────────
static void WriteError(string message)
{
    var prev = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"  ✘  {message}");
    Console.ForegroundColor = prev;
}
