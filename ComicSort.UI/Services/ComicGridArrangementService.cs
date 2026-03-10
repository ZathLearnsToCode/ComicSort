using ComicSort.Engine.Models;
using ComicSort.UI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ComicSort.UI.Services;

public sealed class ComicGridArrangementService : IComicGridArrangementService
{
    private const string NotGrouped = "Not Grouped";
    private const string ArrangeByNotSorted = "Not Sorted";
    private const string ArrangeBySeries = "Series";
    private const string ArrangeByPosition = "Position";
    private const string ArrangeByFilePath = "File Path";

    public IReadOnlyList<string> NormalizeGrouping(IReadOnlyList<string> grouping)
    {
        if (grouping.Count == 0 || grouping.Any(x => string.Equals(x, NotGrouped, StringComparison.Ordinal)))
        {
            return [NotGrouped];
        }

        var normalized = grouping.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        return normalized.Length == 0 ? [NotGrouped] : normalized;
    }

    public string NormalizeArrangement(string? arrangeBy)
    {
        return arrangeBy switch
        {
            ArrangeBySeries => ArrangeBySeries,
            ArrangeByPosition => ArrangeByPosition,
            ArrangeByFilePath => ArrangeByFilePath,
            _ => ArrangeByNotSorted
        };
    }

    public IReadOnlyList<ComicTileModel> ArrangeItems(IReadOnlyList<ComicTileModel> items, string arrangeBy)
    {
        return arrangeBy switch
        {
            ArrangeBySeries => items.OrderBy(ComicGridIssueSortHelper.GetSeriesSortValue, StringComparer.OrdinalIgnoreCase).ThenBy(ComicGridIssueSortHelper.GetIssueSortKey).ThenBy(x => x.DisplayTitle, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            ArrangeByPosition => items.OrderBy(ComicGridIssueSortHelper.GetIssueSortKey).ThenBy(ComicGridIssueSortHelper.GetSeriesSortValue, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.DisplayTitle, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            ArrangeByFilePath => items.OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            _ => items.ToArray()
        };
    }

    public IReadOnlyList<ComicGroupModel> BuildGroups(IReadOnlyList<ComicTileModel> items, IReadOnlyList<string> grouping)
    {
        if (grouping.Contains(NotGrouped, StringComparer.Ordinal))
        {
            return [];
        }

        var groups = new List<ComicGroupModel>();
        var index = new Dictionary<string, ComicGroupModel>(StringComparer.Ordinal);
        foreach (var tile in items)
        {
            AddToGroup(groups, index, tile, grouping);
        }

        return groups;
    }

    public ComicTileModel CreateTile(ComicLibraryItem item)
    {
        return new ComicTileModel
        {
            FilePath = item.FilePath,
            FileDirectory = string.IsNullOrWhiteSpace(item.FileDirectory) ? (Path.GetDirectoryName(item.FilePath) ?? string.Empty) : item.FileDirectory,
            DisplayTitle = item.DisplayTitle,
            Series = CoalesceGroupValue(item.Series, item.DisplayTitle),
            Publisher = CoalesceGroupValue(item.Publisher),
            ThumbnailPath = item.ThumbnailPath,
            ThumbnailImage = null,
            IsThumbnailReady = item.IsThumbnailReady,
            FileTypeTag = item.FileTypeTag,
            LastScannedUtc = item.LastScannedUtc
        };
    }

    public void UpdateTile(ComicTileModel tile, ComicLibraryItem item)
    {
        tile.DisplayTitle = item.DisplayTitle;
        tile.FilePath = item.FilePath;
        tile.FileDirectory = item.FileDirectory;
        tile.Series = CoalesceGroupValue(item.Series);
        tile.Publisher = CoalesceGroupValue(item.Publisher);
        tile.IsThumbnailReady = item.IsThumbnailReady;
        tile.FileTypeTag = item.FileTypeTag;
        tile.LastScannedUtc = item.LastScannedUtc;
    }

    public string CoalesceGroupValue(string? value, string? displayTitle = null)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        if (string.IsNullOrWhiteSpace(displayTitle))
        {
            return "Unspecified";
        }

        return TrimTitle(displayTitle.Trim());
    }

    private static string TrimTitle(string title)
    {
        var hashIndex = title.IndexOf('#');
        if (hashIndex > 0)
        {
            return title[..hashIndex].Trim();
        }

        var yearIndex = title.IndexOf('(');
        return yearIndex > 0 ? title[..yearIndex].Trim() : title;
    }

    private void AddToGroup(
        ICollection<ComicGroupModel> groups,
        IDictionary<string, ComicGroupModel> index,
        ComicTileModel tile,
        IReadOnlyList<string> grouping)
    {
        var values = grouping.Select(filter => ResolveGroupValue(tile, filter)).ToArray();
        var key = string.Join("||", values);
        if (!index.TryGetValue(key, out var group))
        {
            group = new ComicGroupModel { Header = $"{string.Join(" - ", values)} ({0})" };
            index[key] = group;
            groups.Add(group);
        }

        group.Items.Add(tile);
        group.Header = $"{string.Join(" - ", values)} ({group.Items.Count})";
    }

    private string ResolveGroupValue(ComicTileModel tile, string filterName)
    {
        return filterName switch
        {
            "Series" => CoalesceGroupValue(tile.Series, tile.DisplayTitle),
            "Publisher" => CoalesceGroupValue(tile.Publisher),
            "File Directory" => CoalesceGroupValue(tile.FileDirectory),
            "Folder" => CoalesceGroupValue(Path.GetFileName(tile.FileDirectory)),
            _ => "Unspecified"
        };
    }
}
