namespace ComicSort.Engine.Models;

public sealed class ScanBatchSaveResult
{
    public int Inserted { get; init; }

    public int Updated { get; init; }

    public IReadOnlyList<ComicLibraryItem> SavedItems { get; init; } = [];
}
