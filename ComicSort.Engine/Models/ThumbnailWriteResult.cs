namespace ComicSort.Engine.Models;

public sealed class ThumbnailWriteResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public string? ThumbnailPath { get; init; }
}
