using ComicSort.Engine.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ComicSort.Engine.Services;

public sealed class ScanFileProducer : IScanFileProducer
{
    private static readonly HashSet<string> SupportedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbr",
        ".cbz",
        ".cb7"
    };

    private const int LookupPrefetchBatchSize = 300;

    private readonly IScanPathService _scanPathService;
    private readonly IScanLookupCacheService _lookupCacheService;
    private readonly IScanRelinkService _scanRelinkService;
    private readonly IScanProgressTracker _progressTracker;
    private readonly ILogger<ScanFileProducer> _logger;

    public ScanFileProducer(
        IScanPathService scanPathService,
        IScanLookupCacheService lookupCacheService,
        IScanRelinkService scanRelinkService,
        IScanProgressTracker progressTracker,
        ILogger<ScanFileProducer> logger)
    {
        _scanPathService = scanPathService;
        _lookupCacheService = lookupCacheService;
        _scanRelinkService = scanRelinkService;
        _progressTracker = progressTracker;
        _logger = logger;
    }

    public async Task ProduceAsync(
        IReadOnlyList<string> folders,
        ChannelWriter<ScanFileWorkItem> writer,
        Action pulseProgress,
        CancellationToken cancellationToken)
    {
        _progressTracker.SetStage("Enumerating files");
        var prefetchBatch = new List<ScanFileWorkItem>(LookupPrefetchBatchSize);

        try
        {
            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await EnumerateFolderAsync(folder, prefetchBatch, writer, pulseProgress, cancellationToken);
            }

            await FlushPrefetchBatchAsync(prefetchBatch, writer, pulseProgress, cancellationToken);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task EnumerateFolderAsync(
        string folder,
        List<ScanFileWorkItem> prefetchBatch,
        ChannelWriter<ScanFileWorkItem> writer,
        Action pulseProgress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder))
        {
            _progressTracker.IncrementFailed();
            _logger.LogWarning("Configured library folder does not exist: {Folder}", folder);
            return;
        }

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsSupportedArchive(filePath))
                {
                    continue;
                }

                AddWorkItem(filePath, prefetchBatch);
                if (prefetchBatch.Count >= LookupPrefetchBatchSize)
                {
                    await FlushPrefetchBatchAsync(prefetchBatch, writer, pulseProgress, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _progressTracker.IncrementFailed();
            _logger.LogWarning(ex, "Failed to enumerate folder {Folder}", folder);
        }
    }

    private void AddWorkItem(string filePath, ICollection<ScanFileWorkItem> prefetchBatch)
    {
        prefetchBatch.Add(new ScanFileWorkItem
        {
            FilePath = filePath,
            NormalizedPath = _scanPathService.NormalizePath(filePath),
            SequenceNumber = _progressTracker.NextSequenceNumber()
        });
        _progressTracker.IncrementEnumerated();
        _progressTracker.IncrementQueued();
    }

    private async Task FlushPrefetchBatchAsync(
        List<ScanFileWorkItem> prefetchBatch,
        ChannelWriter<ScanFileWorkItem> writer,
        Action pulseProgress,
        CancellationToken cancellationToken)
    {
        if (prefetchBatch.Count == 0)
        {
            return;
        }

        var rows = await ResolveLookupRowsAsync(prefetchBatch, cancellationToken);
        await PublishBatchAsync(prefetchBatch, rows, writer, pulseProgress, cancellationToken);
        prefetchBatch.Clear();
    }

    private async Task<IReadOnlyDictionary<string, ComicFileLookup>> ResolveLookupRowsAsync(
        IReadOnlyList<ScanFileWorkItem> prefetchBatch,
        CancellationToken cancellationToken)
    {
        if (_scanRelinkService.IsEnabled)
        {
            return _scanRelinkService.GetSnapshotLookups(prefetchBatch);
        }

        var paths = prefetchBatch.Select(x => x.NormalizedPath).ToArray();
        return await _lookupCacheService.GetByNormalizedPathsAsync(paths, cancellationToken);
    }

    private async Task PublishBatchAsync(
        IReadOnlyList<ScanFileWorkItem> prefetchBatch,
        IReadOnlyDictionary<string, ComicFileLookup> rows,
        ChannelWriter<ScanFileWorkItem> writer,
        Action pulseProgress,
        CancellationToken cancellationToken)
    {
        foreach (var workItem in prefetchBatch)
        {
            _scanRelinkService.MarkSeenPath(workItem.NormalizedPath);
            if (rows.TryGetValue(workItem.NormalizedPath, out var existingLookup))
            {
                workItem.ExistingLookup = existingLookup;
                _lookupCacheService.CacheLookup(existingLookup);
            }

            await writer.WriteAsync(workItem, cancellationToken);
            _progressTracker.SetCurrentFile(workItem.FilePath);
            pulseProgress();
        }
    }

    private static bool IsSupportedArchive(string filePath)
    {
        return SupportedArchiveExtensions.Contains(Path.GetExtension(filePath));
    }
}
