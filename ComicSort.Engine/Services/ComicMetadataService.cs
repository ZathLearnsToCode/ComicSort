using ComicSort.Engine.Models;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ComicSort.Engine.Services;

public sealed class ComicMetadataService : IComicMetadataService
{
    private static readonly Regex IssueWithMarkerRegex = new(
        @"#\s*(?<issue>[\d]+(?:\.[\d]+)?[A-Za-z]?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex YearRegex = new(
        @"(?:\(|\b)(?<year>(?:19|20)\d{2})(?:\)|\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TrailingIssueRegex = new(
        @"\b(?<issue>\d{1,4}[A-Za-z]?)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IArchiveInspectorService _archiveInspectorService;

    public ComicMetadataService(IArchiveInspectorService archiveInspectorService)
    {
        _archiveInspectorService = archiveInspectorService;
    }

    public async Task<ComicMetadata> GetMetadataAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        var fallbackMetadata = BuildFallbackMetadata(archivePath);
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return fallbackMetadata;
        }

        var inspection = await _archiveInspectorService.InspectAsync(archivePath, cancellationToken);
        if (!inspection.Success || string.IsNullOrWhiteSpace(inspection.ComicInfoEntryPath))
        {
            return fallbackMetadata;
        }

        var comicInfoBytes = await _archiveInspectorService.ExtractEntryAsync(
            archivePath,
            inspection.ComicInfoEntryPath,
            cancellationToken);

        if (comicInfoBytes is null || comicInfoBytes.Length == 0)
        {
            return fallbackMetadata;
        }

