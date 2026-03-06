using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public sealed class ArchiveInspectorService : IArchiveInspectorService
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif",
        ".bmp"
    };

    private readonly IProcessRunner _processRunner;

    public ArchiveInspectorService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<ArchiveInspectionResult> InspectAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return new ArchiveInspectionResult
            {
                ArchivePath = archivePath ?? string.Empty,
                Success = false,
                Error = "Archive file was not found."
            };
        }

        var sevenZipPath = ResolveSevenZipPath();
        var listResult = await _processRunner.RunTextAsync(
            sevenZipPath,
            ["l", "-slt", "-ba", archivePath],
            timeoutMs: 120_000,
            cancellationToken);

        if (listResult.ExitCode != 0)
        {
            return new ArchiveInspectionResult
            {
                ArchivePath = archivePath,
                Success = false,
                Error = listResult.TimedOut
                    ? "Archive inspection timed out."
                    : $"Archive listing failed: {listResult.ErrorText}"
            };
        }

        var parsedEntries = ParseEntries(listResult.OutputText, archivePath);
        var imageEntries = parsedEntries
            .Where(IsImageEntry)
            .ToArray();

        var firstImageEntry = imageEntries.FirstOrDefault();
        var comicInfoEntry = parsedEntries.FirstOrDefault(IsComicInfoEntry);

        return new ArchiveInspectionResult
        {
            ArchivePath = archivePath,
            Success = true,
            FirstImageEntryPath = firstImageEntry,
            ComicInfoEntryPath = comicInfoEntry,
            ImageEntryPaths = imageEntries
        };
    }

    public async Task<byte[]?> ExtractEntryAsync(
        string archivePath,
        string entryPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) ||
            string.IsNullOrWhiteSpace(entryPath) ||
            !File.Exists(archivePath))
        {
            return null;
        }

        var sevenZipPath = ResolveSevenZipPath();
        var extractResult = await _processRunner.RunBinaryAsync(
            sevenZipPath,
            ["e", "-so", "-y", archivePath, entryPath],
            timeoutMs: 120_000,
            cancellationToken);

        if (extractResult.ExitCode != 0 || extractResult.OutputBytes.Length == 0)
        {
            return null;
        }

        return extractResult.OutputBytes;
    }

    private static string ResolveSevenZipPath()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Tools", "7zip", "7z.exe");
        return File.Exists(bundledPath) ? bundledPath : "7z";
    }

    private static IReadOnlyList<string> ParseEntries(string outputText, string archivePath)
    {
        var entries = new List<string>();
        string? currentPath = null;
        var currentIsFolder = false;
        var normalizedArchivePath = NormalizePath(archivePath);

        foreach (var rawLine in outputText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("Path = ", StringComparison.Ordinal))
            {
                if (ShouldIncludeEntry(currentPath, currentIsFolder, normalizedArchivePath))
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

        if (ShouldIncludeEntry(currentPath, currentIsFolder, normalizedArchivePath))
        {
            entries.Add(currentPath!);
        }

        return entries;
    }

    private static bool ShouldIncludeEntry(string? entryPath, bool isFolder, string normalizedArchivePath)
    {
        if (string.IsNullOrWhiteSpace(entryPath) || isFolder)
        {
            return false;
        }

        var normalizedEntry = NormalizePath(entryPath);
        if (string.Equals(normalizedEntry, normalizedArchivePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsImageEntry(string entryPath)
    {
        var extension = Path.GetExtension(entryPath);
        return SupportedImageExtensions.Contains(extension);
    }

    private static bool IsComicInfoEntry(string entryPath)
    {
        return string.Equals(Path.GetFileName(entryPath), "ComicInfo.xml", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
