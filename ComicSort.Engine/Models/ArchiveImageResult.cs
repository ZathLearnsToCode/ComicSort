namespace ComicSort.Engine.Models;

public sealed class ArchiveImageResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public byte[]? ImageBytes { get; init; }
}
