namespace ComicSort.Engine.Services;

internal static class ArchiveListingParser
{
    public static IReadOnlyList<string> ParseEntries(string outputText, string archivePath)
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
                TryAddEntry(entries, currentPath, currentIsFolder, normalizedArchivePath);
                currentPath = line["Path = ".Length..].Trim();
                currentIsFolder = false;
                continue;
            }

            if (line.StartsWith("Folder = ", StringComparison.Ordinal))
            {
                currentIsFolder = line.EndsWith('+');
            }
        }

        TryAddEntry(entries, currentPath, currentIsFolder, normalizedArchivePath);
        return entries;
    }

    private static void TryAddEntry(List<string> entries, string? entryPath, bool isFolder, string archivePath)
    {
        if (ShouldIncludeEntry(entryPath, isFolder, archivePath))
        {
            entries.Add(entryPath!);
        }
    }

    private static bool ShouldIncludeEntry(string? entryPath, bool isFolder, string normalizedArchivePath)
    {
        if (string.IsNullOrWhiteSpace(entryPath) || isFolder)
        {
            return false;
        }

        var normalizedEntry = NormalizePath(entryPath);
        return !string.Equals(normalizedEntry, normalizedArchivePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }
}
