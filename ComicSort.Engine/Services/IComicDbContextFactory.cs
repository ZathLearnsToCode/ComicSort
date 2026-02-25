using ComicSort.Engine.Data;

namespace ComicSort.Engine.Services;

public interface IComicDbContextFactory
{
    ComicSortDbContext CreateDbContext();
}
