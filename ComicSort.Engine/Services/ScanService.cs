using ComicSort.Engine.Models;
using Microsoft.Extensions.Logging;

namespace ComicSort.Engine.Services;

public sealed class ScanService : IScanService
{
    private readonly object _stateLock = new();
    private readonly IScanPipelineCoordinator _scanPipelineCoordinator;
    private readonly IScanProgressTracker _progressTracker;
    private readonly ILogger<ScanService> _logger;

    private CancellationTokenSource? _scanCts;
    private Task? _runningScanTask;

    public ScanService(
        IScanPipelineCoordinator scanPipelineCoordinator,
        IScanProgressTracker progressTracker,
        ILogger<ScanService> logger)
    {
        _scanPipelineCoordinator = scanPipelineCoordinator;
        _progressTracker = progressTracker;
        _logger = logger;
    }

    public bool IsRunning { get; private set; }

    public event EventHandler<ScanProgressUpdate>? ProgressChanged;

    public event EventHandler<ComicFileSavedEventArgs>? ComicFileSaved;

    public event EventHandler<ComicFileRemovedEventArgs>? ComicFileRemoved;

    public event EventHandler<ScanStateChangedEventArgs>? StateChanged;

    public Task StartScanAsync(CancellationToken cancellationToken = default)
        => StartScanInternalAsync(selectedFolders: null, cancellationToken);

    public Task StartScanAsync(IReadOnlyCollection<string> selectedFolders, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedFolders);
        return StartScanInternalAsync(selectedFolders, cancellationToken);
    }

    public void CancelScan()
    {
        lock (_stateLock)
        {
            _scanCts?.Cancel();
        }
    }

    private Task StartScanInternalAsync(IReadOnlyCollection<string>? selectedFolders, CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (IsRunning)
            {
                return _runningScanTask ?? Task.CompletedTask;
            }

            _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsRunning = true;
            _progressTracker.Reset();
            _runningScanTask = Task.Run(() => RunScanCoreAsync(_scanCts.Token, selectedFolders), CancellationToken.None);
            StateChanged?.Invoke(this, new ScanStateChangedEventArgs { IsRunning = true, Stage = "Started" });
            return _runningScanTask;
        }
    }

    private async Task RunScanCoreAsync(CancellationToken cancellationToken, IReadOnlyCollection<string>? requestedFolders)
    {
        var completedStage = await ExecuteScanAsync(cancellationToken, requestedFolders);
        FinalizeRun(completedStage);
    }

    private async Task<string> ExecuteScanAsync(CancellationToken cancellationToken, IReadOnlyCollection<string>? requestedFolders)
    {
        try
        {
            return await _scanPipelineCoordinator.RunAsync(
                requestedFolders,
                OnComicFileSaved,
                OnComicFileRemoved,
                () => PublishProgress(),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Library scan cancelled.");
            return "Cancelled";
        }
        catch (Exception ex)
        {
            return HandleScanFailure(ex);
        }
    }

    private string HandleScanFailure(Exception ex)
    {
        _progressTracker.IncrementFailed();
        _progressTracker.SetStage($"Failed: {ex.Message}");
        _logger.LogError(ex, "Library scan failed.");
        return "Failed";
    }

    private void OnComicFileSaved(ComicLibraryItem item)
    {
        ComicFileSaved?.Invoke(this, new ComicFileSavedEventArgs { Item = item });
    }

    private void OnComicFileRemoved(string filePath)
    {
        ComicFileRemoved?.Invoke(this, new ComicFileRemovedEventArgs { FilePath = filePath });
    }

    private void PublishProgress(bool force = false)
    {
        if (!_progressTracker.ShouldPublish(force))
        {
            return;
        }

        ProgressChanged?.Invoke(this, _progressTracker.CreateUpdate());
    }

    private void FinalizeRun(string completedStage)
    {
        PublishProgress(force: true);
        lock (_stateLock)
        {
            IsRunning = false;
            _scanCts?.Dispose();
            _scanCts = null;
            _runningScanTask = null;
        }

        var progress = _progressTracker.CreateUpdate();
        _logger.LogInformation(
            "Library scan finished. Stage={Stage}, Enumerated={Enumerated}, Inserted={Inserted}, Updated={Updated}, Skipped={Skipped}, Failed={Failed}",
            completedStage,
            progress.FilesEnumerated,
            progress.FilesInserted,
            progress.FilesUpdated,
            progress.FilesSkipped,
            progress.FilesFailed);
        StateChanged?.Invoke(this, new ScanStateChangedEventArgs { IsRunning = false, Stage = completedStage });
    }
}
