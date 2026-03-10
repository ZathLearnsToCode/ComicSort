using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public sealed class ScanFileWorkItem
{
    public string FilePath { get; init; } = string.Empty;

    public string NormalizedPath { get; init; } = string.Empty;

    public long SequenceNumber { get; init; }

    public ComicFileLookup? ExistingLookup { get; set; }
}
