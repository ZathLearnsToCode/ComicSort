using ComicSort.Engine.Data;
using ComicSort.Engine.Models;
using System.Threading.Channels;

namespace ComicSort.Engine.Services;

public sealed class ScanService : IScanService
{
    private static readonly HashSet<string> SupportedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbr",
        ".cbz",
        ".cb7"
    };

    private readonly object _stateLock = new();
    private readonly object _progressLock = new();

    private readonly ISettingsService _settingsService;
    private readonly IComicDatabaseService _databaseService;
    private readonly IScanRepository _scanRepository;
    private readonly IArchiveImageService _archiveImageService;
    private readonly IThumbnailCacheService _thumbnailCacheService;

    private CancellationTokenSource? _scanCts;
    private Task? _runningScanTask;

    private long _filesEnumerated;
    private long _filesQueued;
    private long _filesInserted;
    private long _filesUpdated;
    private long _filesSkipped;
    private long _filesFailed;
    private long _lastProgressAtMs;

    private string _currentFilePath = string.Empty;
    private string _currentStage = "Idle";

    public ScanService(
        ISettingsService settingsService,
        IComicDatabaseService databaseService,
        IScanRepository scanRepository,
        IArchiveImageService archiveImageService,
        IThumbnailCacheService thumbnailCacheService)
    {
        _settingsService = settingsService;
        _databaseService = databaseService;
        _scanRepository = scanRepository;
        _archiveImageService = archiveImageService;
        _thumbnailCacheService = thumbnailCacheService;
    }

    public bool IsRunning { get; private set; }

    public event EventHandler<ScanProgressUpdate>? ProgressChanged;

    public event EventHandler<ComicFileSavedEventArgs>? ComicFileSaved;

    public event EventHandler<ScanStateChangedEventArgs>? StateChanged;

    public Task StartScanAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (IsRunning)
            {
                return _runningScanTask ?? Task.CompletedTask;
            }

            _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsRunning = true;
            ResetProgress();
            _runningScanTask = Task.Run(() => RunScanCoreAsync(_scanCts.Token), CancellationToken.None);
            StateChanged?.Invoke(this, new ScanStateChangedEventArgs { IsRunning = true, Stage = "Started" });
            return _runningScanTask;
        }
    }

    public void CancelScan()
    {
        lock (_stateLock)
        {
            _scanCts?.Cancel();
        }
    }

    private async Task RunScanCoreAsync(CancellationToken cancellationToken)
    {
        var completedStage = "Completed";

        try
        {
            await _databaseService.InitializeAsync(cancellationToken);

            var libraryFolders = _settingsService.CurrentSettings.LibraryFolders
                .Select(x => x.Folder?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();

            if (libraryFolders.Length == 0)
            {
                SetStage("No folders configured");
                PublishProgress(force: true);
                completedStage = "NoFolders";
                return;
            }

            var fileChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(4096)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = true
            });

            var persistChannel = Channel.CreateBounded<ComicFileUpsertModel>(new BoundedChannelOptions(2048)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = true
            });

            // Sequential processing ensures tiles are added in the same order files are scanned.
            var workerCount = 1;
            var producerTask = ProduceFilesAsync(libraryFolders, fileChannel.Writer, cancellationToken);
            var workerTasks = Enumerable.Range(0, workerCount)
                .Select(_ => ProcessFilesAsync(fileChannel.Reader, persistChannel.Writer, cancellationToken))
                .ToArray();
            var writerTask = PersistBatchesAsync(persistChannel.Reader, cancellationToken);

            await producerTask;
            await Task.WhenAll(workerTasks);

            persistChannel.Writer.TryComplete();
            await writerTask;
        }
        catch (OperationCanceledException)
        {
            completedStage = "Cancelled";
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _filesFailed);
            SetStage($"Failed: {ex.Message}");
            completedStage = "Failed";
        }
        finally
        {
            PublishProgress(force: true);

            lock (_stateLock)
            {
                IsRunning = false;
                _scanCts?.Dispose();
                _scanCts = null;
                _runningScanTask = null;
            }

            StateChanged?.Invoke(this, new ScanStateChangedEventArgs
            {
                IsRunning = false,
                Stage = completedStage
            });
        }
    }

    private async Task ProduceFilesAsync(
        IReadOnlyList<string> folders,
        ChannelWriter<string> writer,
        CancellationToken cancellationToken)
    {
        SetStage("Enumerating files");

        try
        {
            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(folder))
                {
                    Interlocked.Increment(ref _filesFailed);
                    continue;
                }

                foreach (var filePath in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var extension = Path.GetExtension(filePath);
                    if (!SupportedArchiveExtensions.Contains(extension))
                    {
                        continue;
                    }

                    Interlocked.Increment(ref _filesEnumerated);
                    Interlocked.Increment(ref _filesQueued);
                    await writer.WriteAsync(filePath, cancellationToken);
                    UpdateCurrentFile(filePath);
                    PublishProgress();
                }
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task ProcessFilesAsync(
        ChannelReader<string> fileReader,
        ChannelWriter<ComicFileUpsertModel> persistWriter,
        CancellationToken cancellationToken)
    {
        SetStage("Processing files");

        await foreach (var filePath in fileReader.ReadAllAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var upsert = await ProcessSingleFileAsync(filePath, cancellationToken);
            if (upsert is null)
            {
                continue;
            }

            await persistWriter.WriteAsync(upsert, cancellationToken);
            PublishProgress();
        }
    }

    private async Task<ComicFileUpsertModel?> ProcessSingleFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return null;
            }

            var normalizedPath = NormalizePath(fileInfo.FullName);
            var modifiedUtc = fileInfo.LastWriteTimeUtc;
            var createdUtc = fileInfo.CreationTimeUtc;
            var fingerprint = $"{fileInfo.Length}|{modifiedUtc.Ticks}";
            var extension = fileInfo.Extension;

            var existing = await _scanRepository.GetByNormalizedPathAsync(normalizedPath, cancellationToken);
            if (existing is not null &&
                string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal) &&
                existing.HasThumbnail &&
                !string.IsNullOrWhiteSpace(existing.ThumbnailPath) &&
                File.Exists(existing.ThumbnailPath))
            {
                Interlocked.Increment(ref _filesSkipped);
                return null;
            }

            var thumbnailKey = _thumbnailCacheService.ComputeKey(normalizedPath, fingerprint);
            string? thumbnailPath = null;
            var hasThumbnail = false;
            var scanState = ScanState.Pending;
            string? lastError = null;

            if (_thumbnailCacheService.TryGetCachedPath(thumbnailKey, out var cachedPath))
            {
                thumbnailPath = cachedPath;
                hasThumbnail = true;
                scanState = ScanState.Ok;
            }
            else
            {
                var imageResult = await _archiveImageService.TryGetFirstImageAsync(fileInfo.FullName, cancellationToken);
                if (imageResult.Success && imageResult.ImageBytes is not null)
                {
                    var thumbnailResult = await _thumbnailCacheService.WriteThumbnailAsync(
                        thumbnailKey,
                        imageResult.ImageBytes,
                        cancellationToken);

                    if (thumbnailResult.Success)
                    {
                        thumbnailPath = thumbnailResult.ThumbnailPath;
                        hasThumbnail = true;
                        scanState = ScanState.Ok;
                    }
                    else
                    {
                        scanState = ScanState.Error;
                        lastError = thumbnailResult.Error;
                        Interlocked.Increment(ref _filesFailed);
                    }
                }
                else
                {
                    scanState = ScanState.Error;
                    lastError = imageResult.Error;
                    Interlocked.Increment(ref _filesFailed);
                }
            }

            return new ComicFileUpsertModel
            {
                NormalizedPath = normalizedPath,
                FileName = fileInfo.Name,
                Extension = extension,
                SizeBytes = fileInfo.Length,
                CreatedUtc = createdUtc,
                ModifiedUtc = modifiedUtc,
                LastScannedUtc = DateTimeOffset.UtcNow,
                Fingerprint = fingerprint,
                ThumbnailKey = thumbnailKey,
                ThumbnailPath = thumbnailPath,
                HasThumbnail = hasThumbnail,
                ScanState = scanState,
                LastError = lastError
            };
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _filesFailed);
            return new ComicFileUpsertModel
            {
                NormalizedPath = NormalizePath(filePath),
                FileName = Path.GetFileName(filePath),
                Extension = Path.GetExtension(filePath),
                SizeBytes = 0,
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
                LastScannedUtc = DateTimeOffset.UtcNow,
                Fingerprint = "0|0",
                ScanState = ScanState.Error,
                LastError = ex.Message
            };
        }
    }

    private async Task PersistBatchesAsync(
        ChannelReader<ComicFileUpsertModel> reader,
        CancellationToken cancellationToken)
    {
        SetStage("Persisting");

        var batch = new List<ComicFileUpsertModel>(1);
        const int batchSize = 1;

        async Task FlushAsync()
        {
            if (batch.Count == 0)
            {
                return;
            }

            var snapshot = batch.ToArray();
            batch.Clear();

            var result = await _scanRepository.UpsertBatchAsync(snapshot, cancellationToken);
            Interlocked.Add(ref _filesInserted, result.Inserted);
            Interlocked.Add(ref _filesUpdated, result.Updated);

            foreach (var savedItem in result.SavedItems)
            {
                ComicFileSaved?.Invoke(this, new ComicFileSavedEventArgs { Item = savedItem });
            }

            PublishProgress();
        }

        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            batch.Add(item);
            if (batch.Count < batchSize)
            {
                continue;
            }

            await FlushAsync();
        }

        await FlushAsync();
    }

    private void ResetProgress()
    {
        Interlocked.Exchange(ref _filesEnumerated, 0);
        Interlocked.Exchange(ref _filesQueued, 0);
        Interlocked.Exchange(ref _filesInserted, 0);
        Interlocked.Exchange(ref _filesUpdated, 0);
        Interlocked.Exchange(ref _filesSkipped, 0);
        Interlocked.Exchange(ref _filesFailed, 0);
        Interlocked.Exchange(ref _lastProgressAtMs, 0);

        lock (_progressLock)
        {
            _currentFilePath = string.Empty;
            _currentStage = "Starting";
        }
    }

    private void SetStage(string stage)
    {
        lock (_progressLock)
        {
            _currentStage = stage;
        }
    }

    private void UpdateCurrentFile(string filePath)
    {
        lock (_progressLock)
        {
            _currentFilePath = filePath;
        }
    }

    private void PublishProgress(bool force = false)
    {
        var intervalMs = Math.Max(10, _settingsService.CurrentSettings.ScanStatusUpdateIntervalMs);
        var now = Environment.TickCount64;
        var previous = Interlocked.Read(ref _lastProgressAtMs);

        if (!force && now - previous < intervalMs)
        {
            return;
        }

        Interlocked.Exchange(ref _lastProgressAtMs, now);

        string stage;
        string currentFile;

        lock (_progressLock)
        {
            stage = _currentStage;
            currentFile = _currentFilePath;
        }

        ProgressChanged?.Invoke(this, new ScanProgressUpdate
        {
            Stage = stage,
            CurrentFilePath = currentFile,
            FilesEnumerated = Interlocked.Read(ref _filesEnumerated),
            FilesQueued = Interlocked.Read(ref _filesQueued),
            FilesInserted = Interlocked.Read(ref _filesInserted),
            FilesUpdated = Interlocked.Read(ref _filesUpdated),
            FilesSkipped = Interlocked.Read(ref _filesSkipped),
            FilesFailed = Interlocked.Read(ref _filesFailed)
        });
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).Trim();
    }
}
