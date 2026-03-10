using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public sealed class ArchiveInspectorService : IArchiveInspectorService
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
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
        if (!IsValidArchivePath(archivePath))
        {
            return CreateNotFoundResult(archivePath);
        }

        var listResult = await ListArchiveEntriesAsync(archivePath, cancellationToken);
        if (!TryGetInspectionError(listResult, out var error))
        {
            return BuildInspectionResult(archivePath, listResult.OutputText);
        }

        return CreateFailureResult(archivePath, error);
    }

    public async Task<byte[]?> ExtractEntryAsync(
        string archivePath,
        string entryPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidArchivePath(archivePath) || string.IsNullOrWhiteSpace(entryPath))
        {
            return null;
        }

        var extractResult = await _processRunner.RunBinaryAsync(
            ResolveSevenZipPath(),
            ["e", "-so", "-y", archivePath, entryPath],
            timeoutMs: 120_000,
            cancellationToken);

        return extractResult.ExitCode == 0 && extractResult.OutputBytes.Length > 0
            ? extractResult.OutputBytes
            : null;
    }

    private async Task<ProcessRunTextResult> ListArchiveEntriesAsync(string archivePath, CancellationToken cancellationToken)
    {
        return await _processRunner.RunTextAsync(
            ResolveSevenZipPath(),
            ["l", "-slt", "-ba", archivePath],
            timeoutMs: 120_000,
            cancellationToken);
    }

    private static ArchiveInspectionResult BuildInspectionResult(string archivePath, string outputText)
    {
        var parsedEntries = ArchiveListingParser.ParseEntries(outputText, archivePath);
        var imageEntries = parsedEntries.Where(IsImageEntry).ToArray();
        return new ArchiveInspectionResult
        {
            ArchivePath = archivePath,
            Success = true,
            FirstImageEntryPath = imageEntries.FirstOrDefault(),
            ComicInfoEntryPath = parsedEntries.FirstOrDefault(IsComicInfoEntry),
            ImageEntryPaths = imageEntries
        };
    }

    private static bool TryGetInspectionError(ProcessRunTextResult result, out string error)
    {
        if (result.ExitCode == 0)
        {
            error = string.Empty;
            return false;
        }

        error = result.TimedOut
            ? "Archive inspection timed out."
            : $"Archive listing failed: {result.ErrorText}";
        return true;
    }

    private static ArchiveInspectionResult CreateNotFoundResult(string archivePath)
    {
        return new ArchiveInspectionResult
        {
            ArchivePath = archivePath ?? string.Empty,
            Success = false,
            Error = "Archive file was not found."
        };
    }

    private static ArchiveInspectionResult CreateFailureResult(string archivePath, string error)
    {
        return new ArchiveInspectionResult
        {
            ArchivePath = archivePath,
            Success = false,
            Error = error
        };
    }

    private static bool IsValidArchivePath(string? archivePath)
    {
        return !string.IsNullOrWhiteSpace(archivePath) && File.Exists(archivePath);
    }

    private static string ResolveSevenZipPath()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Tools", "7zip", "7z.exe");
        return File.Exists(bundledPath) ? bundledPath : "7z";
    }

    private static bool IsImageEntry(string entryPath)
    {
        return SupportedImageExtensions.Contains(Path.GetExtension(entryPath));
    }

    private static bool IsComicInfoEntry(string entryPath)
    {
        return string.Equals(Path.GetFileName(entryPath), "ComicInfo.xml", StringComparison.OrdinalIgnoreCase);
    }
}
