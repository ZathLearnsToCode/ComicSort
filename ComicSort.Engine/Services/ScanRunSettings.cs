using System.Collections.Generic;

namespace ComicSort.Engine.Services;

public readonly record struct ScanRunSettings(
    int WorkerCount,
    int BatchSize,
    IReadOnlyList<string> LibraryFolders,
    bool IsTargetedScan,
    bool RemoveMissingFilesDuringScan);
