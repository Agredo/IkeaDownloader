using IkeaDownloader.Core;

// ── Banner ────────────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════╗");
Console.WriteLine("║       IKEA 3D Model Downloader       ║");
Console.WriteLine("╚══════════════════════════════════════╝");
Console.WriteLine();

// Optional: custom output directory as second argument
string? outputDir = args.Length >= 2 ? args[1].Trim() : null;

// ── Output directory (ask once on first start) ────────────────────────────
if (outputDir is null)
{
    string defaultOutputDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    Console.WriteLine($"  Save location (empty = {defaultOutputDir}):");
    Console.Write("  > ");
    var dirInput = Console.ReadLine()?.Trim() ?? string.Empty;
    outputDir = string.IsNullOrWhiteSpace(dirInput) ? defaultOutputDir : dirInput;
    Console.WriteLine();
}

using var cts        = new CancellationTokenSource();
using var downloader = new IkeaModelDownloader();

// Allow Ctrl+C to cancel gracefully
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nCancelling…");
    cts.Cancel();
};

bool firstRun = true;

while (!cts.IsCancellationRequested)
{
    // ── URL input ──────────────────────────────────────────────────────
    string url;

    if (firstRun && args.Length >= 1)
    {
        url = args[0].Trim();
    }
    else
    {
        Console.Write("  IKEA product URL (empty to exit): ");
        url = Console.ReadLine()?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(url))
            break;
    }

    firstRun = false;

    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        WriteError("Invalid URL. Please provide a full IKEA product URL starting with https://");
        Console.WriteLine();
        continue;
    }

    // ── Download ───────────────────────────────────────────────────────
    Console.WriteLine($"  URL : {url}");
    Console.WriteLine($"  Dir : {outputDir}");
    Console.WriteLine();

    try
    {
        Console.Write("  Fetching product page… ");
        var result = await downloader.DownloadModelAsync(url, outputDir, cts.Token);

        if (!result.IsSuccess)
        {
            Console.WriteLine();
            WriteError(result.ErrorMessage ?? "Unknown error.");
        }
        else
        {
            Console.WriteLine("done");
            Console.WriteLine();
            Console.WriteLine($"  ✔  {result.FileName}");
            Console.WriteLine($"     Size : {result.FileSizeFormatted}");
            Console.WriteLine($"     Path : {result.FilePath}");
        }
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
    }

    Console.WriteLine();
}

return 0;

// ── Helpers ───────────────────────────────────────────────────────────────
static void WriteError(string message)
{
    var prev = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"  ✘  {message}");
    Console.ForegroundColor = prev;
}
