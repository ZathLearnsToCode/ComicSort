using ComicSort.Engine.Data;
using ComicSort.Engine.Models;
using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;
using System.IO.Compression;

namespace ComicSort.Engine.Services;

public sealed class ComicConversionService : IComicConversionService
{
    private static readonly string[] SupportedImageExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif",
        ".bmp"
    ];

    private static readonly HashSet<string> SupportedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbr",
        ".cb7",
        ".cbz"
    };

    private readonly IScanRepository _scanRepository;
    private readonly IArchiveImageService _archiveImageService;
    private readonly IThumbnailCacheService _thumbnailCacheService;

    public ComicConversionService(
        IScanRepository scanRepository,
        IArchiveImageService archiveImageService,
        IThumbnailCacheService thumbnailCacheService)
    {
        _scanRepository = scanRepository;
        _archiveImageService = archiveImageService;
        _thumbnailCacheService = thumbnailCacheService;
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

        var sevenZipPath = ResolveSevenZipPath();
        if (string.IsNullOrWhiteSpace(sevenZipPath))
        {
            return new CbzConversionFileResult
            {
                SourcePath = sourcePath,
                Success = false,
                Error = "7z executable was not found."
            };
        }

        var listResult = await ExecuteTextCommandAsync(
            sevenZipPath,
            ["l", "-slt", "-ba", sourcePath],
            cancellationToken);

        if (listResult.ExitCode != 0)
        {
            return new CbzConversionFileResult
            {
                SourcePath = sourcePath,
                Success = false,
                Error = $"Failed to read archive: {listResult.ErrorText}"
            };
        }

        var imageEntries = FindImageEntries(listResult.OutputText, sourcePath);
        if (imageEntries.Count == 0)
        {
            return new CbzConversionFileResult
            {
                SourcePath = sourcePath,
                Success = false,
                Error = "No images were found in the archive."
            };
        }
        var comicInfoEntry = FindComicInfoEntry(listResult.OutputText, sourcePath);

        var targetPath = GetAvailableDestinationPath(Path.ChangeExtension(sourcePath, ".cbz"));
        var tempOutputPath = Path.Combine(
            Path.GetTempPath(),
            $"comicsort-convert-{Guid.NewGuid():N}.cbz");

        try
        {
            await CreateCbzArchiveAsync(
                sourcePath,
                imageEntries,
                comicInfoEntry,
                tempOutputPath,
                sevenZipPath,
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

    private static async Task CreateCbzArchiveAsync(
        string sourcePath,
        IReadOnlyList<string> imageEntries,
        string? comicInfoEntry,
        string destinationPath,
        string sevenZipPath,
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
            var extractResult = await ExecuteBinaryCommandAsync(
                sevenZipPath,
                ["e", "-so", "-y", sourcePath, sourceEntryPath],
                cancellationToken);

            if (extractResult.ExitCode != 0 || extractResult.OutputBytes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Failed to extract '{sourceEntryPath}' from archive: {extractResult.ErrorText}");
            }

            var outputEntry = outputArchive.CreateEntry(outputEntryPath, CompressionLevel.Optimal);
            await using var entryStream = outputEntry.Open();
            await entryStream.WriteAsync(extractResult.OutputBytes, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(comicInfoEntry))
        {
            var comicInfoResult = await ExecuteBinaryCommandAsync(
                sevenZipPath,
                ["e", "-so", "-y", sourcePath, comicInfoEntry],
                cancellationToken);

            if (comicInfoResult.ExitCode == 0 && comicInfoResult.OutputBytes.Length > 0)
            {
                var comicInfoOutputEntry = outputArchive.CreateEntry("ComicInfo.xml", CompressionLevel.Optimal);
                await using var comicInfoStream = comicInfoOutputEntry.Open();
                await comicInfoStream.WriteAsync(comicInfoResult.OutputBytes, cancellationToken);
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

    private static IReadOnlyList<string> FindImageEntries(string outputText, string archivePath)
    {
        var entries = new List<string>();
        string? currentPath = null;
        var currentIsFolder = false;
        var normalizedArchivePath = Path.GetFullPath(archivePath);

        foreach (var rawLine in outputText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("Path = ", StringComparison.Ordinal))
            {
                if (ShouldUseEntry(currentPath, currentIsFolder, normalizedArchivePath))
                {
                    entries.Add(currentPath!);
                }

                currentPath = line["Path = ".Length..].Trim();
                currentIsFolder = false;
                continue;
            }

            if (line.StartsWith("Folder = ", StringComparison.Ordinal))
            {
                currentIsFolder = line.EndsWith('+');
            }
        }

        if (ShouldUseEntry(currentPath, currentIsFolder, normalizedArchivePath))
        {
            entries.Add(currentPath!);
        }

        return entries;
    }

    private static string? FindComicInfoEntry(string outputText, string archivePath)
    {
        string? currentPath = null;
        var currentIsFolder = false;
        var normalizedArchivePath = Path.GetFullPath(archivePath);

        foreach (var rawLine in outputText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("Path = ", StringComparison.Ordinal))
            {
                if (IsComicInfoEntry(currentPath, currentIsFolder, normalizedArchivePath))
                {
                    return currentPath;
                }

                currentPath = line["Path = ".Length..].Trim();
                currentIsFolder = false;
                continue;
            }

            if (line.StartsWith("Folder = ", StringComparison.Ordinal))
            {
                currentIsFolder = line.EndsWith('+');
            }
        }

        return IsComicInfoEntry(currentPath, currentIsFolder, normalizedArchivePath)
            ? currentPath
            : null;
    }

    private static bool ShouldUseEntry(string? entryPath, bool isFolder, string normalizedArchivePath)
    {
        if (string.IsNullOrWhiteSpace(entryPath) || isFolder)
        {
            return false;
        }

        if (Path.IsPathRooted(entryPath))
        {
            var normalizedEntry = Path.GetFullPath(entryPath);
            if (string.Equals(normalizedEntry, normalizedArchivePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var extension = Path.GetExtension(entryPath);
        return SupportedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsComicInfoEntry(string? entryPath, bool isFolder, string normalizedArchivePath)
    {
        if (string.IsNullOrWhiteSpace(entryPath) || isFolder)
        {
            return false;
        }

        if (Path.IsPathRooted(entryPath))
        {
            var normalizedEntry = Path.GetFullPath(entryPath);
            if (string.Equals(normalizedEntry, normalizedArchivePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return string.Equals(
            Path.GetFileName(entryPath),
            "ComicInfo.xml",
            StringComparison.OrdinalIgnoreCase);
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

    private static string? ResolveSevenZipPath()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Tools", "7zip", "7z.exe");
        if (File.Exists(bundledPath))
        {
            return bundledPath;
        }

        return "7z";
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

    private static async Task<(int ExitCode, string OutputText, string ErrorText)> ExecuteTextCommandAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = BuildProcess(fileName, arguments);
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var outputText = await outputTask;
            var errorText = await errorTask;

            return (process.ExitCode, outputText, errorText);
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }

    private static async Task<(int ExitCode, byte[] OutputBytes, string ErrorText)> ExecuteBinaryCommandAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = BuildProcess(fileName, arguments);
            process.Start();

            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await using var outputStream = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var errorText = await errorTask;

            return (process.ExitCode, outputStream.ToArray(), errorText);
        }
        catch (Exception ex)
        {
            return (-1, [], ex.Message);
        }
    }

    private static Process BuildProcess(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process
        {
            StartInfo = startInfo
        };
    }
}
