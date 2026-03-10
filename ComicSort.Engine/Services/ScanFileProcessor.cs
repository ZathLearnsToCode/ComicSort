using ComicSort.Engine.Data;
using ComicSort.Engine.Models;
using Microsoft.Extensions.Logging;

namespace ComicSort.Engine.Services;

public sealed class ScanFileProcessor : IScanFileProcessor
{
    private readonly IArchiveImageService _archiveImageService;
    private readonly IComicMetadataService _comicMetadataService;
    private readonly IThumbnailCacheService _thumbnailCacheService;
    private readonly IScanLookupCacheService _lookupCacheService;
    private readonly IScanRelinkService _scanRelinkService;
    private readonly IScanProgressTracker _progressTracker;
    private readonly ILogger<ScanFileProcessor> _logger;

    public ScanFileProcessor(
        IArchiveImageService archiveImageService,
        IComicMetadataService comicMetadataService,
        IThumbnailCacheService thumbnailCacheService,
        IScanLookupCacheService lookupCacheService,
        IScanRelinkService scanRelinkService,
        IScanProgressTracker progressTracker,
        ILogger<ScanFileProcessor> logger)
    {
        _archiveImageService = archiveImageService;
        _comicMetadataService = comicMetadataService;
        _thumbnailCacheService = thumbnailCacheService;
        _lookupCacheService = lookupCacheService;
        _scanRelinkService = scanRelinkService;
        _progressTracker = progressTracker;
        _logger = logger;
    }

    public async Task<ScanFileProcessResult> ProcessAsync(ScanFileWorkItem workItem, CancellationToken cancellationToken)
    {
        ComicFileLookup? existingLookupForFailure = workItem.ExistingLookup;
        string? removedPath = null;

        try
        {
            var fileInfo = new FileInfo(workItem.FilePath);
            if (!fileInfo.Exists)
            {
                return new ScanFileProcessResult(null, removedPath);
            }

            var existing = await ResolveExistingLookupAsync(workItem, fileInfo, cancellationToken);
            existingLookupForFailure = existing.Lookup;
            removedPath = existing.RemovedPath;

            var fingerprint = BuildFingerprint(fileInfo);
            if (CanSkip(existing.Lookup, fingerprint))
            {
                _progressTracker.IncrementSkipped();
                return new ScanFileProcessResult(null, removedPath);
            }

            var upsert = await BuildUpsertModelAsync(workItem, fileInfo, fingerprint, cancellationToken);
            return new ScanFileProcessResult(upsert, removedPath);
        }
        catch (Exception ex)
        {
            _progressTracker.IncrementFailed();
            _logger.LogDebug(ex, "Failed to process archive {Path}", workItem.FilePath);
            if (existingLookupForFailure is not null)
            {
                return new ScanFileProcessResult(null, removedPath);
            }

            var failureModel = CreateFailureUpsertModel(workItem, ex.Message);
            return new ScanFileProcessResult(failureModel, removedPath);
        }
    }

    private async Task<(ComicFileLookup? Lookup, string? RemovedPath)> ResolveExistingLookupAsync(
        ScanFileWorkItem workItem,
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var existing = workItem.ExistingLookup;
        if (existing is not null)
        {
            return (existing, null);
        }

        if (_lookupCacheService.TryGetCachedLookup(workItem.NormalizedPath, out var cachedLookup))
        {
            return (cachedLookup, null);
        }

        var relinkResult = await _scanRelinkService.TryRelinkByFileNameSizeAsync(
            fileInfo,
            workItem.NormalizedPath,
            cancellationToken);
        return relinkResult is null
            ? (null, null)
            : (relinkResult.Value.Lookup, relinkResult.Value.RemovedPath);
    }

    private static bool CanSkip(ComicFileLookup? existing, string fingerprint)
    {
        return existing is not null &&
               string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal) &&
               existing.HasThumbnail &&
               !string.IsNullOrWhiteSpace(existing.ThumbnailPath) &&
               existing.HasComicInfo;
    }

    private static string BuildFingerprint(FileInfo fileInfo)
    {
        return $"{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
    }

    private async Task<ComicFileUpsertModel> BuildUpsertModelAsync(
        ScanFileWorkItem workItem,
        FileInfo fileInfo,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var metadata = await _comicMetadataService.GetMetadataAsync(fileInfo.FullName, cancellationToken);
        var thumbnailKey = _thumbnailCacheService.ComputeKey(workItem.NormalizedPath, fingerprint);
        var thumbnail = await BuildThumbnailResultAsync(fileInfo.FullName, thumbnailKey, cancellationToken);

        return new ComicFileUpsertModel
        {
            SequenceNumber = workItem.SequenceNumber,
            NormalizedPath = workItem.NormalizedPath,
            FileName = fileInfo.Name,
            Extension = fileInfo.Extension,
            SizeBytes = fileInfo.Length,
            CreatedUtc = fileInfo.CreationTimeUtc,
            ModifiedUtc = fileInfo.LastWriteTimeUtc,
            LastScannedUtc = DateTimeOffset.UtcNow,
            Fingerprint = fingerprint,
            ThumbnailKey = thumbnailKey,
            ThumbnailPath = thumbnail.Path,
            HasThumbnail = thumbnail.HasThumbnail,
            ScanState = thumbnail.State,
            LastError = thumbnail.Error,
            Metadata = metadata
        };
    }

    private async Task<(bool HasThumbnail, string? Path, ScanState State, string? Error)> BuildThumbnailResultAsync(
        string archivePath,
        string thumbnailKey,
        CancellationToken cancellationToken)
    {
        if (_thumbnailCacheService.TryGetCachedPath(thumbnailKey, out var cachedPath))
        {
            return (true, cachedPath, ScanState.Ok, null);
        }

        var imageResult = await _archiveImageService.TryGetFirstImageAsync(archivePath, cancellationToken);
        if (!imageResult.Success || imageResult.ImageBytes is null)
        {
            _progressTracker.IncrementFailed();
            return (false, null, ScanState.Error, imageResult.Error);
        }

        var writeResult = await _thumbnailCacheService.WriteThumbnailAsync(
            thumbnailKey,
            imageResult.ImageBytes,
            cancellationToken);
        if (writeResult.Success)
        {
            return (true, writeResult.ThumbnailPath, ScanState.Ok, null);
        }

        _progressTracker.IncrementFailed();
        return (false, null, ScanState.Error, writeResult.Error);
    }

    private static ComicFileUpsertModel? CreateFailureUpsertModel(ScanFileWorkItem workItem, string error)
    {
        FileInfo fileInfo;
        try
        {
            fileInfo = new FileInfo(workItem.FilePath);
        }
        catch
        {
            return null;
        }

        if (!fileInfo.Exists)
        {
            return null;
        }

        var modifiedUtc = fileInfo.LastWriteTimeUtc;
        return new ComicFileUpsertModel
        {
            SequenceNumber = workItem.SequenceNumber,
            NormalizedPath = workItem.NormalizedPath,
            FileName = fileInfo.Name,
            Extension = fileInfo.Extension,
            SizeBytes = fileInfo.Length,
            CreatedUtc = fileInfo.CreationTimeUtc,
            ModifiedUtc = modifiedUtc,
            LastScannedUtc = DateTimeOffset.UtcNow,
            Fingerprint = $"{fileInfo.Length}|{modifiedUtc.Ticks}",
            ScanState = ScanState.Error,
            LastError = error
        };
    }
}
