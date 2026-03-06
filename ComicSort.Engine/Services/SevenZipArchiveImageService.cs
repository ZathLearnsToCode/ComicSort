using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public sealed class SevenZipArchiveImageService : IArchiveImageService
{
    private readonly IArchiveInspectorService _archiveInspectorService;

    public SevenZipArchiveImageService(IArchiveInspectorService archiveInspectorService)
    {
        _archiveInspectorService = archiveInspectorService;
    }

    public async Task<ArchiveImageResult> TryGetFirstImageAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return new ArchiveImageResult
            {
                Success = false,
                Error = "Archive file was not found."
            };
        }

        var inspection = await _archiveInspectorService.InspectAsync(archivePath, cancellationToken);
        if (!inspection.Success)
        {
            return new ArchiveImageResult
            {
                Success = false,
                Error = inspection.Error ?? "Archive inspection failed."
            };
        }

        if (string.IsNullOrWhiteSpace(inspection.FirstImageEntryPath))
        {
            return new ArchiveImageResult
            {
                Success = false,
                Error = "No image entry found in archive."
            };
        }

        var imageBytes = await _archiveInspectorService.ExtractEntryAsync(
            archivePath,
            inspection.FirstImageEntryPath,
            cancellationToken);

        if (imageBytes is null || imageBytes.Length == 0)
        {
            return new ArchiveImageResult
            {
                Success = false,
                Error = "Unable to extract first image from archive."
            };
        }

        return new ArchiveImageResult
        {
            Success = true,
            ImageBytes = imageBytes
        };
    }
}
