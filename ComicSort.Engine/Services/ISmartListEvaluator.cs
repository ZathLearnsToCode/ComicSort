using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface ISmartListEvaluator
{
    bool IsMatch(MatcherGroupNode expression, ComicLibraryProjection candidate);
}
