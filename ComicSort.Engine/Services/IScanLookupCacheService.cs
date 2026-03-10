using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IScanLookupCacheService
{
    void Reset();

    bool TryGetCachedLookup(string normalizedPath, out ComicFileLookup lookup);

    void CacheLookup(ComicFileLookup lookup);

    void RemoveCachedLookup(string normalizedPath);

    Task<IReadOnlyDictionary<string, ComicFileLookup>> GetByNormalizedPathsAsync(
        IReadOnlyCollection<string> normalizedPaths,
        CancellationToken cancellationToken);

    ComicFileLookup ApplyUpsert(ComicFileUpsertModel model);
}
