using ComicSort.Engine.Data;
using Microsoft.EntityFrameworkCore;

namespace ComicSort.Engine.Services;

public sealed class ComicDbContextFactory : IComicDbContextFactory
{
    private readonly ISettingsService _settingsService;
    private readonly object _optionsLock = new();
    private DbContextOptions<ComicSortDbContext>? _cachedOptions;
    private string? _cachedDatabasePath;
    private string? _ensuredDatabaseDirectory;

    public ComicDbContextFactory(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public ComicSortDbContext CreateDbContext()
    {
        var databasePath = _settingsService.CurrentSettings.DatabasePath;
        lock (_optionsLock)
        {
            if (_cachedOptions is null ||
                !string.Equals(_cachedDatabasePath, databasePath, StringComparison.OrdinalIgnoreCase))
            {
                var optionsBuilder = new DbContextOptionsBuilder<ComicSortDbContext>();
                optionsBuilder.UseSqlite($"Data Source={databasePath}");
                _cachedOptions = optionsBuilder.Options;
                _cachedDatabasePath = databasePath;
                _ensuredDatabaseDirectory = null;
            }

            var databaseDirectory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(databaseDirectory) &&
                !string.Equals(_ensuredDatabaseDirectory, databaseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(databaseDirectory);
                _ensuredDatabaseDirectory = databaseDirectory;
            }

            return new ComicSortDbContext(_cachedOptions);
        }
    }
}
