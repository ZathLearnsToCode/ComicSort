using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IComicConversionService
{
    Task<CbzConversionBatchResult> ConvertToCbzAsync(
        IReadOnlyCollection<string> sourcePaths,
        CbzConversionOptions options,
        CancellationToken cancellationToken = default);
}
