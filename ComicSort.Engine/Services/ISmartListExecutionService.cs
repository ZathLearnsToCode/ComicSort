using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface ISmartListExecutionService
{
    Task<SmartListExecutionResult> ExecuteAsync(
        MatcherGroupNode expression,
        int take,
        CancellationToken cancellationToken = default);
}
