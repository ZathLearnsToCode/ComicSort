using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public readonly record struct ScanRelinkResult(ComicFileLookup Lookup, string RemovedPath);
