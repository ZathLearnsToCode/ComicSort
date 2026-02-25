namespace ComicSort.Engine.Models;

public sealed class ComicLibraryItem
{
    public string FilePath { get; init; } = string.Empty;

    public string FileDirectory { get; init; } = string.Empty;

    public string DisplayTitle { get; init; } = string.Empty;

    public string? Series { get; init; }

    public string? Publisher { get; init; }

    public string? ThumbnailPath { get; init; }

    public bool IsThumbnailReady { get; init; }

    public string FileTypeTag { get; init; } = string.Empty;

    public DateTimeOffset LastScannedUtc { get; init; }
}
