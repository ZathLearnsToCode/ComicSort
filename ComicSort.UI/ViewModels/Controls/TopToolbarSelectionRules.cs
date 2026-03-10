using System;
using System.Collections.Generic;
using System.Linq;

namespace ComicSort.UI.ViewModels.Controls;

internal static class TopToolbarSelectionRules
{
    internal const string NotGrouped = "Not Grouped";
    internal const string ArrangeNotSorted = "Not Sorted";
    internal const string ArrangeSeries = "Series";
    internal const string ArrangePosition = "Position";
    internal const string ArrangeFilePath = "File Path";

    internal static IReadOnlyList<string> PrimaryFilters { get; } = ["Series", "Publisher", "Smart List"];
    internal static IReadOnlyList<string> SecondaryFilters { get; } = ["File Directory", "Folder", "Import Source"];
    internal static IReadOnlyList<string> SortOptions { get; } = ["Name", "Recent", "Count"];

    internal static bool IsKnownPrimaryFilter(string filterName)
    {
        return string.Equals(filterName, NotGrouped, StringComparison.Ordinal) ||
               PrimaryFilters.Contains(filterName, StringComparer.Ordinal) ||
               SecondaryFilters.Contains(filterName, StringComparer.Ordinal);
    }

    internal static bool IsKnownArrangeBy(string arrangeBy)
    {
        return arrangeBy is ArrangeNotSorted or ArrangeSeries or ArrangePosition or ArrangeFilePath;
    }

    internal static IReadOnlyList<string> ApplyPrimarySelection(
        IReadOnlyList<string> selectedFilters,
        string filterName,
        bool appendSelection)
    {
        if (!IsKnownPrimaryFilter(filterName))
        {
            return selectedFilters;
        }

        if (string.Equals(filterName, NotGrouped, StringComparison.Ordinal))
        {
            return [];
        }

        if (!appendSelection || selectedFilters.Count == 0)
        {
            return [filterName];
        }

        if (selectedFilters.Contains(filterName, StringComparer.Ordinal))
        {
            return selectedFilters.Count == 1
                ? []
                : selectedFilters.Where(x => !string.Equals(x, filterName, StringComparison.Ordinal)).ToArray();
        }

        return selectedFilters.Concat([filterName]).ToArray();
    }
}
