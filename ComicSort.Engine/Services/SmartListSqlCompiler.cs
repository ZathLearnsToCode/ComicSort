using ComicSort.Engine.Models;
using System.Globalization;
using System.Text;

namespace ComicSort.Engine.Services;

public sealed class SmartListSqlCompiler : ISmartListSqlCompiler
{
    private const string DisplayTitleSql =
        "CASE WHEN LENGTH(f.FileName) > LENGTH(f.Extension) THEN SUBSTR(f.FileName, 1, LENGTH(f.FileName) - LENGTH(f.Extension)) ELSE f.FileName END";

    private static readonly string SeriesFallbackSql =
        $"CASE " +
        $"WHEN INSTR({DisplayTitleSql}, '#') > 1 THEN TRIM(SUBSTR({DisplayTitleSql}, 1, INSTR({DisplayTitleSql}, '#') - 1)) " +
        $"WHEN INSTR({DisplayTitleSql}, '(') > 1 THEN TRIM(SUBSTR({DisplayTitleSql}, 1, INSTR({DisplayTitleSql}, '(') - 1)) " +
        $"ELSE TRIM({DisplayTitleSql}) END";

    public CompiledSqlFilter Compile(MatcherGroupNode expression)
    {
        var state = new CompilerState();
        var compileResult = CompileNode(expression, state);

        return new CompiledSqlFilter
        {
            WhereClause = compileResult.Sql,
            Parameters = state.Parameters.ToArray(),
            ResidualRequired = compileResult.ResidualRequired
        };
    }

    private static CompileNodeResult CompileNode(IMatcherNode node, CompilerState state)
    {
        return node switch
        {
            MatcherRuleNode rule => CompileRule(rule, state),
            MatcherGroupNode group => CompileGroup(group, state),
            _ => new CompileNodeResult(null, residualRequired: true)
        };
    }

    private static CompileNodeResult CompileGroup(MatcherGroupNode group, CompilerState state)
    {
        if (group.Children.Count == 0)
        {
            return new CompileNodeResult(null, residualRequired: false);
        }

        if (group.Mode == MatcherMode.Or)
        {
            var orParts = new List<string>(group.Children.Count);
            var residual = false;

            foreach (var child in group.Children)
            {
                var childResult = CompileNode(child, state);
                if (string.IsNullOrWhiteSpace(childResult.Sql))
                {
                    return new CompileNodeResult(null, residualRequired: true);
                }

                if (childResult.ResidualRequired)
                {
                    residual = true;
                }

                orParts.Add(childResult.Sql);
            }

            var orSql = $"({string.Join(" OR ", orParts)})";
            if (group.Not)
            {
                orSql = $"NOT ({orSql})";
            }

            return new CompileNodeResult(orSql, residual);
        }

        var andParts = new List<string>(group.Children.Count);
        var andResidual = false;

        foreach (var child in group.Children)
        {
            var childResult = CompileNode(child, state);
            andResidual |= childResult.ResidualRequired || string.IsNullOrWhiteSpace(childResult.Sql);

            if (!string.IsNullOrWhiteSpace(childResult.Sql))
            {
                andParts.Add(childResult.Sql);
            }
        }

        if (andParts.Count == 0)
        {
            return new CompileNodeResult(null, residualRequired: andResidual || group.Children.Count > 0);
        }

        var andSql = $"({string.Join(" AND ", andParts)})";
        if (group.Not)
        {
            andSql = $"NOT ({andSql})";
        }

        return new CompileNodeResult(andSql, andResidual);
    }

    private static CompileNodeResult CompileRule(MatcherRuleNode rule, CompilerState state)
    {
        if (!TryGetFieldSql(rule.Field, out var fieldSql, out var isNumeric))
        {
            return new CompileNodeResult(null, residualRequired: true);
        }

        var sql = isNumeric
            ? CompileNumericRule(rule, fieldSql, state)
            : CompileStringRule(rule, fieldSql, state);

        if (string.IsNullOrWhiteSpace(sql))
        {
            return new CompileNodeResult(null, residualRequired: true);
        }

        if (rule.Not)
        {
            sql = $"NOT ({sql})";
        }

        return new CompileNodeResult(sql, residualRequired: false);
    }

