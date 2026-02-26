namespace ComicSort.Engine.Models;

public sealed class ComicLibraryProjection
{
    public string FilePath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string FileDirectory { get; init; } = string.Empty;

    public string DisplayTitle { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public string? Series { get; init; }

    public string? Publisher { get; init; }

    public string? ThumbnailPath { get; init; }

    public bool HasThumbnail { get; init; }

    public long SizeBytes { get; init; }

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset ModifiedUtc { get; init; }

    public DateTimeOffset LastScannedUtc { get; init; }
}
