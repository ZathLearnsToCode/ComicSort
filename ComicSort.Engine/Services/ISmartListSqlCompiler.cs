using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface ISmartListSqlCompiler
{
    CompiledSqlFilter Compile(MatcherGroupNode expression);
}
