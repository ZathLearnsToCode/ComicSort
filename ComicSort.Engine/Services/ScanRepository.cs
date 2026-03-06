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
                FileName = x.FileName,
                SizeBytes = x.SizeBytes,
                Fingerprint = x.Fingerprint,
                HasThumbnail = x.HasThumbnail,
                ThumbnailPath = x.ThumbnailPath,
                HasComicInfo = x.ComicInfo != null
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ComicFileLookup>> GetByNormalizedPathsAsync(
        IReadOnlyCollection<string> normalizedPaths,
        CancellationToken cancellationToken = default)
    {
        if (normalizedPaths.Count == 0)
        {
            return new Dictionary<string, ComicFileLookup>(StringComparer.OrdinalIgnoreCase);
        }

        await using var dbContext = _dbContextFactory.CreateDbContext();
        var paths = normalizedPaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (paths.Length == 0)
        {
            return new Dictionary<string, ComicFileLookup>(StringComparer.OrdinalIgnoreCase);
        }

        const int maxSqliteVariableCount = 900;
        var results = new Dictionary<string, ComicFileLookup>(StringComparer.OrdinalIgnoreCase);

        for (var offset = 0; offset < paths.Length; offset += maxSqliteVariableCount)
        {
            var chunk = paths
                .Skip(offset)
                .Take(maxSqliteVariableCount)
                .ToArray();

            var chunkRows = await dbContext.ComicFiles
                .AsNoTracking()
                .Where(x => chunk.Contains(x.NormalizedPath))
                .Select(x => new ComicFileLookup
                {
                    NormalizedPath = x.NormalizedPath,
                    FileName = x.FileName,
                    SizeBytes = x.SizeBytes,
                    Fingerprint = x.Fingerprint,
                    HasThumbnail = x.HasThumbnail,
                    ThumbnailPath = x.ThumbnailPath,
                    HasComicInfo = x.ComicInfo != null
                })
                .ToArrayAsync(cancellationToken);

            foreach (var row in chunkRows)
            {
                results[row.NormalizedPath] = row;
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<ComicFileLookup>> GetAllLookupsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        return await dbContext.ComicFiles
            .AsNoTracking()
            .Select(x => new ComicFileLookup
            {
                NormalizedPath = x.NormalizedPath,
                FileName = x.FileName,
                SizeBytes = x.SizeBytes,
                Fingerprint = x.Fingerprint,
                HasThumbnail = x.HasThumbnail,
                ThumbnailPath = x.ThumbnailPath,
                HasComicInfo = x.ComicInfo != null
            })
            .ToArrayAsync(cancellationToken);
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

    public async Task DeleteByNormalizedPathAsync(
        string normalizedPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        await using var dbContext = _dbContextFactory.CreateDbContext();
        var entity = await dbContext.ComicFiles
            .FirstOrDefaultAsync(x => x.NormalizedPath == normalizedPath, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.ComicFiles.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> DeleteByNormalizedPathsAsync(
        IReadOnlyCollection<string> normalizedPaths,
        CancellationToken cancellationToken = default)
    {
        if (normalizedPaths.Count == 0)
        {
            return [];
        }

        var paths = normalizedPaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0)
        {
            return [];
        }

        await using var dbContext = _dbContextFactory.CreateDbContext();
        const int maxSqliteVariableCount = 900;
        var removedPaths = new List<string>(paths.Length);

        for (var offset = 0; offset < paths.Length; offset += maxSqliteVariableCount)
        {
            var chunk = paths
                .Skip(offset)
                .Take(maxSqliteVariableCount)
                .ToArray();

            var chunkEntities = await dbContext.ComicFiles
                .Where(x => chunk.Contains(x.NormalizedPath))
                .ToArrayAsync(cancellationToken);

            if (chunkEntities.Length == 0)
            {
                continue;
            }

            removedPaths.AddRange(chunkEntities.Select(x => x.NormalizedPath));
            dbContext.ComicFiles.RemoveRange(chunkEntities);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return removedPaths;
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
            .Include(x => x.ComicInfo)
            .Include(x => x.Pages)
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

            var metadata = model.Metadata;
            if (metadata is not null)
            {
                entity.ComicInfo ??= new ComicInfoEntity();
                entity.ComicInfo.Series = metadata.Series;
                entity.ComicInfo.Title = metadata.Title;
                entity.ComicInfo.Summary = metadata.Summary;
                entity.ComicInfo.Writer = metadata.Writer;
                entity.ComicInfo.Penciller = metadata.Penciller;
                entity.ComicInfo.Inker = metadata.Inker;
                entity.ComicInfo.Colorist = metadata.Colorist;
                entity.ComicInfo.Publisher = metadata.Publisher;
                entity.ComicInfo.PageCount = metadata.PageCount;

                if (entity.Pages.Count > 0)
                {
                    dbContext.ComicPages.RemoveRange(entity.Pages);
                    entity.Pages.Clear();
                }

                var uniquePages = metadata.Pages
                    .GroupBy(x => x.ImageIndex)
                    .Select(x => x.First())
                    .OrderBy(x => x.ImageIndex);

                foreach (var page in uniquePages)
                {
                    entity.Pages.Add(new ComicPageEntity
                    {
                        ImageIndex = page.ImageIndex,
                        ImageWidth = page.ImageWidth,
                        ImageHeight = page.ImageHeight,
                        PageType = page.PageType
                    });
                }
            }

            savedItems.Add(ToLibraryItem(entity, model.SequenceNumber));
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
        var candidates = await QueryCandidatesAsync(
            CompiledSqlFilter.Empty,
            take,
            skip,
            cancellationToken);

        return candidates.Select(ToLibraryItem).ToArray();
    }

    public async Task<IReadOnlyList<ComicLibraryProjection>> QueryCandidatesAsync(
        CompiledSqlFilter filter,
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
        var sqlBuilder = new System.Text.StringBuilder("""
            SELECT f.NormalizedPath,
                   f.FileName,
                   f.Extension,
                   f.LastScannedUtc,
                   f.ThumbnailPath,
                   f.HasThumbnail,
                   f.SizeBytes,
                   f.CreatedUtc,
                   f.ModifiedUtc,
                   i.Series,
                   i.Publisher
            FROM ComicFiles f
            LEFT JOIN ComicInfo i ON i.ComicFileId = f.Id
            """);

        if (!string.IsNullOrWhiteSpace(filter.WhereClause))
        {
            sqlBuilder.AppendLine();
            sqlBuilder.Append("WHERE ");
            sqlBuilder.Append(filter.WhereClause);
        }

        sqlBuilder.AppendLine();
        sqlBuilder.Append("ORDER BY f.LastScannedUtc DESC");
        sqlBuilder.AppendLine();
        sqlBuilder.Append("LIMIT $take OFFSET $skip;");

        command.CommandText = sqlBuilder.ToString();

        var takeParameter = command.CreateParameter();
        takeParameter.ParameterName = "$take";
        takeParameter.Value = take;
        command.Parameters.Add(takeParameter);

        var skipParameter = command.CreateParameter();
        skipParameter.ParameterName = "$skip";
        skipParameter.Value = Math.Max(0, skip);
        command.Parameters.Add(skipParameter);

        foreach (var parameter in filter.Parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }

        var items = new List<ComicLibraryProjection>(take);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var filePath = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var fileName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var extension = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var lastScannedRaw = reader.IsDBNull(3) ? string.Empty : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty;
            var thumbnailPath = reader.IsDBNull(4) ? null : reader.GetString(4);
            var hasThumbnailRaw = reader.IsDBNull(5) ? 0L : reader.GetInt64(5);
            var sizeBytes = reader.IsDBNull(6) ? 0L : reader.GetInt64(6);
            var createdRaw = reader.IsDBNull(7) ? string.Empty : Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty;
            var modifiedRaw = reader.IsDBNull(8) ? string.Empty : Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture) ?? string.Empty;
            var series = reader.IsDBNull(9) ? null : reader.GetString(9);
            var publisher = reader.IsDBNull(10) ? null : reader.GetString(10);

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

            var createdUtc = DateTimeOffset.TryParse(
                createdRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedCreatedUtc)
                ? parsedCreatedUtc
                : DateTimeOffset.MinValue;

            var modifiedUtc = DateTimeOffset.TryParse(
                modifiedRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedModifiedUtc)
                ? parsedModifiedUtc
                : DateTimeOffset.MinValue;

            items.Add(new ComicLibraryProjection
            {
                FilePath = filePath,
                FileDirectory = Path.GetDirectoryName(filePath) ?? string.Empty,
                DisplayTitle = string.IsNullOrWhiteSpace(displayTitle) ? filePath : displayTitle,
                FileName = fileName,
                Extension = extension,
                Series = series,
                Publisher = publisher,
                ThumbnailPath = thumbnailPath,
                HasThumbnail = hasThumbnailRaw != 0,
                SizeBytes = sizeBytes,
                CreatedUtc = createdUtc,
                ModifiedUtc = modifiedUtc,
                LastScannedUtc = lastScannedUtc
            });
        }

        return items;
    }

    public async Task<int> CountCandidatesAsync(
        CompiledSqlFilter filter,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        var sqlBuilder = new System.Text.StringBuilder("SELECT COUNT(*) FROM ComicFiles f LEFT JOIN ComicInfo i ON i.ComicFileId = f.Id");
        if (!string.IsNullOrWhiteSpace(filter.WhereClause))
        {
            sqlBuilder.Append(" WHERE ");
            sqlBuilder.Append(filter.WhereClause);
        }

        sqlBuilder.Append(';');
        command.CommandText = sqlBuilder.ToString();

        foreach (var parameter in filter.Parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        if (scalar is null || scalar == DBNull.Value)
        {
            return 0;
        }

        return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    public async Task<int> RewritePathsForDirectoryRenameAsync(
        string oldDirectoryPath,
        string newDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        var normalizedOldDirectoryPath = NormalizeDirectoryPath(oldDirectoryPath);
        var normalizedNewDirectoryPath = NormalizeDirectoryPath(newDirectoryPath);
        if (string.IsNullOrWhiteSpace(normalizedOldDirectoryPath) ||
            string.IsNullOrWhiteSpace(normalizedNewDirectoryPath) ||
            string.Equals(normalizedOldDirectoryPath, normalizedNewDirectoryPath, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var oldPrefix = normalizedOldDirectoryPath + Path.DirectorySeparatorChar;
        var newPrefix = normalizedNewDirectoryPath + Path.DirectorySeparatorChar;
        var oldPrefixLike = EscapeLikePattern(oldPrefix) + "%";
        var updatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        await using var dbContext = _dbContextFactory.CreateDbContext();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            UPDATE ComicFiles
            SET NormalizedPath = $newPrefix || SUBSTR(NormalizedPath, LENGTH($oldPrefix) + 1),
                LastScannedUtc = $updatedUtc
            WHERE LOWER(NormalizedPath) LIKE LOWER($oldPrefixLike) ESCAPE '\';
            """;

        AddParameter(command, "$newPrefix", newPrefix);
        AddParameter(command, "$oldPrefix", oldPrefix);
        AddParameter(command, "$oldPrefixLike", oldPrefixLike);
        AddParameter(command, "$updatedUtc", updatedUtc);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> RewritePathForFileRenameAsync(
        string oldFilePath,
        string newFilePath,
        CancellationToken cancellationToken = default)
    {
        var normalizedOldFilePath = NormalizeFilePath(oldFilePath);
        var normalizedNewFilePath = NormalizeFilePath(newFilePath);
        if (string.IsNullOrWhiteSpace(normalizedOldFilePath) ||
            string.IsNullOrWhiteSpace(normalizedNewFilePath) ||
            string.Equals(normalizedOldFilePath, normalizedNewFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var newFileName = Path.GetFileName(normalizedNewFilePath);
        var newExtension = Path.GetExtension(normalizedNewFilePath);
        var updatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        await using var dbContext = _dbContextFactory.CreateDbContext();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            UPDATE ComicFiles
            SET NormalizedPath = $newPath,
                FileName = $newFileName,
                Extension = $newExtension,
                LastScannedUtc = $updatedUtc
            WHERE LOWER(NormalizedPath) = LOWER($oldPath);
            """;

        AddParameter(command, "$newPath", normalizedNewFilePath);
        AddParameter(command, "$newFileName", newFileName);
        AddParameter(command, "$newExtension", newExtension);
        AddParameter(command, "$oldPath", normalizedOldFilePath);
        AddParameter(command, "$updatedUtc", updatedUtc);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    private static ComicLibraryItem ToLibraryItem(ComicFileEntity entity)
    {
        return ToLibraryItem(entity, sequenceNumber: 0);
    }

    private static ComicLibraryItem ToLibraryItem(ComicFileEntity entity, long sequenceNumber)
    {
        return new ComicLibraryItem
        {
            SequenceNumber = sequenceNumber,
            FilePath = entity.NormalizedPath,
            FileDirectory = Path.GetDirectoryName(entity.NormalizedPath) ?? string.Empty,
            DisplayTitle = Path.GetFileNameWithoutExtension(entity.FileName),
            Series = entity.ComicInfo?.Series,
            Publisher = entity.ComicInfo?.Publisher,
            ThumbnailPath = entity.ThumbnailPath,
            IsThumbnailReady = entity.HasThumbnail,
            FileTypeTag = entity.Extension.TrimStart('.').ToUpperInvariant(),
            LastScannedUtc = entity.LastScannedUtc
        };
    }

    private static ComicLibraryItem ToLibraryItem(ComicLibraryProjection candidate)
    {
        var normalizedExtension = string.IsNullOrWhiteSpace(candidate.Extension)
            ? "FILE"
            : candidate.Extension.TrimStart('.').ToUpperInvariant();

        var thumbnailReady = candidate.HasThumbnail;

        return new ComicLibraryItem
        {
            SequenceNumber = 0,
            FilePath = candidate.FilePath,
            FileDirectory = candidate.FileDirectory,
            DisplayTitle = candidate.DisplayTitle,
            Series = candidate.Series,
            Publisher = candidate.Publisher,
            ThumbnailPath = candidate.ThumbnailPath,
            IsThumbnailReady = thumbnailReady,
            FileTypeTag = normalizedExtension,
            LastScannedUtc = candidate.LastScannedUtc
        };
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string NormalizeDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static string NormalizeFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).Trim();
        }
        catch
        {
            return path.Trim();
        }
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
