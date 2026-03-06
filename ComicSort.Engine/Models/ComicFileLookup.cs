namespace ComicSort.Engine.Models;

public sealed class ComicFileLookup
{
    public string NormalizedPath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public string Fingerprint { get; init; } = string.Empty;

    public bool HasThumbnail { get; init; }

    public string? ThumbnailPath { get; init; }

    public bool HasComicInfo { get; init; }
}
