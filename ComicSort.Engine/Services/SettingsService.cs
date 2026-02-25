using ComicSort.Engine.Settings;
using System.Text.Json;

namespace ComicSort.Engine.Services;

public sealed class SettingsService : ISettingsService
{
    private const string AppDataDirectoryName = "ComcSort2";
    private const string SettingsFileName = "settings.json";
    private const int DefaultScanBatchSize = 500;
    private const int DefaultScanStatusUpdateIntervalMs = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim _settingsLock = new(1, 1);
    private readonly string _settingsDirectoryPath;
    private readonly string _settingsFilePath;

    private AppSettings? _settings;

    public SettingsService()
    {
        var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsDirectoryPath = Path.Combine(appDataDirectory, AppDataDirectoryName);
        _settingsFilePath = Path.Combine(_settingsDirectoryPath, SettingsFileName);
    }

    public AppSettings CurrentSettings =>
        _settings ?? throw new InvalidOperationException("Settings are not loaded. Call InitializeAsync first.");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            if (_settings is not null)
            {
                return;
            }

            Directory.CreateDirectory(_settingsDirectoryPath);

            if (!File.Exists(_settingsFilePath))
            {
                _settings = CreateDefaultSettings();
                await PersistLockedAsync(cancellationToken);
                return;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
            var loadedSettings = string.IsNullOrWhiteSpace(json)
                ? new AppSettings()
                : JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();

            ApplyDefaults(loadedSettings);
            _settings = loadedSettings;
            await PersistLockedAsync(cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            _settings!.LastUpdatedUtc = DateTimeOffset.UtcNow;
            await PersistLockedAsync(cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task SavetoSettings(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        await InitializeAsync();

        await _settingsLock.WaitAsync();
        try
        {
            var normalizedFolder = folder.Trim();
            var alreadyExists = _settings!.LibraryFolders.Any(x =>
                string.Equals(x.Folder, normalizedFolder, StringComparison.OrdinalIgnoreCase));

            if (!alreadyExists)
            {
                _settings.LibraryFolders.Add(new LibraryFolderSetting
                {
                    Folder = normalizedFolder,
                    Watched = false
                });
            }

            _settings.LastUpdatedUtc = DateTimeOffset.UtcNow;
            await PersistLockedAsync();
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    private async Task PersistLockedAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_settingsDirectoryPath);

        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        var tempFilePath = _settingsFilePath + ".tmp";

        await File.WriteAllTextAsync(tempFilePath, json, cancellationToken);
        File.Move(tempFilePath, _settingsFilePath, true);
    }

    private static void ApplyDefaults(AppSettings settings)
    {
        var defaultDatabasePath = GetDefaultDatabasePath();
        var defaultThumbnailCacheDirectory = GetDefaultThumbnailCacheDirectory();

        settings.Version = settings.Version <= 0 ? 1 : settings.Version;
        settings.DatabasePath = string.IsNullOrWhiteSpace(settings.DatabasePath)
            ? defaultDatabasePath
            : settings.DatabasePath.Trim();
        settings.ThumbnailCacheDirectory = string.IsNullOrWhiteSpace(settings.ThumbnailCacheDirectory)
            ? defaultThumbnailCacheDirectory
            : settings.ThumbnailCacheDirectory.Trim();
        settings.DefaultTheme = string.IsNullOrWhiteSpace(settings.DefaultTheme)
            ? "Soft Neutral Pro"
            : settings.DefaultTheme.Trim();

        var currentThemeCandidate = !string.IsNullOrWhiteSpace(settings.CurrentTheme)
            ? settings.CurrentTheme
            : settings.LegacyThemeName;

        settings.CurrentTheme = string.IsNullOrWhiteSpace(currentThemeCandidate)
            ? settings.DefaultTheme
            : currentThemeCandidate.Trim();

        settings.LegacyThemeName = null;

        settings.ScanBatchSize = settings.ScanBatchSize <= 0 ? DefaultScanBatchSize : settings.ScanBatchSize;
        settings.ScanWorkerCount = settings.ScanWorkerCount <= 0
            ? Math.Min(4, Environment.ProcessorCount)
            : settings.ScanWorkerCount;
        settings.ScanStatusUpdateIntervalMs = settings.ScanStatusUpdateIntervalMs <= 0
            ? DefaultScanStatusUpdateIntervalMs
            : settings.ScanStatusUpdateIntervalMs;
        settings.LibraryFolders ??= [];
        settings.LibraryFolders = settings.LibraryFolders
            .Where(x => !string.IsNullOrWhiteSpace(x.Folder))
            .GroupBy(x => x.Folder, StringComparer.OrdinalIgnoreCase)
            .Select(x => new LibraryFolderSetting
            {
                Folder = x.First().Folder.Trim(),
                Watched = x.First().Watched
            })
            .ToList();
        settings.ComicLists ??= [];

        foreach (var comicList in settings.ComicLists)
        {
            comicList.Items ??= [];
            comicList.Matchers ??= [];
        }

        var defaultSettings = CreateDefaultSettings();

        foreach (var defaultList in defaultSettings.ComicLists)
        {
            var existingTopLevelList = settings.ComicLists.FirstOrDefault(x =>
                string.Equals(x.Name, defaultList.Name, StringComparison.OrdinalIgnoreCase));

            if (existingTopLevelList is null)
            {
                settings.ComicLists.Add(defaultList);
                continue;
            }

            existingTopLevelList.Items ??= [];
            existingTopLevelList.Matchers ??= [];

            foreach (var defaultChild in defaultList.Items)
            {
                var childExists = existingTopLevelList.Items.Any(x =>
                    string.Equals(x.Name, defaultChild.Name, StringComparison.OrdinalIgnoreCase));

                if (!childExists)
                {
                    existingTopLevelList.Items.Add(defaultChild);
                }
            }
        }

        if (settings.LastUpdatedUtc == default)
        {
            settings.LastUpdatedUtc = DateTimeOffset.UtcNow;
        }
    }

    private static AppSettings CreateDefaultSettings()
    {
        var now = DateTimeOffset.UtcNow;

        return new AppSettings
        {
            Version = 1,
            LastUpdatedUtc = now,
            DatabasePath = GetDefaultDatabasePath(),
            ThumbnailCacheDirectory = GetDefaultThumbnailCacheDirectory(),
            DefaultTheme = "Soft Neutral Pro",
            CurrentTheme = "Soft Neutral Pro",
            ScanBatchSize = DefaultScanBatchSize,
            ScanWorkerCount = Math.Min(4, Environment.ProcessorCount),
            ScanStatusUpdateIntervalMs = DefaultScanStatusUpdateIntervalMs,
            LibraryFolders = [],
            ComicLists =
            [
                new ComicListItem
                {
                    Id = Guid.Parse("546b0bee-c659-4984-960f-35536acc1b5b"),
                    Type = "ComicLibraryListItem",
                    Name = "Library",
                    BookCount = 0,
                    NewBookCount = 0,
                    NewBookCountDateUtc = now
                },
                new ComicListItem
                {
                    Id = Guid.Parse("0e980233-c78a-48c9-946e-49a3fdbcee09"),
                    Type = "ComicListItemFolder",
                    Name = "Smart Lists",
                    BookCount = 0,
                    NewBookCount = 0,
                    NewBookCountDateUtc = now,
                    Items =
                    [
                        CreateSmartList(
                            "92b54c9a-f077-45e1-a93e-4d492cbabfdb",
                            "My Favorites",
                            "ComicBookRatingMatcher",
                            now,
                            matchOperator: 1,
                            matchValue: 3),
                        CreateSmartList(
                            "a8a835c8-2e80-44fe-8b65-ccb3a1ca9ac0",
                            "Recently Added",
                            "ComicBookAddedMatcher",
                            now,
                            matchOperator: 3,
                            matchValue: 14),
                        CreateSmartList(
                            "a0dbaeb9-17a4-41c2-9977-5c80f7e8bd50",
                            "Recently Read",
                            "ComicBookOpenedMatcher",
                            now,
                            matchOperator: 3,
                            matchValue: 14),
                        CreateSmartList(
                            "e4075d13-d3b1-45ed-851b-5bc08c2e2079",
                            "Never Read",
                            "ComicBookReadPercentageMatcher",
                            now,
                            matchOperator: 2,
                            matchValue: 10),
                        CreateSmartList(
                            "038f4fcd-59b0-4eb2-8abc-25ccfb641d5a",
                            "Reading",
                            "ComicBookReadPercentageMatcher",
                            now,
                            matchOperator: 3,
                            matchValue: 10,
                            matchValue2: 95),
                        CreateSmartList(
                            "ca3d3044-b898-4b2e-bca0-09d85d1fb991",
                            "Read",
                            "ComicBookReadPercentageMatcher",
                            now,
                            matchOperator: 1,
                            matchValue: 95),
                        CreateSmartList(
                            "9ddc2db2-bcd1-46e7-8d2a-dade04b23291",
                            "Files to update",
                            "ComicBookModifiedInfoMatcher",
                            now: now)
                    ]
                },
                new ComicListItem
                {
                    Id = Guid.Parse("b61952b2-932a-43a5-9156-1d209d4a54e0"),
                    Type = "ComicListItemFolder",
                    Name = "Temporary Lists",
                    Temporary = true,
                    NewBookCountDateUtc = now
                }
            ]
        };
    }

    private static string GetDefaultDatabasePath()
    {
        return Path.Combine(GetAppDataRoot(), "data", "library.db");
    }

    private static string GetDefaultThumbnailCacheDirectory()
    {
        return Path.Combine(GetAppDataRoot(), "cache", "thumbnails");
    }

    private static string GetAppDataRoot()
    {
        var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataDirectory, AppDataDirectoryName);
    }

    private static ComicListItem CreateSmartList(
        string id,
        string name,
        string matcherType,
        DateTimeOffset now,
        int? matchOperator = null,
        int? matchValue = null,
        int? matchValue2 = null)
    {
        return new ComicListItem
        {
            Id = Guid.Parse(id),
            Type = "ComicSmartListItem",
            Name = name,
            BookCount = 0,
            NewBookCount = 0,
            NewBookCountDateUtc = now,
            Matchers =
            [
                new ComicBookMatcher
                {
                    MatcherType = matcherType,
                    MatchOperator = matchOperator,
                    MatchValue = matchValue,
                    MatchValue2 = matchValue2
                }
            ]
        };
    }
}
