using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IScanRepository
{
    Task<ComicFileLookup?> GetByNormalizedPathAsync(string normalizedPath, CancellationToken cancellationToken = default);

    Task DeleteByNormalizedPathAsync(string normalizedPath, CancellationToken cancellationToken = default);

    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);

    Task<ScanBatchSaveResult> UpsertBatchAsync(
        IReadOnlyCollection<ComicFileUpsertModel> items,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComicLibraryItem>> GetLibraryItemsAsync(
        int take,
        int skip = 0,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComicLibraryProjection>> QueryCandidatesAsync(
        CompiledSqlFilter filter,
        int take,
        int skip = 0,
        CancellationToken cancellationToken = default);

    Task<int> CountCandidatesAsync(
        CompiledSqlFilter filter,
        CancellationToken cancellationToken = default);
}
