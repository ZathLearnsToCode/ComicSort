using ComicSort.Engine.Data;
using ComicSort.Engine.Models;
using Microsoft.VisualBasic.FileIO;
using System.IO.Compression;

namespace ComicSort.Engine.Services;

public sealed class ComicConversionService : IComicConversionService
{
    private static readonly HashSet<string> SupportedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbr",
        ".cb7",
        ".cbz"
    };

    private readonly IScanRepository _scanRepository;
    private readonly IArchiveImageService _archiveImageService;
    private readonly IThumbnailCacheService _thumbnailCacheService;
    private readonly IArchiveInspectorService _archiveInspectorService;

    public ComicConversionService(
        IScanRepository scanRepository,
        IArchiveImageService archiveImageService,
        IThumbnailCacheService thumbnailCacheService,
        IArchiveInspectorService archiveInspectorService)
    {
        _scanRepository = scanRepository;
        _archiveImageService = archiveImageService;
        _thumbnailCacheService = thumbnailCacheService;
        _archiveInspectorService = archiveInspectorService;
    }

    public async Task<CbzConversionBatchResult> ConvertToCbzAsync(
        IReadOnlyCollection<string> sourcePaths,
        CbzConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (sourcePaths.Count == 0)
        {
            return new CbzConversionBatchResult();
        }

        var normalizedDistinctPaths = sourcePaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => Path.GetFullPath(x.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<CbzConversionFileResult>(normalizedDistinctPaths.Length);
        foreach (var sourcePath in normalizedDistinctPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ConvertSingleAsync(sourcePath, options, cancellationToken));
        }

        return new CbzConversionBatchResult
        {
            Files = results
        };
    }

    private async Task<CbzConversionFileResult> ConvertSingleAsync(
        string sourcePath,
        CbzConversionOptions options,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(sourcePath);
        if (!SupportedArchiveExtensions.Contains(extension))
        {
            return new CbzConversionFileResult
            {
                SourcePath = sourcePath,
                Success = false,
                Error = "Unsupported archive type."
            };
        }

        if (string.Equals(extension, ".cbz", StringComparison.OrdinalIgnoreCase))
        {
            return new CbzConversionFileResult
            {
                SourcePath = sourcePath,
                Success = false,
                Error = "File is already a CBZ archive."
            };
        }

        if (!File.Exists(sourcePath))
        {
            return new CbzConversionFileResult
            {
                SourcePath = sourcePath,
                Success = false,
                Error = "Source file was not found."
            };
        }

        var inspection = await _archiveInspectorService.InspectAsync(sourcePath, cancellationToken);
        if (!inspection.Success)
        {
            return new CbzConversionFileResult
            {
                SourcePath = sourcePath,
                Success = false,
                Error = inspection.Error ?? "Failed to inspect source archive."
            };
        }

        if (inspection.ImageEntryPaths.Count == 0)
        {
            return new CbzConversionFileResult
            {
                SourcePath = sourcePath,
                Success = false,
                Error = "No images were found in the archive."
            };
        }

        var targetPath = GetAvailableDestinationPath(Path.ChangeExtension(sourcePath, ".cbz"));
        var tempOutputPath = Path.Combine(
            Path.GetTempPath(),
            $"comicsort-convert-{Guid.NewGuid():N}.cbz");

        try
        {
            await CreateCbzArchiveAsync(
                sourcePath,
                inspection.ImageEntryPaths,
                inspection.ComicInfoEntryPath,
                tempOutputPath,
                cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? Path.GetTempPath());
            File.Move(tempOutputPath, targetPath, false);

            var savedLibraryItem = await PersistConvertedFileAsync(
                targetPath,
                cancellationToken);

            if (options.SendOriginalToRecycleBin)
            {
                MoveFileToRecycleBin(sourcePath);
                await _scanRepository.DeleteByNormalizedPathAsync(
                    NormalizePath(sourcePath),
                    cancellationToken);
            }

            return new CbzConversionFileResult
            {
                SourcePath = sourcePath,
                DestinationPath = targetPath,
                Success = true,
                OriginalRemoved = options.SendOriginalToRecycleBin,
                SavedLibraryItem = savedLibraryItem
            };
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempOutputPath);
            return new CbzConversionFileResult
            {
                SourcePath = sourcePath,
                DestinationPath = targetPath,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<ComicLibraryItem?> PersistConvertedFileAsync(
        string targetPath,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(targetPath);
        var normalizedTargetPath = NormalizePath(info.FullName);
        var fingerprint = $"{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        var thumbnailKey = _thumbnailCacheService.ComputeKey(normalizedTargetPath, fingerprint);
        string? thumbnailPath = null;
        var hasThumbnail = false;
        var scanState = ScanState.Pending;
        string? lastError = null;

        if (_thumbnailCacheService.TryGetCachedPath(thumbnailKey, out var cachedPath))
        {
            thumbnailPath = cachedPath;
            hasThumbnail = true;
            scanState = ScanState.Ok;
        }
        else
        {
            var archiveImageResult = await _archiveImageService.TryGetFirstImageAsync(targetPath, cancellationToken);
            if (archiveImageResult.Success && archiveImageResult.ImageBytes is not null)
            {
                var thumbWriteResult = await _thumbnailCacheService.WriteThumbnailAsync(
                    thumbnailKey,
                    archiveImageResult.ImageBytes,
                    cancellationToken);
                if (thumbWriteResult.Success)
                {
                    thumbnailPath = thumbWriteResult.ThumbnailPath;
                    hasThumbnail = true;
                    scanState = ScanState.Ok;
                }
                else
                {
                    scanState = ScanState.Error;
                    lastError = thumbWriteResult.Error;
                }
            }
            else
            {
                scanState = ScanState.Error;
                lastError = archiveImageResult.Error;
            }
        }

        var upsertModel = new ComicFileUpsertModel
        {
            NormalizedPath = normalizedTargetPath,
            FileName = info.Name,
            Extension = ".cbz",
            SizeBytes = info.Length,
            CreatedUtc = info.CreationTimeUtc,
            ModifiedUtc = info.LastWriteTimeUtc,
            LastScannedUtc = DateTimeOffset.UtcNow,
            Fingerprint = fingerprint,
            ThumbnailKey = thumbnailKey,
            ThumbnailPath = thumbnailPath,
            HasThumbnail = hasThumbnail,
            ScanState = scanState,
            LastError = lastError
        };

        var saveResult = await _scanRepository.UpsertBatchAsync([upsertModel], cancellationToken);
        return saveResult.SavedItems.FirstOrDefault();
    }

    private async Task CreateCbzArchiveAsync(
        string sourcePath,
        IReadOnlyList<string> imageEntries,
        string? comicInfoEntry,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var outputStream = new FileStream(destinationPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var outputArchive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: false);

        for (var index = 0; index < imageEntries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceEntryPath = imageEntries[index];
            var extension = NormalizeImageExtension(Path.GetExtension(sourceEntryPath));
            var outputEntryPath = $"p{index + 1:00000}{extension}";
            var entryBytes = await _archiveInspectorService.ExtractEntryAsync(sourcePath, sourceEntryPath, cancellationToken);

            if (entryBytes is null || entryBytes.Length == 0)
            {
                throw new InvalidOperationException($"Failed to extract '{sourceEntryPath}' from archive.");
            }

            var outputEntry = outputArchive.CreateEntry(outputEntryPath, CompressionLevel.Optimal);
            await using var entryStream = outputEntry.Open();
            await entryStream.WriteAsync(entryBytes, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(comicInfoEntry))
        {
            var comicInfoBytes = await _archiveInspectorService.ExtractEntryAsync(sourcePath, comicInfoEntry, cancellationToken);
            if (comicInfoBytes is not null && comicInfoBytes.Length > 0)
            {
                var comicInfoOutputEntry = outputArchive.CreateEntry("ComicInfo.xml", CompressionLevel.Optimal);
                await using var comicInfoStream = comicInfoOutputEntry.Open();
                await comicInfoStream.WriteAsync(comicInfoBytes, cancellationToken);
            }
        }
    }

    private static string GetAvailableDestinationPath(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        var directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);

        for (var index = 1; index < 10000; index++)
        {
            var candidate = Path.Combine(directory, $"{baseName} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Unable to select an available destination path for CBZ output.");
    }

    private static string NormalizeImageExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".jpg";
        }

        if (string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return ".jpg";
        }

        return extension.ToLowerInvariant();
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).Trim();
    }

    private static void MoveFileToRecycleBin(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Sending files to recycle bin is only supported on Windows.");
        }

        FileSystem.DeleteFile(
            path,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin,
            UICancelOption.ThrowException);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Swallow cleanup failures.
        }
    }
}
