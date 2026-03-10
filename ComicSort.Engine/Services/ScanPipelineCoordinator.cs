using ComicSort.Engine.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ComicSort.Engine.Services;

public sealed class ScanPipelineCoordinator : IScanPipelineCoordinator
{
    private readonly ISettingsService _settingsService;
    private readonly IComicDatabaseService _databaseService;
    private readonly IScanRunSettingsFactory _runSettingsFactory;
    private readonly IScanLookupCacheService _lookupCacheService;
    private readonly IScanRelinkService _scanRelinkService;
    private readonly IScanFileProducer _scanFileProducer;
    private readonly IScanFileProcessor _scanFileProcessor;
    private readonly IScanBatchPersister _scanBatchPersister;
    private readonly IScanProgressTracker _progressTracker;
    private readonly ILogger<ScanPipelineCoordinator> _logger;

    public ScanPipelineCoordinator(
        ISettingsService settingsService,
        IComicDatabaseService databaseService,
        IScanRunSettingsFactory runSettingsFactory,
        IScanLookupCacheService lookupCacheService,
        IScanRelinkService scanRelinkService,
        IScanFileProducer scanFileProducer,
        IScanFileProcessor scanFileProcessor,
        IScanBatchPersister scanBatchPersister,
        IScanProgressTracker progressTracker,
        ILogger<ScanPipelineCoordinator> logger)
    {
        _settingsService = settingsService;
        _databaseService = databaseService;
        _runSettingsFactory = runSettingsFactory;
        _lookupCacheService = lookupCacheService;
        _scanRelinkService = scanRelinkService;
        _scanFileProducer = scanFileProducer;
        _scanFileProcessor = scanFileProcessor;
        _scanBatchPersister = scanBatchPersister;
        _progressTracker = progressTracker;
        _logger = logger;
    }

    public async Task<string> RunAsync(
        IReadOnlyCollection<string>? requestedFolders,
        Action<ComicLibraryItem> onSaved,
        Action<string> onRemoved,
        Action pulseProgress,
        CancellationToken cancellationToken)
    {
        var settings = await BuildRunSettingsAsync(requestedFolders, cancellationToken);
        var stage = await PrepareRunAsync(settings, pulseProgress, cancellationToken);
        if (stage is not null)
        {
            return stage;
        }

        await RunPipelineAsync(settings, onSaved, onRemoved, pulseProgress, cancellationToken);
        await RemoveMissingFilesAsync(settings, onRemoved, cancellationToken);
        return "Completed";
    }

    private async Task<ScanRunSettings> BuildRunSettingsAsync(
        IReadOnlyCollection<string>? requestedFolders,
        CancellationToken cancellationToken)
    {
        await _settingsService.InitializeAsync(cancellationToken);
        await _databaseService.InitializeAsync(cancellationToken);
        return _runSettingsFactory.Create(requestedFolders);
    }

    private async Task<string?> PrepareRunAsync(
        ScanRunSettings settings,
        Action pulseProgress,
        CancellationToken cancellationToken)
    {
        _lookupCacheService.Reset();
        _scanRelinkService.Reset();
        if (settings.LibraryFolders.Count == 0)
        {
            _progressTracker.SetStage(settings.IsTargetedScan ? "No valid folders selected" : "No folders configured");
            pulseProgress();
            return settings.IsTargetedScan ? "NoFoldersSelected" : "NoFolders";
        }

        var stage = settings.RemoveMissingFilesDuringScan ? "Loading scan baseline" : "Starting scan";
        _progressTracker.SetStage(stage);
        await _scanRelinkService.InitializeAsync(settings.RemoveMissingFilesDuringScan, settings.LibraryFolders, cancellationToken);
        _logger.LogInformation(
            "Library scan started. Mode={Mode}, Workers={Workers}, BatchSize={BatchSize}, Folders={FolderCount}, RemoveMissing={RemoveMissing}",
            settings.IsTargetedScan ? "Targeted" : "Full",
            settings.WorkerCount,
            settings.BatchSize,
            settings.LibraryFolders.Count,
            settings.RemoveMissingFilesDuringScan);
        return null;
    }

    private async Task RunPipelineAsync(
        ScanRunSettings settings,
        Action<ComicLibraryItem> onSaved,
        Action<string> onRemoved,
        Action pulseProgress,
        CancellationToken cancellationToken)
    {
        var fileChannel = CreateFileChannel(settings.WorkerCount);
        var persistChannel = CreatePersistChannel(settings.WorkerCount);
        var producerTask = _scanFileProducer.ProduceAsync(settings.LibraryFolders, fileChannel.Writer, pulseProgress, cancellationToken);
        var workers = Enumerable.Range(0, settings.WorkerCount)
            .Select(_ => ProcessFilesAsync(fileChannel.Reader, persistChannel.Writer, onRemoved, pulseProgress, cancellationToken))
            .ToArray();
        var writerTask = _scanBatchPersister.PersistAsync(persistChannel.Reader, settings.BatchSize, onSaved, pulseProgress, cancellationToken);

        await producerTask;
        await Task.WhenAll(workers);
        persistChannel.Writer.TryComplete();
        await writerTask;
    }

    private async Task ProcessFilesAsync(
        ChannelReader<ScanFileWorkItem> fileReader,
        ChannelWriter<ComicFileUpsertModel> persistWriter,
        Action<string> onRemoved,
        Action pulseProgress,
        CancellationToken cancellationToken)
    {
        _progressTracker.SetStage("Processing files");
        await foreach (var workItem in fileReader.ReadAllAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _scanFileProcessor.ProcessAsync(workItem, cancellationToken);
            if (!string.IsNullOrWhiteSpace(result.RemovedPath))
            {
                onRemoved(result.RemovedPath);
            }

            if (result.UpsertModel is null)
            {
                continue;
            }

            await persistWriter.WriteAsync(result.UpsertModel, cancellationToken);
            pulseProgress();
        }
    }

    private async Task RemoveMissingFilesAsync(
        ScanRunSettings settings,
        Action<string> onRemoved,
        CancellationToken cancellationToken)
    {
        if (!settings.RemoveMissingFilesDuringScan)
        {
            return;
        }

        _progressTracker.SetStage("Removing missing files");
        var removedPaths = await _scanRelinkService.RemoveMissingFilesAsync(cancellationToken);
        foreach (var path in removedPaths)
        {
            onRemoved(path);
        }

        if (removedPaths.Count > 0)
        {
            _logger.LogInformation("Removed {RemovedCount} missing files from library.", removedPaths.Count);
        }
    }

    private static Channel<ScanFileWorkItem> CreateFileChannel(int workerCount)
    {
        return Channel.CreateBounded<ScanFileWorkItem>(new BoundedChannelOptions(4_096)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = workerCount == 1
        });
    }

    private static Channel<ComicFileUpsertModel> CreatePersistChannel(int workerCount)
    {
        return Channel.CreateBounded<ComicFileUpsertModel>(new BoundedChannelOptions(2_048)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = workerCount == 1,
            SingleReader = true
        });
    }
}
