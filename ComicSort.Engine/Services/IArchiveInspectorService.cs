using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IArchiveInspectorService
{
    Task<ArchiveInspectionResult> InspectAsync(
        string archivePath,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ExtractEntryAsync(
        string archivePath,
        string entryPath,
        CancellationToken cancellationToken = default);
}
