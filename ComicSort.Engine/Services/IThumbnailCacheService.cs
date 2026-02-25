using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IThumbnailCacheService
{
    string BaseDirectory { get; }

    string ComputeKey(string normalizedPath, string fingerprint);

    bool TryGetCachedPath(string key, out string cachedPath);

    string GetExpectedPath(string key);

    Task<ThumbnailWriteResult> WriteThumbnailAsync(
        string key,
        byte[] imageBytes,
        CancellationToken cancellationToken = default);
}
