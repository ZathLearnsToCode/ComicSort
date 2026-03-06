using ComicSort.Engine.Data;
using ComicSort.Engine.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
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

    private const int LookupPrefetchBatchSize = 300;

    private readonly object _stateLock = new();
    private readonly object _progressLock = new();
    private readonly object _relinkLock = new();

    private readonly ISettingsService _settingsService;
    private readonly IComicDatabaseService _databaseService;
    private readonly IScanRepository _scanRepository;
    private readonly IArchiveImageService _archiveImageService;
    private readonly IComicMetadataService _comicMetadataService;
    private readonly IThumbnailCacheService _thumbnailCacheService;
    private readonly ILogger<ScanService> _logger;
    private readonly ConcurrentDictionary<string, ComicFileLookup> _lookupCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _seenPathsDuringScan = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ComicFileLookup> _initialLookupByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ComicFileLookup>> _relinkCandidatesByNameSize = new(StringComparer.Ordinal);
    private readonly HashSet<string> _initialPathsAtScanStart = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _scanCts;
    private Task? _runningScanTask;

    private long _filesEnumerated;
    private long _filesQueued;
    private long _filesInserted;
    private long _filesUpdated;
    private long _filesSkipped;
    private long _filesFailed;
    private long _lastProgressAtMs;
    private long _nextSequenceNumber;
    private bool _removeMissingFilesDuringScan;
    private string[] _activeScanRootPrefixes = [];

    private string _currentFilePath = string.Empty;
    private string _currentStage = "Idle";

    public ScanService(
        ISettingsService settingsService,
        IComicDatabaseService databaseService,
        IScanRepository scanRepository,
        IArchiveImageService archiveImageService,
        IComicMetadataService comicMetadataService,
        IThumbnailCacheService thumbnailCacheService,
        ILogger<ScanService> logger)
    {
        _settingsService = settingsService;
        _databaseService = databaseService;
        _scanRepository = scanRepository;
        _archiveImageService = archiveImageService;
        _comicMetadataService = comicMetadataService;
        _thumbnailCacheService = thumbnailCacheService;
        _logger = logger;
    }

    public bool IsRunning { get; private set; }

    public event EventHandler<ScanProgressUpdate>? ProgressChanged;

    public event EventHandler<ComicFileSavedEventArgs>? ComicFileSaved;

    public event EventHandler<ComicFileRemovedEventArgs>? ComicFileRemoved;

    public event EventHandler<ScanStateChangedEventArgs>? StateChanged;

    public Task StartScanAsync(CancellationToken cancellationToken = default)
    {
        return StartScanInternalAsync(selectedFolders: null, cancellationToken);
    }

    public Task StartScanAsync(
        IReadOnlyCollection<string> selectedFolders,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedFolders);
        return StartScanInternalAsync(selectedFolders, cancellationToken);
    }

    private Task StartScanInternalAsync(
        IReadOnlyCollection<string>? selectedFolders,
        CancellationToken cancellationToken)
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
            var requestedFolders = selectedFolders?
                .Select(NormalizeDirectoryPath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();

            _runningScanTask = Task.Run(() => RunScanCoreAsync(_scanCts.Token, requestedFolders), CancellationToken.None);
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

    private async Task RunScanCoreAsync(
        CancellationToken cancellationToken,
        IReadOnlyCollection<string>? requestedFolders)
    {
        var completedStage = "Completed";

        try
        {
            await _settingsService.InitializeAsync(cancellationToken);
            await _databaseService.InitializeAsync(cancellationToken);
            _removeMissingFilesDuringScan = _settingsService.CurrentSettings.RemoveMissingFilesDuringScan;

            var workerCount = Math.Clamp(
                _settingsService.CurrentSettings.ScanWorkerCount,
                1,
                Math.Max(1, Math.Min(16, Environment.ProcessorCount * 2)));
            var batchSize = Math.Clamp(_settingsService.CurrentSettings.ScanBatchSize, 1, 2_000);

            var configuredFolders = _settingsService.CurrentSettings.LibraryFolders
                .Select(x => NormalizeDirectoryPath(x.Folder))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();
            var isTargetedScan = requestedFolders is not null;
            var libraryFolders = ResolveFoldersToScan(configuredFolders, requestedFolders);

            _activeScanRootPrefixes = BuildScanRootPrefixes(libraryFolders);

            if (libraryFolders.Length == 0)
            {
                SetStage(isTargetedScan ? "No valid folders selected" : "No folders configured");
                PublishProgress(force: true);
                completedStage = isTargetedScan ? "NoFoldersSelected" : "NoFolders";
                return;
            }

            if (_removeMissingFilesDuringScan)
            {
                SetStage("Loading scan baseline");
                var existingLookups = await _scanRepository.GetAllLookupsAsync(cancellationToken);
                InitializeScanSnapshot(existingLookups);
            }

            _logger.LogInformation(
                "Library scan started. Mode={Mode}, Workers={WorkerCount}, BatchSize={BatchSize}, Folders={FolderCount}, RemoveMissing={RemoveMissing}",
                isTargetedScan ? "Targeted" : "Full",
                workerCount,
                batchSize,
                libraryFolders.Length,
                _removeMissingFilesDuringScan);

            var fileChannel = Channel.CreateBounded<FileScanWorkItem>(new BoundedChannelOptions(4_096)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = workerCount == 1
            });

            var persistChannel = Channel.CreateBounded<ComicFileUpsertModel>(new BoundedChannelOptions(2_048)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = workerCount == 1,
                SingleReader = true
            });

            var producerTask = ProduceFilesAsync(libraryFolders, fileChannel.Writer, cancellationToken);
            var workerTasks = Enumerable.Range(0, workerCount)
                .Select(_ => ProcessFilesAsync(fileChannel.Reader, persistChannel.Writer, cancellationToken))
                .ToArray();
            var writerTask = PersistBatchesAsync(persistChannel.Reader, batchSize, cancellationToken);

            await producerTask;
            await Task.WhenAll(workerTasks);

            persistChannel.Writer.TryComplete();
            await writerTask;

            if (_removeMissingFilesDuringScan)
            {
                await RemoveMissingFilesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            completedStage = "Cancelled";
            _logger.LogInformation("Library scan cancelled.");
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _filesFailed);
            SetStage($"Failed: {ex.Message}");
            completedStage = "Failed";
            _logger.LogError(ex, "Library scan failed.");
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

            _logger.LogInformation(
                "Library scan finished. Stage={Stage}, Enumerated={Enumerated}, Inserted={Inserted}, Updated={Updated}, Skipped={Skipped}, Failed={Failed}",
                completedStage,
                _filesEnumerated,
                _filesInserted,
                _filesUpdated,
                _filesSkipped,
                _filesFailed);

            StateChanged?.Invoke(this, new ScanStateChangedEventArgs
            {
                IsRunning = false,
                Stage = completedStage
            });
        }
    }

    private async Task ProduceFilesAsync(
        IReadOnlyList<string> folders,
        ChannelWriter<FileScanWorkItem> writer,
        CancellationToken cancellationToken)
    {
        SetStage("Enumerating files");
        var prefetchBatch = new List<FileScanWorkItem>(LookupPrefetchBatchSize);

        async Task FlushPrefetchBatchAsync()
        {
            if (prefetchBatch.Count == 0)
            {
                return;
            }

            IReadOnlyDictionary<string, ComicFileLookup> lookupRows;
            if (_removeMissingFilesDuringScan)
            {
                lock (_relinkLock)
                {
                    var rowMap = new Dictionary<string, ComicFileLookup>(StringComparer.OrdinalIgnoreCase);
                    foreach (var workItem in prefetchBatch)
                    {
                        if (_initialLookupByPath.TryGetValue(workItem.NormalizedPath, out var existing))
                        {
                            rowMap[workItem.NormalizedPath] = existing;
                        }
                    }

                    lookupRows = rowMap;
                }
            }
            else
            {
                lookupRows = await _scanRepository.GetByNormalizedPathsAsync(
                    prefetchBatch.Select(x => x.NormalizedPath).ToArray(),
                    cancellationToken);
            }

            foreach (var workItem in prefetchBatch)
            {
                _seenPathsDuringScan.TryAdd(workItem.NormalizedPath, 0);

                if (lookupRows.TryGetValue(workItem.NormalizedPath, out var existing))
                {
                    workItem.ExistingLookup = existing;
                    _lookupCache[workItem.NormalizedPath] = existing;
                }

                await writer.WriteAsync(workItem, cancellationToken);
                UpdateCurrentFile(workItem.FilePath);
                PublishProgress();
            }

            prefetchBatch.Clear();
        }

        try
        {
            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(folder))
                {
                    Interlocked.Increment(ref _filesFailed);
                    _logger.LogWarning("Configured library folder does not exist: {Folder}", folder);
                    continue;
                }

                try
                {
                    foreach (var filePath in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var extension = Path.GetExtension(filePath);
                        if (!SupportedArchiveExtensions.Contains(extension))
                        {
                            continue;
                        }

                        var normalizedPath = NormalizePath(filePath);
                        var sequenceNumber = Interlocked.Increment(ref _nextSequenceNumber);
                        prefetchBatch.Add(new FileScanWorkItem
                        {
                            FilePath = filePath,
                            NormalizedPath = normalizedPath,
                            SequenceNumber = sequenceNumber
                        });

                        Interlocked.Increment(ref _filesEnumerated);
                        Interlocked.Increment(ref _filesQueued);

                        if (prefetchBatch.Count >= LookupPrefetchBatchSize)
                        {
                            await FlushPrefetchBatchAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _filesFailed);
                    _logger.LogWarning(ex, "Failed to enumerate folder {Folder}", folder);
                }
            }

            await FlushPrefetchBatchAsync();
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task ProcessFilesAsync(
        ChannelReader<FileScanWorkItem> fileReader,
        ChannelWriter<ComicFileUpsertModel> persistWriter,
        CancellationToken cancellationToken)
    {
        SetStage("Processing files");

        await foreach (var workItem in fileReader.ReadAllAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var upsert = await ProcessSingleFileAsync(workItem, cancellationToken);
            if (upsert is null)
            {
                continue;
            }

            await persistWriter.WriteAsync(upsert, cancellationToken);
            PublishProgress();
        }
    }

    private async Task<ComicFileUpsertModel?> ProcessSingleFileAsync(
        FileScanWorkItem workItem,
        CancellationToken cancellationToken)
    {
        ComicFileLookup? existingLookupForFailure = workItem.ExistingLookup;

        try
        {
            var fileInfo = new FileInfo(workItem.FilePath);
            if (!fileInfo.Exists)
            {
                return null;
            }

            var normalizedPath = workItem.NormalizedPath;
            var modifiedUtc = fileInfo.LastWriteTimeUtc;
            var createdUtc = fileInfo.CreationTimeUtc;
            var fingerprint = $"{fileInfo.Length}|{modifiedUtc.Ticks}";
            var extension = fileInfo.Extension;

            var existing = workItem.ExistingLookup;
            if (existing is null && _lookupCache.TryGetValue(normalizedPath, out var cachedLookup))
            {
                existing = cachedLookup;
            }

            if (existing is null && _removeMissingFilesDuringScan)
            {
                existing = await TryRelinkExistingByFileNameSizeAsync(fileInfo, normalizedPath, cancellationToken);
            }
            existingLookupForFailure = existing;

            if (existing is not null &&
                string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal) &&
                existing.HasThumbnail &&
                !string.IsNullOrWhiteSpace(existing.ThumbnailPath) &&
                existing.HasComicInfo)
            {
                Interlocked.Increment(ref _filesSkipped);
                return null;
            }

            var metadata = await _comicMetadataService.GetMetadataAsync(fileInfo.FullName, cancellationToken);

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
                SequenceNumber = workItem.SequenceNumber,
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
                LastError = lastError,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _filesFailed);
            _logger.LogDebug(ex, "Failed to process archive {Path}", workItem.FilePath);

            if (existingLookupForFailure is not null)
            {
                // Keep prior DB values for already indexed files when this scan attempt fails.
                return null;
            }

            if (TryCreateFailureUpsertModel(workItem, ex.Message, out var failureModel))
            {
                return failureModel;
            }

            return null;
        }
    }

    private static bool TryCreateFailureUpsertModel(
        FileScanWorkItem workItem,
        string error,
        out ComicFileUpsertModel model)
    {
        model = null!;

        FileInfo fileInfo;
        try
        {
            fileInfo = new FileInfo(workItem.FilePath);
        }
        catch
        {
            return false;
        }

        if (!fileInfo.Exists)
        {
            return false;
        }

        var modifiedUtc = fileInfo.LastWriteTimeUtc;
        var createdUtc = fileInfo.CreationTimeUtc;

        model = new ComicFileUpsertModel
        {
            SequenceNumber = workItem.SequenceNumber,
            NormalizedPath = workItem.NormalizedPath,
            FileName = fileInfo.Name,
            Extension = fileInfo.Extension,
            SizeBytes = fileInfo.Length,
            CreatedUtc = createdUtc,
            ModifiedUtc = modifiedUtc,
            LastScannedUtc = DateTimeOffset.UtcNow,
            Fingerprint = $"{fileInfo.Length}|{modifiedUtc.Ticks}",
            ScanState = ScanState.Error,
            LastError = error
        };

        return true;
    }

    private async Task PersistBatchesAsync(
        ChannelReader<ComicFileUpsertModel> reader,
        int batchSize,
        CancellationToken cancellationToken)
    {
        SetStage("Persisting");

        var batch = new List<ComicFileUpsertModel>(batchSize);

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

            foreach (var model in snapshot)
            {
                var lookup = new ComicFileLookup
                {
                    NormalizedPath = model.NormalizedPath,
                    FileName = model.FileName,
                    SizeBytes = model.SizeBytes,
                    Fingerprint = model.Fingerprint,
                    HasThumbnail = model.HasThumbnail,
                    ThumbnailPath = model.ThumbnailPath,
                    HasComicInfo = model.Metadata is not null
                };

                _lookupCache[model.NormalizedPath] = lookup;
                if (_removeMissingFilesDuringScan)
                {
                    lock (_relinkLock)
                    {
                        _initialLookupByPath[model.NormalizedPath] = lookup;
                    }
                }
            }

            foreach (var savedItem in result.SavedItems.OrderBy(x => x.SequenceNumber))
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

    private async Task<ComicFileLookup?> TryRelinkExistingByFileNameSizeAsync(
        FileInfo fileInfo,
        string normalizedPath,
        CancellationToken cancellationToken)
    {
        var key = BuildRelinkKey(fileInfo.Name, fileInfo.Length);
        List<ComicFileLookup> candidatesSnapshot;

        lock (_relinkLock)
        {
            if (!_relinkCandidatesByNameSize.TryGetValue(key, out var candidates))
            {
                return null;
            }

            candidatesSnapshot = candidates.ToList();
        }

        foreach (var relinkCandidate in candidatesSnapshot)
        {
            if (string.Equals(relinkCandidate.NormalizedPath, normalizedPath, StringComparison.OrdinalIgnoreCase) ||
                _seenPathsDuringScan.ContainsKey(relinkCandidate.NormalizedPath))
            {
                continue;
            }

            if (File.Exists(relinkCandidate.NormalizedPath))
            {
                continue;
            }

            var relinked = await _scanRepository.RewritePathForFileRenameAsync(
                relinkCandidate.NormalizedPath,
                normalizedPath,
                cancellationToken);
            if (!relinked)
            {
                continue;
            }

            lock (_relinkLock)
            {
                if (_relinkCandidatesByNameSize.TryGetValue(key, out var candidates))
                {
                    candidates.RemoveAll(candidate =>
                        string.Equals(candidate.NormalizedPath, relinkCandidate.NormalizedPath, StringComparison.OrdinalIgnoreCase));
                }

                _initialLookupByPath.Remove(relinkCandidate.NormalizedPath);
                _initialPathsAtScanStart.Remove(relinkCandidate.NormalizedPath);
            }
            _lookupCache.TryRemove(relinkCandidate.NormalizedPath, out _);
            ComicFileRemoved?.Invoke(this, new ComicFileRemovedEventArgs
            {
                FilePath = relinkCandidate.NormalizedPath
            });

            var updatedLookup = new ComicFileLookup
            {
                NormalizedPath = normalizedPath,
                FileName = fileInfo.Name,
                SizeBytes = fileInfo.Length,
                Fingerprint = relinkCandidate.Fingerprint,
                HasThumbnail = relinkCandidate.HasThumbnail,
                ThumbnailPath = relinkCandidate.ThumbnailPath,
                HasComicInfo = relinkCandidate.HasComicInfo
            };

            _lookupCache[normalizedPath] = updatedLookup;
            lock (_relinkLock)
            {
                _initialLookupByPath[normalizedPath] = updatedLookup;
            }
            _logger.LogInformation("Relinked missing path {OldPath} to {NewPath}", relinkCandidate.NormalizedPath, normalizedPath);
            return updatedLookup;
        }

        return null;
    }

    private async Task RemoveMissingFilesAsync(CancellationToken cancellationToken)
    {
        SetStage("Removing missing files");

        string[] missingPaths;
        lock (_relinkLock)
        {
            missingPaths = _initialPathsAtScanStart
                .Where(path => IsPathInCurrentScanRoots(path) && !_seenPathsDuringScan.ContainsKey(path))
                .ToArray();
        }

        if (missingPaths.Length == 0)
        {
            return;
        }

        var removedPaths = await _scanRepository.DeleteByNormalizedPathsAsync(missingPaths, cancellationToken);
        foreach (var path in removedPaths)
        {
            _lookupCache.TryRemove(path, out _);
            ComicFileRemoved?.Invoke(this, new ComicFileRemovedEventArgs
            {
                FilePath = path
            });
        }

        if (removedPaths.Count > 0)
        {
            _logger.LogInformation("Removed {RemovedCount} missing files from library.", removedPaths.Count);
        }
    }

    private void InitializeScanSnapshot(IReadOnlyList<ComicFileLookup> existingLookups)
    {
        lock (_relinkLock)
        {
            _initialLookupByPath.Clear();
            _relinkCandidatesByNameSize.Clear();
            _initialPathsAtScanStart.Clear();

            foreach (var lookup in existingLookups)
            {
                _initialLookupByPath[lookup.NormalizedPath] = lookup;
                _initialPathsAtScanStart.Add(lookup.NormalizedPath);

                var relinkKey = BuildRelinkKey(lookup.FileName, lookup.SizeBytes);
                if (!_relinkCandidatesByNameSize.TryGetValue(relinkKey, out var candidates))
                {
                    candidates = [];
                    _relinkCandidatesByNameSize[relinkKey] = candidates;
                }

                candidates.Add(lookup);
            }
        }
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
        Interlocked.Exchange(ref _nextSequenceNumber, 0);
        _removeMissingFilesDuringScan = false;
        _activeScanRootPrefixes = [];
        _lookupCache.Clear();
        _seenPathsDuringScan.Clear();

        lock (_relinkLock)
        {
            _initialLookupByPath.Clear();
            _relinkCandidatesByNameSize.Clear();
            _initialPathsAtScanStart.Clear();
        }

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

    private static string BuildRelinkKey(string fileName, long fileSize)
    {
        return string.Concat(fileName.Trim(), "|", fileSize.ToString(CultureInfo.InvariantCulture));
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).Trim();
    }

    private static string NormalizeDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var normalized = Path.GetFullPath(path).Trim();
            var trimmed = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(normalized);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(trimmed, trimmedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return root.EndsWith(Path.DirectorySeparatorChar) || root.EndsWith(Path.AltDirectorySeparatorChar)
                        ? root
                        : string.Concat(root, Path.DirectorySeparatorChar);
                }
            }

            return trimmed;
        }
        catch
        {
            return path.Trim();
        }
    }

    private static string[] ResolveFoldersToScan(
        IReadOnlyCollection<string> configuredFolders,
        IReadOnlyCollection<string>? requestedFolders)
    {
        if (requestedFolders is null)
        {
            return configuredFolders.ToArray();
        }

        var configuredSet = configuredFolders.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (configuredSet.Count == 0)
        {
            return [];
        }

        return requestedFolders
            .Select(NormalizeDirectoryPath)
            .Where(x => !string.IsNullOrWhiteSpace(x) && configuredSet.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();
    }

    private static string[] BuildScanRootPrefixes(IReadOnlyCollection<string> roots)
    {
        return roots
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => x.EndsWith(Path.DirectorySeparatorChar) || x.EndsWith(Path.AltDirectorySeparatorChar)
                ? x
                : string.Concat(x, Path.DirectorySeparatorChar))
            .OrderByDescending(x => x.Length)
            .ToArray();
    }

    private bool IsPathInCurrentScanRoots(string normalizedPath)
    {
        var roots = _activeScanRootPrefixes;
        if (roots.Length == 0 || string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        foreach (var rootPrefix in roots)
        {
            if (normalizedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class FileScanWorkItem
    {
        public string FilePath { get; init; } = string.Empty;

        public string NormalizedPath { get; init; } = string.Empty;

        public long SequenceNumber { get; init; }

        public ComicFileLookup? ExistingLookup { get; set; }
    }
}
