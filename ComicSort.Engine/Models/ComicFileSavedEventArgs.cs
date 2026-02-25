namespace ComicSort.Engine.Models;

public sealed class ComicFileSavedEventArgs : EventArgs
{
    public ComicLibraryItem Item { get; init; } = new();
}
