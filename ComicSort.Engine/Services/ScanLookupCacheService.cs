using ComicSort.Engine.Models;
using System.Collections.Concurrent;

namespace ComicSort.Engine.Services;

public sealed class ScanLookupCacheService : IScanLookupCacheService
{
    private readonly IScanRepository _scanRepository;
    private readonly ConcurrentDictionary<string, ComicFileLookup> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ScanLookupCacheService(IScanRepository scanRepository)
    {
        _scanRepository = scanRepository;
    }

    public void Reset()
    {
        _cache.Clear();
    }

    public bool TryGetCachedLookup(string normalizedPath, out ComicFileLookup lookup)
    {
        return _cache.TryGetValue(normalizedPath, out lookup!);
    }

    public void CacheLookup(ComicFileLookup lookup)
    {
        _cache[lookup.NormalizedPath] = lookup;
    }

    public void RemoveCachedLookup(string normalizedPath)
    {
        _cache.TryRemove(normalizedPath, out _);
    }

    public Task<IReadOnlyDictionary<string, ComicFileLookup>> GetByNormalizedPathsAsync(
        IReadOnlyCollection<string> normalizedPaths,
        CancellationToken cancellationToken)
    {
        return _scanRepository.GetByNormalizedPathsAsync(normalizedPaths, cancellationToken);
    }

    public ComicFileLookup ApplyUpsert(ComicFileUpsertModel model)
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

        CacheLookup(lookup);
        return lookup;
    }
}
