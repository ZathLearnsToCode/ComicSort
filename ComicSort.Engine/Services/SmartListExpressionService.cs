using ComicSort.Engine.Models;
using ComicSort.Engine.Settings;

namespace ComicSort.Engine.Services;

public sealed class SmartListExpressionService : ISmartListExpressionService
{
    private readonly ISmartListParser _parser;
    private readonly ISmartListSerializer _serializer;

    public SmartListExpressionService(ISmartListParser parser, ISmartListSerializer serializer)
    {
        _parser = parser;
        _serializer = serializer;
    }

    public MatcherGroupNode ResolveExpression(ComicListItem listModel)
    {
        if (listModel.Expression is not null)
        {
            var expression = SmartListNodeMapper.ToRuntimeNode(listModel.Expression);
            if (expression is MatcherGroupNode groupFromSettings)
            {
                return groupFromSettings;
            }

            if (expression is MatcherRuleNode singleRule)
            {
                var wrapped = new MatcherGroupNode
                {
                    Mode = SmartListNodeMapper.ParseMode(listModel.MatchMode)
                };
                wrapped.Children.Add(singleRule);
                return wrapped;
            }
        }

        if (!string.IsNullOrWhiteSpace(listModel.QueryText) &&
            _parser.TryParse(listModel.QueryText, out var parsedExpression, out _))
        {
            return parsedExpression;
        }

        return FromLegacyMatchers(listModel.Matchers, listModel.MatchMode);
    }

    public MatcherGroupNode FromLegacyMatchers(IReadOnlyCollection<ComicBookMatcher> matchers, string? matchMode = null)
    {
        var root = new MatcherGroupNode
        {
            Mode = SmartListNodeMapper.ParseMode(matchMode),
            Not = false
        };

        foreach (var matcher in matchers)
        {
            var value1 = matcher.MatchValueText;
            if (string.IsNullOrWhiteSpace(value1) && matcher.MatchValue.HasValue)
            {
                value1 = matcher.MatchValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var value2 = matcher.MatchValueText2;
            if (string.IsNullOrWhiteSpace(value2) && matcher.MatchValue2.HasValue)
            {
                value2 = matcher.MatchValue2.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            root.Children.Add(new MatcherRuleNode
            {
                Not = matcher.Not,
                Field = ExtractFieldFromMatcherType(matcher.MatcherType),
                Operator = ParseLegacyOperator(matcher.MatchOperator),
                Value1 = value1,
                Value2 = value2,
                ValueKind = InferLegacyValueKind(matcher.MatcherType, value1, value2)
            });
        }

        return root;
    }

    public SmartListExpressionNode ToSettingsExpression(MatcherGroupNode expression)
    {
        return SmartListNodeMapper.ToSettingsNode(expression);
    }

    public string ToQueryText(MatcherGroupNode expression)
    {
        return _serializer.Serialize(expression);
    }

    private static MatcherField ExtractFieldFromMatcherType(string matcherType)
    {
        if (string.IsNullOrWhiteSpace(matcherType))
        {
            return MatcherField.Unknown;
        }

        var token = matcherType
            .Replace("ComicBook", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Matcher", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return SmartListNodeMapper.ParseField(token);
    }

    private static MatcherOperator ParseLegacyOperator(int? op)
    {
        return op switch
        {
            1 => MatcherOperator.Is,
            2 => MatcherOperator.Contains,
            3 => MatcherOperator.ContainsAny,
            4 => MatcherOperator.ContainsAll,
            5 => MatcherOperator.StartsWith,
            6 => MatcherOperator.EndsWith,
            7 => MatcherOperator.ListContains,
            8 => MatcherOperator.Regex,
            _ => MatcherOperator.Contains
        };
    }

    private static MatcherValueKind InferLegacyValueKind(string matcherType, string? value1, string? value2)
    {
        if (matcherType.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
            matcherType.Contains("Added", StringComparison.OrdinalIgnoreCase) ||
            matcherType.Contains("Opened", StringComparison.OrdinalIgnoreCase) ||
            matcherType.Contains("Modified", StringComparison.OrdinalIgnoreCase))
        {
            return MatcherValueKind.Date;
        }

        if (matcherType.Contains("Rating", StringComparison.OrdinalIgnoreCase) ||
            matcherType.Contains("Count", StringComparison.OrdinalIgnoreCase) ||
            matcherType.Contains("Size", StringComparison.OrdinalIgnoreCase) ||
            matcherType.Contains("Year", StringComparison.OrdinalIgnoreCase))
        {
            return MatcherValueKind.Number;
        }

        if (double.TryParse(value1, out _) && (string.IsNullOrWhiteSpace(value2) || double.TryParse(value2, out _)))
        {
            return MatcherValueKind.Number;
        }

        return MatcherValueKind.String;
    }
}
