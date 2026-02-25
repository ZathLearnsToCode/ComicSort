namespace ComicSort.Engine.Data.Entities;

public sealed class ComicPageEntity
{
    public long Id { get; set; }

    public long ComicFileId { get; set; }

    public int ImageIndex { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    public string? PageType { get; set; }

    public ComicFileEntity ComicFile { get; set; } = null!;
}
