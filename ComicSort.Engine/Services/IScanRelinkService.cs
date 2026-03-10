using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IScanRelinkService
{
    bool IsEnabled { get; }

    void Reset();

    Task InitializeAsync(
        bool enabled,
        IReadOnlyList<string> libraryFolders,
        CancellationToken cancellationToken);

    IReadOnlyDictionary<string, ComicFileLookup> GetSnapshotLookups(IReadOnlyList<ScanFileWorkItem> prefetchBatch);

    void MarkSeenPath(string normalizedPath);

    void ApplyUpsert(ComicFileLookup lookup);

    Task<ScanRelinkResult?> TryRelinkByFileNameSizeAsync(
        FileInfo fileInfo,
        string normalizedPath,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> RemoveMissingFilesAsync(CancellationToken cancellationToken);
}
