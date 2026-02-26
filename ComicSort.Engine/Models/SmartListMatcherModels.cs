using ComicSort.Engine.Settings;

namespace ComicSort.Engine.Models;

public enum MatcherMode
{
    And = 0,
    Or = 1
}

public enum MatcherValueKind
{
    Unknown = 0,
    String = 1,
    Number = 2,
    Date = 3,
    Boolean = 4
}

public enum MatcherField
{
    Unknown = 0,
    Series = 1,
    Publisher = 2,
    FileDirectory = 3,
    FilePath = 4,
    Title = 5,
    FileFormat = 6,
    SizeBytes = 7,
    Added = 8,
    Modified = 9,
    LastScanned = 10,
    Year = 11
}

public enum MatcherOperator
{
    Is = 0,
    Contains = 1,
    ContainsAny = 2,
    ContainsAll = 3,
    StartsWith = 4,
    EndsWith = 5,
    ListContains = 6,
    Regex = 7,
    GreaterThan = 8,
    LessThan = 9,
    Range = 10,
    IsYes = 11,
    IsNo = 12,
    IsUnknown = 13
}

public interface IMatcherNode
{
    bool Not { get; set; }
}

public sealed class MatcherGroupNode : IMatcherNode
{
    public MatcherMode Mode { get; set; } = MatcherMode.And;

    public bool Not { get; set; }

    public List<IMatcherNode> Children { get; } = [];
}

public sealed class MatcherRuleNode : IMatcherNode
{
    public MatcherField Field { get; set; } = MatcherField.Unknown;

    public MatcherOperator Operator { get; set; } = MatcherOperator.Contains;

    public string? Value1 { get; set; }

    public string? Value2 { get; set; }

    public MatcherValueKind ValueKind { get; set; } = MatcherValueKind.Unknown;

    public bool Not { get; set; }
}

public static class SmartListNodeMapper
{
    public static SmartListExpressionNode ToSettingsNode(IMatcherNode node)
    {
        return node switch
        {
            MatcherGroupNode group => new SmartListExpressionNode
            {
                NodeType = "Group",
                Not = group.Not,
                MatchMode = group.Mode == MatcherMode.Or ? "Any" : "All",
                Children = group.Children.Select(ToSettingsNode).ToList()
            },
            MatcherRuleNode rule => new SmartListExpressionNode
            {
                NodeType = "Rule",
                Not = rule.Not,
                Field = ToFieldName(rule.Field),
                Operator = ToOperatorName(rule.Operator),
                Value1 = rule.Value1,
                Value2 = rule.Value2,
                ValueKind = ToValueKindName(rule.ValueKind)
            },
            _ => new SmartListExpressionNode
            {
                NodeType = "Group",
                MatchMode = "All"
            }
        };
    }

    public static IMatcherNode ToRuntimeNode(SmartListExpressionNode node)
    {
        if (string.Equals(node.NodeType, "Rule", StringComparison.OrdinalIgnoreCase))
        {
            return new MatcherRuleNode
            {
                Not = node.Not,
                Field = ParseField(node.Field),
                Operator = ParseOperator(node.Operator),
                Value1 = node.Value1,
                Value2 = node.Value2,
                ValueKind = ParseValueKind(node.ValueKind)
            };
        }

        var group = new MatcherGroupNode
        {
            Not = node.Not,
            Mode = ParseMode(node.MatchMode)
        };

        foreach (var child in node.Children)
        {
            group.Children.Add(ToRuntimeNode(child));
        }

        return group;
    }

    public static MatcherField ParseField(string? value)
    {
        var normalized = NormalizeToken(value);
        return normalized switch
        {
            "series" => MatcherField.Series,
            "publisher" => MatcherField.Publisher,
            "filedirectory" => MatcherField.FileDirectory,
            "filepath" => MatcherField.FilePath,
            "file" => MatcherField.FilePath,
            "title" => MatcherField.Title,
            "fileformat" => MatcherField.FileFormat,
            "format" => MatcherField.FileFormat,
            "sizebytes" => MatcherField.SizeBytes,
            "filesize" => MatcherField.SizeBytes,
            "added" => MatcherField.Added,
            "filecreated" => MatcherField.Added,
            "modified" => MatcherField.Modified,
            "filemodified" => MatcherField.Modified,
            "lastscanned" => MatcherField.LastScanned,
            "year" => MatcherField.Year,
            _ => MatcherField.Unknown
        };
    }

