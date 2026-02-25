using ComicSort.Engine.Data;

namespace ComicSort.Engine.Models;

public sealed class ComicFileUpsertModel
{
    public string NormalizedPath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset ModifiedUtc { get; init; }

    public DateTimeOffset LastScannedUtc { get; init; }

    public string Fingerprint { get; init; } = string.Empty;

    public string? ThumbnailKey { get; init; }

    public string? ThumbnailPath { get; init; }

    public bool HasThumbnail { get; init; }

    public ScanState ScanState { get; init; }

    public string? LastError { get; init; }
}
