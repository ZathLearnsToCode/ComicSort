namespace ComicSort.Engine.Models;

public sealed class CbzConversionFileResult
{
    public string SourcePath { get; init; } = string.Empty;

    public string? DestinationPath { get; init; }

    public bool Success { get; init; }

    public bool OriginalRemoved { get; init; }

    public string? Error { get; init; }

    public ComicLibraryItem? SavedLibraryItem { get; init; }
}
