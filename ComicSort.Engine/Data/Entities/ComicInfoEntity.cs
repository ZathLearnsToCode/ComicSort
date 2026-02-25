namespace ComicSort.Engine.Data.Entities;

public sealed class ComicInfoEntity
{
    public long Id { get; set; }

    public long ComicFileId { get; set; }

    public string? Series { get; set; }

    public string? Title { get; set; }

    public string? Summary { get; set; }

    public string? Writer { get; set; }

    public string? Penciller { get; set; }

    public string? Inker { get; set; }

    public string? Colorist { get; set; }

    public string? Publisher { get; set; }

    public int? PageCount { get; set; }

    public ComicFileEntity ComicFile { get; set; } = null!;
}
