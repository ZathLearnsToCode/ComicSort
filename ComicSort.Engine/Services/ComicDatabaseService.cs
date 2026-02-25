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
            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }
}
