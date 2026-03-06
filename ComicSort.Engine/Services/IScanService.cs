using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IScanService
{
    bool IsRunning { get; }

    event EventHandler<ScanProgressUpdate>? ProgressChanged;

    event EventHandler<ComicFileSavedEventArgs>? ComicFileSaved;

    event EventHandler<ComicFileRemovedEventArgs>? ComicFileRemoved;

    event EventHandler<ScanStateChangedEventArgs>? StateChanged;

    Task StartScanAsync(CancellationToken cancellationToken = default);

    Task StartScanAsync(
        IReadOnlyCollection<string> selectedFolders,
        CancellationToken cancellationToken = default);

    void CancelScan();
}
