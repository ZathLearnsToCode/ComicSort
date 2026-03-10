using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public sealed class ScanProgressTracker : IScanProgressTracker
{
    private readonly object _progressLock = new();
    private readonly ISettingsService _settingsService;

    private long _filesEnumerated;
    private long _filesQueued;
    private long _filesInserted;
    private long _filesUpdated;
    private long _filesSkipped;
    private long _filesFailed;
    private long _lastProgressAtMs;
    private long _nextSequenceNumber;
    private string _currentFilePath = string.Empty;
    private string _currentStage = "Idle";

    public ScanProgressTracker(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _filesEnumerated, 0);
        Interlocked.Exchange(ref _filesQueued, 0);
        Interlocked.Exchange(ref _filesInserted, 0);
        Interlocked.Exchange(ref _filesUpdated, 0);
        Interlocked.Exchange(ref _filesSkipped, 0);
        Interlocked.Exchange(ref _filesFailed, 0);
        Interlocked.Exchange(ref _lastProgressAtMs, 0);
        Interlocked.Exchange(ref _nextSequenceNumber, 0);
        SetCurrentFile(string.Empty);
        SetStage("Starting");
    }

    public void SetStage(string stage)
    {
        lock (_progressLock)
        {
            _currentStage = stage;
        }
    }

    public void SetCurrentFile(string filePath)
    {
        lock (_progressLock)
        {
            _currentFilePath = filePath;
        }
    }

    public void IncrementEnumerated() => Interlocked.Increment(ref _filesEnumerated);

    public void IncrementQueued() => Interlocked.Increment(ref _filesQueued);

    public void IncrementInserted(int count) => Interlocked.Add(ref _filesInserted, count);

    public void IncrementUpdated(int count) => Interlocked.Add(ref _filesUpdated, count);

    public void IncrementSkipped() => Interlocked.Increment(ref _filesSkipped);

    public void IncrementFailed() => Interlocked.Increment(ref _filesFailed);

    public long NextSequenceNumber() => Interlocked.Increment(ref _nextSequenceNumber);

    public bool ShouldPublish(bool force = false)
    {
        var intervalMs = Math.Max(10, _settingsService.CurrentSettings.ScanStatusUpdateIntervalMs);
        var now = Environment.TickCount64;
        var previous = Interlocked.Read(ref _lastProgressAtMs);

        if (!force && now - previous < intervalMs)
        {
            return false;
        }

        Interlocked.Exchange(ref _lastProgressAtMs, now);
        return true;
    }

    public ScanProgressUpdate CreateUpdate()
    {
        string stage;
        string filePath;
        lock (_progressLock)
        {
            stage = _currentStage;
            filePath = _currentFilePath;
        }

        return new ScanProgressUpdate
        {
            Stage = stage,
            CurrentFilePath = filePath,
            FilesEnumerated = Interlocked.Read(ref _filesEnumerated),
            FilesQueued = Interlocked.Read(ref _filesQueued),
            FilesInserted = Interlocked.Read(ref _filesInserted),
            FilesUpdated = Interlocked.Read(ref _filesUpdated),
            FilesSkipped = Interlocked.Read(ref _filesSkipped),
            FilesFailed = Interlocked.Read(ref _filesFailed)
        };
    }
}
