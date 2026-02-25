namespace ComicSort.Engine.Models;

public sealed class ScanProgressUpdate
{
    public string Stage { get; init; } = string.Empty;

    public string CurrentFilePath { get; init; } = string.Empty;

    public long FilesEnumerated { get; init; }

    public long FilesQueued { get; init; }

    public long FilesInserted { get; init; }

    public long FilesUpdated { get; init; }

    public long FilesSkipped { get; init; }

    public long FilesFailed { get; init; }
}
