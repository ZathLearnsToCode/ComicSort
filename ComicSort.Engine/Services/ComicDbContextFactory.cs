using ComicSort.Engine.Data;
using Microsoft.EntityFrameworkCore;

namespace ComicSort.Engine.Services;

public sealed class ComicDbContextFactory : IComicDbContextFactory
{
    private readonly ISettingsService _settingsService;

    public ComicDbContextFactory(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public ComicSortDbContext CreateDbContext()
    {
        var databasePath = _settingsService.CurrentSettings.DatabasePath;
        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        var optionsBuilder = new DbContextOptionsBuilder<ComicSortDbContext>();
        optionsBuilder.UseSqlite($"Data Source={databasePath}");
        return new ComicSortDbContext(optionsBuilder.Options);
    }
}
