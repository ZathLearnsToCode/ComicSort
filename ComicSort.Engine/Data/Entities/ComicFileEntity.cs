namespace ComicSort.Engine.Data.Entities;

public sealed class ComicFileEntity
{
    public long Id { get; set; }

    public string NormalizedPath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset ModifiedUtc { get; set; }

    public DateTimeOffset LastScannedUtc { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public string? ThumbnailKey { get; set; }

    public string? ThumbnailPath { get; set; }

    public bool HasThumbnail { get; set; }

    public ScanState ScanState { get; set; }

    public string? LastError { get; set; }

    public ComicInfoEntity? ComicInfo { get; set; }

    public List<ComicPageEntity> Pages { get; set; } = [];
}
