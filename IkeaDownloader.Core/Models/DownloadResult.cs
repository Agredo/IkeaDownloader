namespace IkeaDownloader.Core.Models;

public sealed class DownloadResult
{
    public bool IsSuccess { get; private init; }
    public string? FilePath { get; private init; }
    public string? FileName { get; private init; }
    public long FileSizeBytes { get; private init; }
    public string? ErrorMessage { get; private init; }

    public string FileSizeFormatted => FileSizeBytes switch
    {
        >= 1_048_576 => $"{FileSizeBytes / 1_048_576.0:F1} MB",
        >= 1_024     => $"{FileSizeBytes / 1_024.0:F1} KB",
        _            => $"{FileSizeBytes} B"
    };

    private DownloadResult() { }

    public static DownloadResult Ok(string filePath, string fileName, long fileSizeBytes) => new()
    {
        IsSuccess      = true,
        FilePath       = filePath,
        FileName       = fileName,
        FileSizeBytes  = fileSizeBytes
    };

    public static DownloadResult Fail(string errorMessage) => new()
    {
        IsSuccess    = false,
        ErrorMessage = errorMessage
    };
}
