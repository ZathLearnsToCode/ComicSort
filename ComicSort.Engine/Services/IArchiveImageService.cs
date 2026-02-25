using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IArchiveImageService
{
    Task<ArchiveImageResult> TryGetFirstImageAsync(string archivePath, CancellationToken cancellationToken = default);
}
