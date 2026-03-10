namespace ComicSort.Engine.Services;

public sealed class ScanPathService : IScanPathService
{
    public string NormalizePath(string path)
    {
        return Path.GetFullPath(path).Trim();
    }

    public string NormalizeDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var normalized = Path.GetFullPath(path).Trim();
            var trimmed = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(normalized);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(trimmed, trimmedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return root.EndsWith(Path.DirectorySeparatorChar) || root.EndsWith(Path.AltDirectorySeparatorChar)
                        ? root
                        : string.Concat(root, Path.DirectorySeparatorChar);
                }
            }

            return trimmed;
        }
        catch
        {
            return path.Trim();
        }
    }

    public string[] ResolveFoldersToScan(
        IReadOnlyCollection<string> configuredFolders,
        IReadOnlyCollection<string>? requestedFolders)
    {
        if (requestedFolders is null)
        {
            return configuredFolders.ToArray();
        }

        var configuredSet = configuredFolders.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (configuredSet.Count == 0)
        {
            return [];
        }

        return requestedFolders
            .Select(NormalizeDirectoryPath)
            .Where(x => !string.IsNullOrWhiteSpace(x) && configuredSet.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();
    }

    public string[] BuildScanRootPrefixes(IReadOnlyCollection<string> roots)
    {
        return roots
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => x.EndsWith(Path.DirectorySeparatorChar) || x.EndsWith(Path.AltDirectorySeparatorChar)
                ? x
                : string.Concat(x, Path.DirectorySeparatorChar))
            .OrderByDescending(x => x.Length)
            .ToArray();
    }

    public bool IsPathInRoots(string normalizedPath, IReadOnlyCollection<string> rootPrefixes)
    {
        if (rootPrefixes.Count == 0 || string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        foreach (var rootPrefix in rootPrefixes)
        {
            if (normalizedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
