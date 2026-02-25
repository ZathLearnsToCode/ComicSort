using ComicSort.Engine.Data.Entities;
using ComicSort.Engine.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ComicSort.Engine.Services;

public sealed class ScanRepository : IScanRepository
{
    private readonly IComicDbContextFactory _dbContextFactory;

    public ScanRepository(IComicDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<ComicFileLookup?> GetByNormalizedPathAsync(
        string normalizedPath,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        return await dbContext.ComicFiles
            .AsNoTracking()
            .Where(x => x.NormalizedPath == normalizedPath)
            .Select(x => new ComicFileLookup
            {
                NormalizedPath = x.NormalizedPath,
                Fingerprint = x.Fingerprint,
                HasThumbnail = x.HasThumbnail,
                ThumbnailPath = x.ThumbnailPath
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ComicFiles;";
        var scalar = await command.ExecuteScalarAsync(cancellationToken);

        if (scalar is null || scalar == DBNull.Value)
        {
            return 0;
        }

        return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    public async Task<ScanBatchSaveResult> UpsertBatchAsync(
        IReadOnlyCollection<ComicFileUpsertModel> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return new ScanBatchSaveResult();
        }

        await using var dbContext = _dbContextFactory.CreateDbContext();
        var normalizedPaths = items.Select(x => x.NormalizedPath).Distinct().ToArray();
        var existingItems = await dbContext.ComicFiles
            .Where(x => normalizedPaths.Contains(x.NormalizedPath))
            .ToDictionaryAsync(x => x.NormalizedPath, cancellationToken);

        var inserted = 0;
        var updated = 0;
        var savedItems = new List<ComicLibraryItem>(items.Count);

        foreach (var model in items)
        {
            if (!existingItems.TryGetValue(model.NormalizedPath, out var entity))
            {
                entity = new ComicFileEntity
                {
                    NormalizedPath = model.NormalizedPath
                };
                dbContext.ComicFiles.Add(entity);
                existingItems[model.NormalizedPath] = entity;
                inserted++;
            }
            else
            {
                updated++;
            }

            entity.FileName = model.FileName;
            entity.Extension = model.Extension;
            entity.SizeBytes = model.SizeBytes;
            entity.CreatedUtc = model.CreatedUtc;
            entity.ModifiedUtc = model.ModifiedUtc;
            entity.LastScannedUtc = model.LastScannedUtc;
            entity.Fingerprint = model.Fingerprint;
            entity.ThumbnailKey = model.ThumbnailKey;
            entity.ThumbnailPath = model.ThumbnailPath;
            entity.HasThumbnail = model.HasThumbnail;
            entity.ScanState = model.ScanState;
            entity.LastError = model.LastError;

            savedItems.Add(ToLibraryItem(entity));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ScanBatchSaveResult
        {
            Inserted = inserted,
            Updated = updated,
            SavedItems = savedItems
        };
    }

    public async Task<IReadOnlyList<ComicLibraryItem>> GetLibraryItemsAsync(
        int take,
        int skip = 0,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        await using var dbContext = _dbContextFactory.CreateDbContext();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT f.NormalizedPath,
                   f.FileName,
                   f.Extension,
                   f.LastScannedUtc,
                   f.ThumbnailPath,
                   f.HasThumbnail,
                   i.Series,
                   i.Publisher
            FROM ComicFiles f
            LEFT JOIN ComicInfo i ON i.ComicFileId = f.Id
            ORDER BY f.LastScannedUtc DESC
            LIMIT $take OFFSET $skip;
            """;

        var takeParameter = command.CreateParameter();
        takeParameter.ParameterName = "$take";
        takeParameter.Value = take;
        command.Parameters.Add(takeParameter);

        var skipParameter = command.CreateParameter();
        skipParameter.ParameterName = "$skip";
        skipParameter.Value = Math.Max(0, skip);
        command.Parameters.Add(skipParameter);

        var items = new List<ComicLibraryItem>(take);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var filePath = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var fileName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var extension = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var lastScannedRaw = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var thumbnailPath = reader.IsDBNull(4) ? null : reader.GetString(4);
            var hasThumbnailRaw = reader.IsDBNull(5) ? 0L : reader.GetInt64(5);
            var series = reader.IsDBNull(6) ? null : reader.GetString(6);
            var publisher = reader.IsDBNull(7) ? null : reader.GetString(7);

            var displayTitle = !string.IsNullOrWhiteSpace(fileName)
                ? Path.GetFileNameWithoutExtension(fileName)
                : Path.GetFileNameWithoutExtension(filePath);

            var lastScannedUtc = DateTimeOffset.TryParse(
                lastScannedRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedLastScannedUtc)
                ? parsedLastScannedUtc
                : DateTimeOffset.MinValue;

            var normalizedExtension = string.IsNullOrWhiteSpace(extension)
                ? "FILE"
                : extension.TrimStart('.').ToUpperInvariant();

            var thumbnailReady = !string.IsNullOrWhiteSpace(thumbnailPath) && File.Exists(thumbnailPath);

            items.Add(new ComicLibraryItem
            {
                FilePath = filePath,
                FileDirectory = Path.GetDirectoryName(filePath) ?? string.Empty,
                DisplayTitle = string.IsNullOrWhiteSpace(displayTitle) ? filePath : displayTitle,
                Series = series,
                Publisher = publisher,
                ThumbnailPath = thumbnailPath,
                IsThumbnailReady = hasThumbnailRaw != 0 && thumbnailReady,
                FileTypeTag = normalizedExtension,
                LastScannedUtc = lastScannedUtc
            });
        }

        return items;
    }

    private static ComicLibraryItem ToLibraryItem(ComicFileEntity entity)
    {
        return new ComicLibraryItem
        {
            FilePath = entity.NormalizedPath,
            FileDirectory = Path.GetDirectoryName(entity.NormalizedPath) ?? string.Empty,
            DisplayTitle = Path.GetFileNameWithoutExtension(entity.FileName),
            Series = entity.ComicInfo?.Series,
            Publisher = entity.ComicInfo?.Publisher,
            ThumbnailPath = entity.ThumbnailPath,
            IsThumbnailReady = entity.HasThumbnail && !string.IsNullOrWhiteSpace(entity.ThumbnailPath) && File.Exists(entity.ThumbnailPath),
            FileTypeTag = entity.Extension.TrimStart('.').ToUpperInvariant(),
            LastScannedUtc = entity.LastScannedUtc
        };
    }
}
