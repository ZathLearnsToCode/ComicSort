namespace ComicSort.Engine.Models;

public sealed class ComicFileRemovedEventArgs : EventArgs
{
    public string FilePath { get; init; } = string.Empty;
}
