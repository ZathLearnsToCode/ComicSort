using ComicSort.Engine.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ComicSort.Engine.Services;

public sealed class SmartListEvaluator : ISmartListEvaluator
{
    public bool IsMatch(MatcherGroupNode expression, ComicLibraryProjection candidate)
    {
        return EvaluateGroup(expression, candidate);
    }

    private static bool EvaluateNode(IMatcherNode node, ComicLibraryProjection candidate)
    {
        return node switch
        {
            MatcherGroupNode group => EvaluateGroup(group, candidate),
            MatcherRuleNode rule => EvaluateRule(rule, candidate),
            _ => true
        };
    }

    private static bool EvaluateGroup(MatcherGroupNode group, ComicLibraryProjection candidate)
    {
        var value = group.Mode == MatcherMode.Or
            ? group.Children.Any(child => EvaluateNode(child, candidate))
            : group.Children.All(child => EvaluateNode(child, candidate));

        return group.Not ? !value : value;
    }

    private static bool EvaluateRule(MatcherRuleNode rule, ComicLibraryProjection candidate)
    {
        var value = EvaluateRuleCore(rule, candidate);
        return rule.Not ? !value : value;
    }

    private static bool EvaluateRuleCore(MatcherRuleNode rule, ComicLibraryProjection candidate)
    {
        if (TryEvaluateNumberRule(rule, candidate, out var numericResult))
        {
            return numericResult;
        }

        if (TryEvaluateDateRule(rule, candidate, out var dateResult))
        {
            return dateResult;
        }

        if (TryEvaluateBooleanRule(rule, candidate, out var boolResult))
        {
            return boolResult;
        }

        var target = GetStringField(rule.Field, candidate);
        return EvaluateString(target, rule.Operator, rule.Value1, rule.Value2);
    }

    private static bool TryEvaluateNumberRule(MatcherRuleNode rule, ComicLibraryProjection candidate, out bool result)
    {
        result = false;

        if (rule.ValueKind != MatcherValueKind.Number &&
            rule.Field is not MatcherField.SizeBytes and not MatcherField.Year)
        {
            return false;
        }

        if (!TryGetNumericField(rule.Field, candidate, out var fieldValue))
        {
            return true;
        }

        if (!double.TryParse(rule.Value1, NumberStyles.Float, CultureInfo.InvariantCulture, out var value1))
        {
            return true;
        }

        var value2Parsed = double.TryParse(rule.Value2, NumberStyles.Float, CultureInfo.InvariantCulture, out var value2);
        result = rule.Operator switch
        {
            MatcherOperator.Is => fieldValue.Equals(value1),
            MatcherOperator.GreaterThan => fieldValue > value1,
            MatcherOperator.LessThan => fieldValue < value1,
            MatcherOperator.Range => value2Parsed && fieldValue >= value1 && fieldValue <= value2,
            _ => false
        };

        return true;
    }

    private static bool TryEvaluateDateRule(MatcherRuleNode rule, ComicLibraryProjection candidate, out bool result)
    {
        result = false;

        if (rule.ValueKind != MatcherValueKind.Date &&
            rule.Field is not MatcherField.Added and not MatcherField.Modified and not MatcherField.LastScanned)
        {
            return false;
        }

        if (!TryGetDateField(rule.Field, candidate, out var fieldValue))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(rule.Value1, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value1))
        {
            return true;
        }

        var hasValue2 = DateTimeOffset.TryParse(rule.Value2, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value2);
        result = rule.Operator switch
        {
            MatcherOperator.Is => fieldValue.UtcDateTime.Date == value1.UtcDateTime.Date,
            MatcherOperator.GreaterThan => fieldValue > value1,
            MatcherOperator.LessThan => fieldValue < value1,
            MatcherOperator.Range => hasValue2 && fieldValue >= value1 && fieldValue <= value2,
            _ => false
        };

