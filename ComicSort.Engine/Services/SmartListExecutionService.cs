using ComicSort.Engine.Models;
using System.Globalization;

namespace ComicSort.Engine.Services;

public sealed class SmartListExecutionService : ISmartListExecutionService
{
    private const int CandidateChunkSize = 400;

    private readonly IScanRepository _scanRepository;
    private readonly ISmartListSqlCompiler _sqlCompiler;
    private readonly ISmartListEvaluator _evaluator;

    public SmartListExecutionService(
        IScanRepository scanRepository,
        ISmartListSqlCompiler sqlCompiler,
        ISmartListEvaluator evaluator)
    {
        _scanRepository = scanRepository;
        _sqlCompiler = sqlCompiler;
        _evaluator = evaluator;
    }

    public async Task<SmartListExecutionResult> ExecuteAsync(
        MatcherGroupNode expression,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return new SmartListExecutionResult();
        }

        var compiledFilter = _sqlCompiler.Compile(expression);
        var matched = new List<ComicLibraryItem>(take);

        var skip = 0;
        while (matched.Count < take)
        {
            var candidates = await _scanRepository.QueryCandidatesAsync(
                compiledFilter,
                CandidateChunkSize,
                skip,
                cancellationToken);

            if (candidates.Count == 0)
            {
                break;
            }

            foreach (var candidate in candidates)
            {
                if (!_evaluator.IsMatch(expression, candidate))
                {
                    continue;
                }

                matched.Add(ToLibraryItem(candidate));
                if (matched.Count >= take)
                {
                    break;
                }
            }

            skip += candidates.Count;
            if (candidates.Count < CandidateChunkSize)
            {
                break;
            }
        }

        var summary = compiledFilter.ResidualRequired
            ? $"Smart List loaded {matched.Count.ToString(CultureInfo.InvariantCulture)} items (hybrid filter)"
            : $"Smart List loaded {matched.Count.ToString(CultureInfo.InvariantCulture)} items";

        return new SmartListExecutionResult
        {
            Items = matched,
            LoadedCount = matched.Count,
            ResidualRequired = compiledFilter.ResidualRequired,
            Summary = summary
        };
    }

    private static ComicLibraryItem ToLibraryItem(ComicLibraryProjection candidate)
    {
        var fileTypeTag = string.IsNullOrWhiteSpace(candidate.Extension)
            ? "FILE"
            : candidate.Extension.TrimStart('.').ToUpperInvariant();

        var thumbnailReady = !string.IsNullOrWhiteSpace(candidate.ThumbnailPath) &&
                             candidate.HasThumbnail &&
                             File.Exists(candidate.ThumbnailPath);

        return new ComicLibraryItem
        {
            FilePath = candidate.FilePath,
            FileDirectory = candidate.FileDirectory,
            DisplayTitle = candidate.DisplayTitle,
            Series = candidate.Series,
            Publisher = candidate.Publisher,
            ThumbnailPath = candidate.ThumbnailPath,
            IsThumbnailReady = thumbnailReady,
            FileTypeTag = fileTypeTag,
            LastScannedUtc = candidate.LastScannedUtc
        };
    }
}
