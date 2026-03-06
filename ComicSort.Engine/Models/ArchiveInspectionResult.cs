namespace ComicSort.Engine.Models;

public sealed class ArchiveInspectionResult
{
    public bool Success { get; init; }

    public string ArchivePath { get; init; } = string.Empty;

    public string? Error { get; init; }

    public string? FirstImageEntryPath { get; init; }

    public string? ComicInfoEntryPath { get; init; }

    public IReadOnlyList<string> ImageEntryPaths { get; init; } = [];
}