        return true;
    }

    private static bool TryEvaluateBooleanRule(MatcherRuleNode rule, ComicLibraryProjection candidate, out bool result)
    {
        result = false;
        if (rule.Operator is not (MatcherOperator.IsYes or MatcherOperator.IsNo or MatcherOperator.IsUnknown))
        {
            return false;
        }

        var hasThumbnail = candidate.HasThumbnail;
        result = rule.Operator switch
        {
            MatcherOperator.IsYes => hasThumbnail,
            MatcherOperator.IsNo => !hasThumbnail,
            MatcherOperator.IsUnknown => false,
            _ => false
        };
        return true;
    }

    private static bool EvaluateString(string source, MatcherOperator op, string? value1, string? value2)
    {
        var input = source ?? string.Empty;
        var v1 = value1 ?? string.Empty;
        var v2 = value2 ?? string.Empty;
        var comparison = StringComparison.OrdinalIgnoreCase;

        return op switch
        {
            MatcherOperator.Is => string.Equals(input, v1, comparison),
            MatcherOperator.Contains => input.IndexOf(v1, comparison) >= 0,
            MatcherOperator.StartsWith => input.StartsWith(v1, comparison),
            MatcherOperator.EndsWith => input.EndsWith(v1, comparison),
            MatcherOperator.ContainsAny => SplitValues(v1).Any(value => input.IndexOf(value, comparison) >= 0),
            MatcherOperator.ContainsAll => SplitValues(v1).All(value => input.IndexOf(value, comparison) >= 0),
            MatcherOperator.ListContains => SplitValues(input).Any(value => string.Equals(value, v1, comparison)),
            MatcherOperator.Regex => EvaluateRegex(input, v1),
            MatcherOperator.Range => string.Compare(input, v1, comparison) >= 0 &&
                                     string.Compare(input, v2, comparison) <= 0,
            _ => false
        };
    }

    private static bool EvaluateRegex(string input, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
        catch
        {
            return false;
        }
    }

    private static string GetStringField(MatcherField field, ComicLibraryProjection candidate)
    {
        return field switch
        {
            MatcherField.Series => CoalesceSeries(candidate.Series, candidate.DisplayTitle),
            MatcherField.Publisher => string.IsNullOrWhiteSpace(candidate.Publisher) ? "Unspecified" : candidate.Publisher.Trim(),
            MatcherField.FileDirectory => candidate.FileDirectory,
            MatcherField.FilePath => candidate.FilePath,
            MatcherField.Title => candidate.DisplayTitle,
            MatcherField.FileFormat => candidate.Extension.TrimStart('.').ToUpperInvariant(),
            MatcherField.Year => ExtractYear(candidate.DisplayTitle)?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            _ => string.Empty
        };
    }

    private static bool TryGetNumericField(MatcherField field, ComicLibraryProjection candidate, out double value)
    {
        value = field switch
        {
            MatcherField.SizeBytes => candidate.SizeBytes,
            MatcherField.Year => ExtractYear(candidate.DisplayTitle) ?? 0,
            _ => 0
        };

        return field is MatcherField.SizeBytes or MatcherField.Year;
    }

    private static bool TryGetDateField(MatcherField field, ComicLibraryProjection candidate, out DateTimeOffset value)
    {
        value = field switch
        {
            MatcherField.Added => candidate.CreatedUtc,
            MatcherField.Modified => candidate.ModifiedUtc,
            MatcherField.LastScanned => candidate.LastScannedUtc,
            _ => default
        };

        return field is MatcherField.Added or MatcherField.Modified or MatcherField.LastScanned;
    }

    private static IEnumerable<string> SplitValues(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var source = input.Trim();
        var values = source.Contains(',') || source.Contains(';')
            ? source.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
            : source.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return values
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string CoalesceSeries(string? series, string displayTitle)
    {
        if (!string.IsNullOrWhiteSpace(series))
        {
            return series.Trim();
        }

        var title = displayTitle.Trim();
        var hashIndex = title.IndexOf('#');
        if (hashIndex > 0)
        {
            return title[..hashIndex].Trim();
        }

        var yearIndex = title.IndexOf('(');
        if (yearIndex > 0)
        {
            return title[..yearIndex].Trim();
        }

        return title;
    }

    private static int? ExtractYear(string displayTitle)
    {
        var match = Regex.Match(displayTitle, @"\((\d{4})\)");
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var parsed)
            ? parsed
            : null;
    }
}