    public static MatcherOperator ParseOperator(string? value)
    {
        var normalized = NormalizeToken(value);
        return normalized switch
        {
            "is" => MatcherOperator.Is,
            "contains" => MatcherOperator.Contains,
            "containsanyof" => MatcherOperator.ContainsAny,
            "containsallof" => MatcherOperator.ContainsAll,
            "startswith" => MatcherOperator.StartsWith,
            "endswith" => MatcherOperator.EndsWith,
            "listcontains" => MatcherOperator.ListContains,
            "regularexpression" => MatcherOperator.Regex,
            "isgreater" => MatcherOperator.GreaterThan,
            "greaterthan" => MatcherOperator.GreaterThan,
            "issmaller" => MatcherOperator.LessThan,
            "lessthan" => MatcherOperator.LessThan,
            "isintherange" => MatcherOperator.Range,
            "isinrange" => MatcherOperator.Range,
            "isyes" => MatcherOperator.IsYes,
            "isno" => MatcherOperator.IsNo,
            "isunknown" => MatcherOperator.IsUnknown,
            _ => MatcherOperator.Contains
        };
    }

    public static MatcherMode ParseMode(string? value)
    {
        return string.Equals(value?.Trim(), "Any", StringComparison.OrdinalIgnoreCase)
            ? MatcherMode.Or
            : MatcherMode.And;
    }

    public static MatcherValueKind ParseValueKind(string? value)
    {
        var normalized = NormalizeToken(value);
        return normalized switch
        {
            "string" => MatcherValueKind.String,
            "number" => MatcherValueKind.Number,
            "date" => MatcherValueKind.Date,
            "boolean" => MatcherValueKind.Boolean,
            _ => MatcherValueKind.Unknown
        };
    }

    public static string ToFieldName(MatcherField field)
    {
        return field switch
        {
            MatcherField.Series => "Series",
            MatcherField.Publisher => "Publisher",
            MatcherField.FileDirectory => "File Directory",
            MatcherField.FilePath => "File Path",
            MatcherField.Title => "Title",
            MatcherField.FileFormat => "File Format",
            MatcherField.SizeBytes => "Size Bytes",
            MatcherField.Added => "Added",
            MatcherField.Modified => "File Modified",
            MatcherField.LastScanned => "Last Scanned",
            MatcherField.Year => "Year",
            _ => "All"
        };
    }

    public static string ToOperatorName(MatcherOperator op)
    {
        return op switch
        {
            MatcherOperator.Is => "is",
            MatcherOperator.Contains => "contains",
            MatcherOperator.ContainsAny => "contains any of",
            MatcherOperator.ContainsAll => "contains all of",
            MatcherOperator.StartsWith => "starts with",
            MatcherOperator.EndsWith => "ends with",
            MatcherOperator.ListContains => "list contains",
            MatcherOperator.Regex => "regular expression",
            MatcherOperator.GreaterThan => "is greater",
            MatcherOperator.LessThan => "is smaller",
            MatcherOperator.Range => "is in the range",
            MatcherOperator.IsYes => "is Yes",
            MatcherOperator.IsNo => "is No",
            MatcherOperator.IsUnknown => "is Unknown",
            _ => "contains"
        };
    }

    public static string ToValueKindName(MatcherValueKind valueKind)
    {
        return valueKind switch
        {
            MatcherValueKind.String => "String",
            MatcherValueKind.Number => "Number",
            MatcherValueKind.Date => "Date",
            MatcherValueKind.Boolean => "Boolean",
            _ => "Unknown"
        };
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
