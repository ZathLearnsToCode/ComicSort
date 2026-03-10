using ComicSort.UI.Models;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace ComicSort.UI.Services;

internal static class ComicGridIssueSortHelper
{
    private static readonly Regex IssueNumberRegex = new(@"#\s*(?<num>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AnyNumberRegex = new(@"(?<num>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string GetSeriesSortValue(ComicTileModel tile)
    {
        return string.IsNullOrWhiteSpace(tile.Series) ? tile.DisplayTitle : tile.Series;
    }

    public static IssueSortKey GetIssueSortKey(ComicTileModel tile)
    {
        if (TryParseIssueNumber(tile.DisplayTitle ?? string.Empty, out var issueNumber))
        {
            return new IssueSortKey(true, issueNumber);
        }

        var fileName = Path.GetFileNameWithoutExtension(tile.FilePath) ?? string.Empty;
        return TryParseIssueNumber(fileName, out issueNumber)
            ? new IssueSortKey(true, issueNumber)
            : IssueSortKey.Missing;
    }

    private static bool TryParseIssueNumber(string value, out decimal issueNumber)
    {
        issueNumber = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var issueMatch = IssueNumberRegex.Match(value);
        if (issueMatch.Success)
        {
            return decimal.TryParse(issueMatch.Groups["num"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out issueNumber);
        }

        var anyNumberMatch = AnyNumberRegex.Match(value);
        return anyNumberMatch.Success &&
               decimal.TryParse(anyNumberMatch.Groups["num"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out issueNumber);
    }
}

internal readonly record struct IssueSortKey(bool HasValue, decimal Value) : IComparable<IssueSortKey>
{
    public static IssueSortKey Missing => new(false, decimal.MaxValue);

    public int CompareTo(IssueSortKey other)
    {
        return HasValue != other.HasValue ? (HasValue ? -1 : 1) : Value.CompareTo(other.Value);
    }
}
