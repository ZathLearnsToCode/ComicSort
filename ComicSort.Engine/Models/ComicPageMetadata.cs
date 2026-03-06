namespace ComicSort.Engine.Models;

public sealed class ComicPageMetadata
{
    public int ImageIndex { get; init; }

    public int? ImageWidth { get; init; }

    public int? ImageHeight { get; init; }

    public string? PageType { get; init; }
}
