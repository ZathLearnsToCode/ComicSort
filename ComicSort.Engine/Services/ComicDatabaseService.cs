using Microsoft.EntityFrameworkCore;

namespace ComicSort.Engine.Services;

public sealed class ComicDatabaseService : IComicDatabaseService
{
    private readonly ISettingsService _settingsService;
    private readonly IComicDbContextFactory _dbContextFactory;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);

    private bool _initialized;

    public ComicDatabaseService(ISettingsService settingsService, IComicDbContextFactory dbContextFactory)
    {
        _settingsService = settingsService;
        _dbContextFactory = dbContextFactory;
    }

    public string DatabasePath => _settingsService.CurrentSettings.DatabasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _settingsService.InitializeAsync(cancellationToken);

            await using var dbContext = _dbContextFactory.CreateDbContext();
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            await EnsureMetadataTablesAsync(dbContext, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    private static async Task EnsureMetadataTablesAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        const string ensureComicInfoSql = """
            CREATE TABLE IF NOT EXISTS ComicInfo (
                Id INTEGER NOT NULL CONSTRAINT PK_ComicInfo PRIMARY KEY AUTOINCREMENT,
                ComicFileId INTEGER NOT NULL,
                Series TEXT NULL,
                Title TEXT NULL,
                Summary TEXT NULL,
                Writer TEXT NULL,
                Penciller TEXT NULL,
                Inker TEXT NULL,
                Colorist TEXT NULL,
                Publisher TEXT NULL,
                PageCount INTEGER NULL,
                CONSTRAINT FK_ComicInfo_ComicFiles_ComicFileId
                    FOREIGN KEY (ComicFileId) REFERENCES ComicFiles (Id) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_ComicInfo_ComicFileId
                ON ComicInfo (ComicFileId);
            """;

        const string ensureComicPagesSql = """
            CREATE TABLE IF NOT EXISTS ComicPages (
                Id INTEGER NOT NULL CONSTRAINT PK_ComicPages PRIMARY KEY AUTOINCREMENT,
                ComicFileId INTEGER NOT NULL,
                ImageIndex INTEGER NOT NULL,
                ImageWidth INTEGER NULL,
                ImageHeight INTEGER NULL,
                PageType TEXT NULL,
                CONSTRAINT FK_ComicPages_ComicFiles_ComicFileId
                    FOREIGN KEY (ComicFileId) REFERENCES ComicFiles (Id) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_ComicPages_ComicFileId_ImageIndex
                ON ComicPages (ComicFileId, ImageIndex);
            """;

        await dbContext.Database.ExecuteSqlRawAsync(ensureComicInfoSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(ensureComicPagesSql, cancellationToken);
    }
}
