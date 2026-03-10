namespace ComicSort.Engine.Services;

public interface IScanPathService
{
    string NormalizePath(string path);

    string NormalizeDirectoryPath(string? path);

    string[] ResolveFoldersToScan(
        IReadOnlyCollection<string> configuredFolders,
        IReadOnlyCollection<string>? requestedFolders);

    string[] BuildScanRootPrefixes(IReadOnlyCollection<string> roots);

    bool IsPathInRoots(string normalizedPath, IReadOnlyCollection<string> rootPrefixes);
}
