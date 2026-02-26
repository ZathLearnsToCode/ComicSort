using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface ISmartListSerializer
{
    string Serialize(MatcherGroupNode expression);
}
