using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public readonly record struct ScanFileProcessResult(ComicFileUpsertModel? UpsertModel, string? RemovedPath);
