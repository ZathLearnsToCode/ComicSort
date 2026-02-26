using ComicSort.Engine.Models;
using ComicSort.Engine.Settings;

namespace ComicSort.Engine.Services;

public interface ISmartListExpressionService
{
    MatcherGroupNode ResolveExpression(ComicListItem listModel);

    MatcherGroupNode FromLegacyMatchers(IReadOnlyCollection<ComicBookMatcher> matchers, string? matchMode = null);

    SmartListExpressionNode ToSettingsExpression(MatcherGroupNode expression);

    string ToQueryText(MatcherGroupNode expression);
}
