using ComicSort.Engine.Models;
using ComicSort.UI.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.Services;

public interface IComicGridFileActionService
{
    Task<CbzConversionBatchResult?> ConvertToCbzAsync(
        IReadOnlyList<ComicTileModel> targets,
        CancellationToken cancellationToken = default);

    Task<ComicGridDeleteActionResult?> DeleteFromLibraryAsync(
        IReadOnlyList<ComicTileModel> targets,
        CancellationToken cancellationToken = default);
}

public sealed class ComicGridDeleteActionResult
{
    public IReadOnlyList<string> RemovedPaths { get; init; } = [];

    public int FailedRecycleCount { get; init; }

    public string? WarningMessage { get; init; }
}
