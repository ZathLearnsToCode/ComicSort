using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IComicMetadataService
{
    Task<ComicMetadata> GetMetadataAsync(string archivePath, CancellationToken cancellationToken = default);
}
