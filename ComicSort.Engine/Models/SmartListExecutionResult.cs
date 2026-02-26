namespace ComicSort.Engine.Models;

public sealed class SmartListExecutionResult
{
    public IReadOnlyList<ComicLibraryItem> Items { get; init; } = [];

    public int LoadedCount { get; init; }

    public bool ResidualRequired { get; init; }

    public string Summary { get; init; } = string.Empty;
}
