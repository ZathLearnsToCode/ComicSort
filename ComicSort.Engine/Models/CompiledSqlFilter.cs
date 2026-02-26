namespace ComicSort.Engine.Models;

public sealed class CompiledSqlFilter
{
    public string? WhereClause { get; init; }

    public IReadOnlyList<SqlQueryParameter> Parameters { get; init; } = [];

    public bool ResidualRequired { get; init; }

    public static CompiledSqlFilter Empty { get; } = new();
}

public sealed class SqlQueryParameter
{
    public string Name { get; init; } = string.Empty;

    public object? Value { get; init; }
}
