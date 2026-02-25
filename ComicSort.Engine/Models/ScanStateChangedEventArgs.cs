namespace ComicSort.Engine.Models;

public sealed class ScanStateChangedEventArgs : EventArgs
{
    public bool IsRunning { get; init; }

    public string Stage { get; init; } = string.Empty;
}
