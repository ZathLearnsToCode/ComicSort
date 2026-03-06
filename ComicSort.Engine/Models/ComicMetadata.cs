namespace ComicSort.Engine.Models;

public enum ComicMetadataSource
{
    ComicInfoXml = 0,
    FileNameFallback = 1
}

public sealed class ComicMetadata
{
    public string FilePath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string DisplayTitle { get; init; } = string.Empty;

    public string? Series { get; init; }

    public string? Title { get; init; }

    public string? IssueNumber { get; init; }

    public int? Volume { get; init; }

    public int? Year { get; init; }

    public string? Publisher { get; init; }

    public string? Writer { get; init; }

    public string? Penciller { get; init; }

    public string? Inker { get; init; }

    public string? Colorist { get; init; }

    public string? Summary { get; init; }

    public int? PageCount { get; init; }

    public IReadOnlyList<ComicPageMetadata> Pages { get; init; } = [];

    public ComicMetadataSource Source { get; init; } = ComicMetadataSource.FileNameFallback;
}
