using ComicSort.Engine.Data;
using ComicSort.Engine.Models;
using System.Globalization;
using System.Xml.Linq;

namespace ComicSort.Engine.Services;

public sealed class ComicRackImportService : IComicRackImportService
{
    private readonly IScanRepository _scanRepository;
    private readonly IComicDatabaseService _comicDatabaseService;
    private readonly ISettingsService _settingsService;

    public ComicRackImportService(
        IScanRepository scanRepository,
        IComicDatabaseService comicDatabaseService,
        ISettingsService settingsService)
    {
        _scanRepository = scanRepository;
        _comicDatabaseService = comicDatabaseService;
        _settingsService = settingsService;
    }

    public async Task ImportFromXmlAsync(string xmlFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(xmlFilePath))
        {
            throw new ArgumentException("XML file path is required.", nameof(xmlFilePath));
        }

        if (!File.Exists(xmlFilePath))
        {
            throw new FileNotFoundException("XML file was not found.", xmlFilePath);
        }

        await _comicDatabaseService.InitializeAsync(cancellationToken);

        var document = await Task.Run(() => XDocument.Load(xmlFilePath), cancellationToken);
        var books = document.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "Book", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var batchSize = Math.Max(1, _settingsService.CurrentSettings.ScanBatchSize);
        var buffer = new List<ComicFileUpsertModel>(batchSize);

        foreach (var book in books)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = (string?)book.Attribute("File");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                continue;
            }

            var normalizedPath = Path.GetFullPath(filePath.Trim());
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(filePath);

            var fileSize = ParseLong(GetElementValue(book, "FileSize")) ?? 0L;
            var createdUtc = ParseDateTimeOffset(GetElementValue(book, "FileCreationTime")) ?? DateTimeOffset.UtcNow;
            var modifiedUtc = ParseDateTimeOffset(GetElementValue(book, "FileModifiedTime")) ?? DateTimeOffset.UtcNow;
            var addedUtc = ParseDateTimeOffset(GetElementValue(book, "Added")) ?? DateTimeOffset.UtcNow;
            var fingerprint = $"{fileSize}|{modifiedUtc.UtcTicks}";

            buffer.Add(new ComicFileUpsertModel
            {
                NormalizedPath = normalizedPath,
                FileName = fileName,
                Extension = extension,
                SizeBytes = fileSize,
                CreatedUtc = createdUtc,
                ModifiedUtc = modifiedUtc,
                LastScannedUtc = addedUtc,
                Fingerprint = fingerprint,
                ThumbnailKey = null,
                ThumbnailPath = null,
                HasThumbnail = false,
                ScanState = ScanState.Pending
            });

            if (buffer.Count < batchSize)
            {
                continue;
            }

            await _scanRepository.UpsertBatchAsync(buffer, cancellationToken);
            buffer.Clear();
        }

        if (buffer.Count > 0)
        {
            await _scanRepository.UpsertBatchAsync(buffer, cancellationToken);
        }
    }

    private static string? GetElementValue(XElement element, string localName)
    {
        return element.Elements()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static long? ParseLong(string? value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
