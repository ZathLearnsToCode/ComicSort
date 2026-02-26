using ComicSort.Engine.Models;
using System.Text;

namespace ComicSort.Engine.Services;

public sealed class SmartListSerializer : ISmartListSerializer
{
    public string Serialize(MatcherGroupNode expression)
    {
        return SerializeGroup(expression);
    }

    private static string SerializeGroup(MatcherGroupNode group)
    {
        var builder = new StringBuilder();
        if (group.Not)
        {
            builder.Append("Not ");
        }

        builder.Append(group.Mode == MatcherMode.Or ? "Match Any (" : "Match All (");

        for (var index = 0; index < group.Children.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(" ; ");
            }

            builder.Append(SerializeNode(group.Children[index]));
        }

        builder.Append(')');
        return builder.ToString();
    }

    private static string SerializeNode(IMatcherNode node)
    {
        return node switch
        {
            MatcherGroupNode group => SerializeGroup(group),
            MatcherRuleNode rule => SerializeRule(rule),
            _ => string.Empty
        };
    }

    private static string SerializeRule(MatcherRuleNode rule)
    {
        var fieldName = SmartListNodeMapper.ToFieldName(rule.Field);
        var operatorName = SmartListNodeMapper.ToOperatorName(rule.Operator);

        var builder = new StringBuilder();
        if (rule.Not)
        {
            builder.Append("Not ");
        }

        builder.Append('[');
        builder.Append(fieldName);
        builder.Append("] ");
        builder.Append(operatorName);

        if (!string.IsNullOrWhiteSpace(rule.Value1))
        {
            builder.Append(' ');
            builder.Append('"');
            builder.Append(Escape(rule.Value1));
            builder.Append('"');
        }

        if (!string.IsNullOrWhiteSpace(rule.Value2))
        {
            builder.Append(' ');
            builder.Append('"');
            builder.Append(Escape(rule.Value2));
            builder.Append('"');
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