    private static string? CompileStringRule(MatcherRuleNode rule, string fieldSql, CompilerState state)
    {
        if (rule.Operator is MatcherOperator.Regex or MatcherOperator.ListContains)
        {
            return null;
        }

        var normalizedValue = (rule.Value1 ?? string.Empty).Trim().ToLowerInvariant();
        var parameterName = state.CreateParameter(normalizedValue);
        var loweredField = $"LOWER({fieldSql})";

        return rule.Operator switch
        {
            MatcherOperator.Is => $"{loweredField} = {parameterName}",
            MatcherOperator.Contains => $"{loweredField} LIKE {state.CreateParameter($"%{normalizedValue}%")}",
            MatcherOperator.StartsWith => $"{loweredField} LIKE {state.CreateParameter($"{normalizedValue}%")}",
            MatcherOperator.EndsWith => $"{loweredField} LIKE {state.CreateParameter($"%{normalizedValue}")}",
            MatcherOperator.ContainsAny => BuildContainsAnySql(loweredField, normalizedValue, state),
            MatcherOperator.ContainsAll => BuildContainsAllSql(loweredField, normalizedValue, state),
            _ => null
        };
    }

    private static string? BuildContainsAnySql(string loweredField, string normalizedValue, CompilerState state)
    {
        var values = SplitValues(normalizedValue).ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        var parts = values
            .Select(value => $"{loweredField} LIKE {state.CreateParameter($"%{value}%")}")
            .ToArray();

        return $"({string.Join(" OR ", parts)})";
    }

    private static string? BuildContainsAllSql(string loweredField, string normalizedValue, CompilerState state)
    {
        var values = SplitValues(normalizedValue).ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        var parts = values
            .Select(value => $"{loweredField} LIKE {state.CreateParameter($"%{value}%")}")
            .ToArray();

        return $"({string.Join(" AND ", parts)})";
    }

    private static string? CompileNumericRule(MatcherRuleNode rule, string fieldSql, CompilerState state)
    {
        if (!double.TryParse(rule.Value1, NumberStyles.Float, CultureInfo.InvariantCulture, out var value1))
        {
            return null;
        }

        return rule.Operator switch
        {
            MatcherOperator.Is => $"{fieldSql} = {state.CreateParameter(value1)}",
            MatcherOperator.GreaterThan => $"{fieldSql} > {state.CreateParameter(value1)}",
            MatcherOperator.LessThan => $"{fieldSql} < {state.CreateParameter(value1)}",
            MatcherOperator.Range when double.TryParse(rule.Value2, NumberStyles.Float, CultureInfo.InvariantCulture, out var value2) =>
                $"({fieldSql} >= {state.CreateParameter(value1)} AND {fieldSql} <= {state.CreateParameter(value2)})",
            _ => null
        };
    }

    private static bool TryGetFieldSql(MatcherField field, out string sql, out bool isNumeric)
    {
        isNumeric = false;

        switch (field)
        {
            case MatcherField.Series:
                sql = $"COALESCE(NULLIF(TRIM(i.Series), ''), {SeriesFallbackSql})";
                return true;
            case MatcherField.Publisher:
                sql = "COALESCE(NULLIF(TRIM(i.Publisher), ''), 'Unspecified')";
                return true;
            case MatcherField.FileDirectory:
                sql = "CASE WHEN LENGTH(f.NormalizedPath) > LENGTH(f.FileName) THEN SUBSTR(f.NormalizedPath, 1, LENGTH(f.NormalizedPath) - LENGTH(f.FileName) - 1) ELSE '' END";
                return true;
            case MatcherField.FilePath:
                sql = "f.NormalizedPath";
                return true;
            case MatcherField.Title:
                sql = DisplayTitleSql;
                return true;
            case MatcherField.FileFormat:
                sql = "UPPER(REPLACE(f.Extension, '.', ''))";
                return true;
            case MatcherField.SizeBytes:
                sql = "f.SizeBytes";
                isNumeric = true;
                return true;
            default:
                sql = string.Empty;
                return false;
        }
    }

    private static IEnumerable<string> SplitValues(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        return input
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length > 0);
    }

    private sealed class CompilerState
    {
        private int _parameterIndex;

        public List<SqlQueryParameter> Parameters { get; } = [];

        public string CreateParameter(object? value)
        {
            var name = $"$p{_parameterIndex++}";
            Parameters.Add(new SqlQueryParameter
            {
                Name = name,
                Value = value
            });

            return name;
        }
    }

    private readonly struct CompileNodeResult
    {
        public CompileNodeResult(string? sql, bool residualRequired)
        {
            Sql = sql;
            ResidualRequired = residualRequired;
        }

        public string? Sql { get; }

        public bool ResidualRequired { get; }
    }
}
