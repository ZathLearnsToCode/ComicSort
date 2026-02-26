using System.Collections.Generic;

namespace ComicSort.UI.Models.Dialogs;

public sealed class SmartListEditorResult
{
    public string Name { get; init; } = "New Smart List";

    public string Notes { get; init; } = string.Empty;

    public string MatchMode { get; init; } = "All";

    public string Scope { get; init; } = "Library";

    public bool ShowInQuickOpen { get; init; }

    public bool LimitToBooksEnabled { get; init; }

    public int LimitBookCount { get; init; } = 25;

    public IReadOnlyList<SmartListRuleResult> Rules { get; init; } = [];
}

public sealed class SmartListRuleResult
{
    public bool IsGroupHeader { get; init; }

    public bool IsNegated { get; init; }

    public string MatchMode { get; init; } = "All";

    public string Field { get; init; } = "All";

    public string Operator { get; init; } = "contains";

    public string Value { get; init; } = string.Empty;
}
