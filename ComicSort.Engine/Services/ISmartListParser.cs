using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface ISmartListParser
{
    bool TryParse(string? queryText, out MatcherGroupNode expression, out string? error);
}
