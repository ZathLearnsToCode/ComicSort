using ComicSort.Engine.Models;
using System.Threading.Channels;

namespace ComicSort.Engine.Services;

public sealed class ScanBatchPersister : IScanBatchPersister
{
    private readonly IScanRepository _scanRepository;
    private readonly IScanLookupCacheService _lookupCacheService;
    private readonly IScanRelinkService _scanRelinkService;
    private readonly IScanProgressTracker _progressTracker;

    public ScanBatchPersister(
        IScanRepository scanRepository,
        IScanLookupCacheService lookupCacheService,
        IScanRelinkService scanRelinkService,
        IScanProgressTracker progressTracker)
    {
        _scanRepository = scanRepository;
        _lookupCacheService = lookupCacheService;
        _scanRelinkService = scanRelinkService;
        _progressTracker = progressTracker;
    }

    public async Task PersistAsync(
        ChannelReader<ComicFileUpsertModel> reader,
        int batchSize,
        Action<ComicLibraryItem> onSaved,
        Action pulseProgress,
        CancellationToken cancellationToken)
    {
        _progressTracker.SetStage("Persisting");
        var batch = new List<ComicFileUpsertModel>(batchSize);

        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            batch.Add(item);
            if (batch.Count < batchSize)
            {
                continue;
            }

            await SaveBatchAsync(batch, onSaved, pulseProgress, cancellationToken);
        }

        await SaveBatchAsync(batch, onSaved, pulseProgress, cancellationToken);
    }

    private async Task SaveBatchAsync(
        List<ComicFileUpsertModel> batch,
        Action<ComicLibraryItem> onSaved,
        Action pulseProgress,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var snapshot = batch.ToArray();
        batch.Clear();
        var result = await _scanRepository.UpsertBatchAsync(snapshot, cancellationToken);
        _progressTracker.IncrementInserted(result.Inserted);
        _progressTracker.IncrementUpdated(result.Updated);

        foreach (var model in snapshot)
        {
            var lookup = _lookupCacheService.ApplyUpsert(model);
            _scanRelinkService.ApplyUpsert(lookup);
        }

        foreach (var savedItem in result.SavedItems.OrderBy(x => x.SequenceNumber))
        {
            onSaved(savedItem);
        }

        pulseProgress();
    }
}
