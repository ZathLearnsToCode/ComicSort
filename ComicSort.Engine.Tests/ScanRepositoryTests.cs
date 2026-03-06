using ComicSort.Engine.Data;
using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using ComicSort.Engine.Settings;
using Xunit;

namespace ComicSort.Engine.Tests;

public sealed class ScanRepositoryTests
{
    [Fact]
    public async Task GetByNormalizedPathsAsync_ReturnsExistingRowsOnly()
    {
        using var fixture = new RepositoryFixture();
        await fixture.DatabaseService.InitializeAsync();

        var existingPath = Path.GetFullPath(Path.Combine(fixture.RootDirectory, "library", "issue-1.cbz"));
        await fixture.Repository.UpsertBatchAsync(
        [
            new ComicFileUpsertModel
            {
                NormalizedPath = existingPath,
                FileName = "issue-1.cbz",
                Extension = ".cbz",
                SizeBytes = 123,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
                ModifiedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                LastScannedUtc = DateTimeOffset.UtcNow,
                Fingerprint = "123|1",
                HasThumbnail = true,
                ThumbnailPath = Path.Combine(fixture.RootDirectory, "thumb.jpg"),
                ScanState = ScanState.Ok
            }
        ]);

        var missingPath = Path.GetFullPath(Path.Combine(fixture.RootDirectory, "library", "missing.cbz"));
        var rows = await fixture.Repository.GetByNormalizedPathsAsync([existingPath, missingPath]);

        Assert.Single(rows);
        Assert.True(rows.ContainsKey(existingPath));
        Assert.False(rows.ContainsKey(missingPath));
    }

    [Fact]
    public async Task GetLibraryItemsAsync_UsesHasThumbnailWithoutDiskProbe()
    {
        using var fixture = new RepositoryFixture();
        await fixture.DatabaseService.InitializeAsync();

        var normalizedPath = Path.GetFullPath(Path.Combine(fixture.RootDirectory, "library", "issue-2.cbz"));
        var missingThumbnailPath = Path.Combine(fixture.RootDirectory, "cache", "missing-thumbnail.jpg");

        await fixture.Repository.UpsertBatchAsync(
        [
            new ComicFileUpsertModel
            {
                NormalizedPath = normalizedPath,
                FileName = "issue-2.cbz",
                Extension = ".cbz",
                SizeBytes = 456,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
                ModifiedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                LastScannedUtc = DateTimeOffset.UtcNow,
                Fingerprint = "456|2",
                HasThumbnail = true,
                ThumbnailPath = missingThumbnailPath,
                ScanState = ScanState.Ok
            }
        ]);

        var rows = await fixture.Repository.GetLibraryItemsAsync(10);
        var row = Assert.Single(rows);

        Assert.True(row.IsThumbnailReady);
        Assert.Equal(missingThumbnailPath, row.ThumbnailPath);
    }

    [Fact]
    public async Task DeleteByNormalizedPathsAsync_RemovesExistingRowsOnly()
    {
        using var fixture = new RepositoryFixture();
        await fixture.DatabaseService.InitializeAsync();

        var pathA = Path.GetFullPath(Path.Combine(fixture.RootDirectory, "library", "a.cbz"));
        var pathB = Path.GetFullPath(Path.Combine(fixture.RootDirectory, "library", "b.cbz"));
        await fixture.Repository.UpsertBatchAsync(
        [
            CreateModel(pathA, "a.cbz", 1),
            CreateModel(pathB, "b.cbz", 2)
        ]);

        var missingPath = Path.GetFullPath(Path.Combine(fixture.RootDirectory, "library", "missing.cbz"));
        var removed = await fixture.Repository.DeleteByNormalizedPathsAsync([pathA, missingPath]);

        Assert.Single(removed);
        Assert.Equal(pathA, removed[0]);

        var remaining = await fixture.Repository.GetByNormalizedPathsAsync([pathA, pathB]);
        Assert.False(remaining.ContainsKey(pathA));
        Assert.True(remaining.ContainsKey(pathB));
    }

    private static ComicFileUpsertModel CreateModel(string normalizedPath, string fileName, long sequence)
    {
        return new ComicFileUpsertModel
        {
            SequenceNumber = sequence,
            NormalizedPath = normalizedPath,
            FileName = fileName,
            Extension = ".cbz",
            SizeBytes = 256 + sequence,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
            ModifiedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            LastScannedUtc = DateTimeOffset.UtcNow,
            Fingerprint = $"{256 + sequence}|{sequence}",
            HasThumbnail = false,
            ThumbnailPath = null,
            ScanState = ScanState.Pending
        };
    }

    private sealed class RepositoryFixture : IDisposable
    {
        public RepositoryFixture()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), $"comicsort-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootDirectory);

            var settings = new AppSettings
            {
                DatabasePath = Path.Combine(RootDirectory, "data", "library.db"),
                ThumbnailCacheDirectory = Path.Combine(RootDirectory, "cache", "thumbnails")
            };

            SettingsService = new TestSettingsService(settings);
            DbContextFactory = new ComicDbContextFactory(SettingsService);
            DatabaseService = new ComicDatabaseService(SettingsService, DbContextFactory);
            Repository = new ScanRepository(DbContextFactory);
        }

        public string RootDirectory { get; }

        public ISettingsService SettingsService { get; }

        public IComicDbContextFactory DbContextFactory { get; }

        public IComicDatabaseService DatabaseService { get; }

        public IScanRepository Repository { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootDirectory))
                {
                    Directory.Delete(RootDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }
}
