using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IScanPipelineCoordinator
{
    Task<string> RunAsync(
        IReadOnlyCollection<string>? requestedFolders,
        Action<ComicLibraryItem> onSaved,
        Action<string> onRemoved,
        Action pulseProgress,
        CancellationToken cancellationToken);
}