        var comicInfoMetadata = TryParseComicInfoXml(comicInfoBytes, fallbackMetadata);
        return comicInfoMetadata ?? fallbackMetadata;
    }

    private static ComicMetadata BuildFallbackMetadata(string archivePath)
    {
        var normalizedPath = NormalizePath(archivePath);
        var fileName = Path.GetFileName(normalizedPath);
        var displayTitle = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(displayTitle))
        {
            displayTitle = fileName;
        }

        var parsed = ParseFromFileName(displayTitle);
        return new ComicMetadata
        {
            FilePath = normalizedPath,
            FileName = fileName,
            DisplayTitle = BuildDisplayTitle(parsed.Series, parsed.IssueNumber, parsed.Year, displayTitle),
            Series = parsed.Series,
            Title = displayTitle,
            IssueNumber = parsed.IssueNumber,
            Year = parsed.Year,
            Source = ComicMetadataSource.FileNameFallback
        };
    }

    private static (string? Series, string? IssueNumber, int? Year) ParseFromFileName(string? displayTitle)
    {
        if (string.IsNullOrWhiteSpace(displayTitle))
        {
            return (null, null, null);
        }

        var workingValue = displayTitle.Trim();
        var yearMatch = YearRegex.Match(workingValue);
        var issueMatch = IssueWithMarkerRegex.Match(workingValue);
        var issueIndex = issueMatch.Success ? issueMatch.Index : -1;
        var issueNumber = issueMatch.Success
            ? issueMatch.Groups["issue"].Value.Trim()
            : null;

        if (string.IsNullOrWhiteSpace(issueNumber))
        {
            var issueSearchSlice = yearMatch.Success && yearMatch.Index > 0
                ? workingValue[..yearMatch.Index]
                : workingValue;
            var trailingMatches = TrailingIssueRegex.Matches(issueSearchSlice);
            if (trailingMatches.Count > 0)
            {
                var trailingMatch = trailingMatches[^1];
                issueNumber = trailingMatch.Groups["issue"].Value.Trim();
                issueIndex = trailingMatch.Index;
            }
        }

        int? year = null;
        if (yearMatch.Success &&
            int.TryParse(yearMatch.Groups["year"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedYear))
        {
            year = parsedYear;
        }

        var seriesBoundary = int.MaxValue;
        if (issueIndex >= 0)
        {
            seriesBoundary = Math.Min(seriesBoundary, issueIndex);
        }

        if (yearMatch.Success)
        {
            seriesBoundary = Math.Min(seriesBoundary, yearMatch.Index);
        }

        string? series;
        if (seriesBoundary != int.MaxValue && seriesBoundary > 0)
        {
            series = workingValue[..seriesBoundary];
        }
        else
        {
            series = workingValue;
        }

        series = series
            .Trim()
            .Trim('-', '_');

        if (string.IsNullOrWhiteSpace(series))
        {
            series = null;
        }

        if (string.IsNullOrWhiteSpace(issueNumber))
        {
            issueNumber = null;
        }

        return (series, issueNumber, year);
    }

    private static ComicMetadata? TryParseComicInfoXml(byte[] xmlBytes, ComicMetadata fallbackMetadata)
    {
        try
        {
            using var stream = new MemoryStream(xmlBytes);
            var document = XDocument.Load(stream);
            var pages = ParsePageEntries(document);

            var series = Coalesce(GetElementValue(document, "Series"), fallbackMetadata.Series);
            var title = Coalesce(GetElementValue(document, "Title"), fallbackMetadata.Title);
            var issueNumber = Coalesce(GetElementValue(document, "Number"), fallbackMetadata.IssueNumber);
            var volume = ParseInt(GetElementValue(document, "Volume"));
            var year = ParseInt(GetElementValue(document, "Year")) ?? fallbackMetadata.Year;
            var publisher = Coalesce(GetElementValue(document, "Publisher"));
            var writer = Coalesce(GetElementValue(document, "Writer"));
            var penciller = Coalesce(GetElementValue(document, "Penciller"));
            var inker = Coalesce(GetElementValue(document, "Inker"));
            var colorist = Coalesce(GetElementValue(document, "Colorist"));
            var summary = Coalesce(GetElementValue(document, "Summary"));
            var pageCount = ParseInt(GetElementValue(document, "PageCount"));

            return new ComicMetadata
            {
                FilePath = fallbackMetadata.FilePath,
                FileName = fallbackMetadata.FileName,
                DisplayTitle = BuildDisplayTitle(series, issueNumber, year, fallbackMetadata.DisplayTitle),
                Series = series,
                Title = title,
                IssueNumber = issueNumber,
                Volume = volume,
                Year = year,
                Publisher = publisher,
                Writer = writer,
                Penciller = penciller,
                Inker = inker,
                Colorist = colorist,
                Summary = summary,
                PageCount = pageCount,
                Pages = pages,
                Source = ComicMetadataSource.ComicInfoXml
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetElementValue(XDocument document, string localName)
    {
        return document.Descendants()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static int? ParseInt(string? value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static IReadOnlyList<ComicPageMetadata> ParsePageEntries(XDocument document)
    {
        var pages = new List<ComicPageMetadata>();

        foreach (var pageNode in document.Descendants()
                     .Where(x => string.Equals(x.Name.LocalName, "Page", StringComparison.OrdinalIgnoreCase)))
        {
            var imageRaw = GetAttributeValue(pageNode, "Image");
            if (!int.TryParse(imageRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var imageIndex))
            {
                continue;
            }

            pages.Add(new ComicPageMetadata
            {
                ImageIndex = imageIndex,
                ImageWidth = ParseInt(GetAttributeValue(pageNode, "ImageWidth")),
                ImageHeight = ParseInt(GetAttributeValue(pageNode, "ImageHeight")),
                PageType = Coalesce(GetAttributeValue(pageNode, "Type"))
            });
        }

        return pages
            .OrderBy(x => x.ImageIndex)
            .ToArray();
    }

    private static string? GetAttributeValue(XElement element, string localName)
    {
        return element.Attributes()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string BuildDisplayTitle(
        string? series,
        string? issueNumber,
        int? year,
        string fallbackTitle)
    {
        var baseTitle = string.IsNullOrWhiteSpace(series) ? fallbackTitle : series.Trim();
        if (string.IsNullOrWhiteSpace(baseTitle))
        {
            return fallbackTitle;
        }

        if (!string.IsNullOrWhiteSpace(issueNumber))
        {
            baseTitle = $"{baseTitle} #{issueNumber.Trim()}";
        }

        if (year is not null)
        {
            baseTitle = $"{baseTitle} ({year.Value.ToString(CultureInfo.InvariantCulture)})";
        }

        return baseTitle;
    }

    private static string? Coalesce(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string NormalizePath(string path)
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
}
