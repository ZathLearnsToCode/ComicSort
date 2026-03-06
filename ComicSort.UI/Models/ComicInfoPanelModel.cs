using Avalonia.Media.Imaging;
using ComicSort.Engine.Models;
using System;
using System.Globalization;

namespace ComicSort.UI.Models;

public sealed class ComicInfoPanelModel
{
    public string DisplayTitle { get; set; } = string.Empty;

    public string MetadataSource { get; set; } = string.Empty;

    public string Series { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string IssueNumber { get; set; } = string.Empty;

    public string Volume { get; set; } = string.Empty;

    public string Year { get; set; } = string.Empty;

    public string Publisher { get; set; } = string.Empty;

    public string Writer { get; set; } = string.Empty;

    public string Penciller { get; set; } = string.Empty;

    public string Inker { get; set; } = string.Empty;

    public string Colorist { get; set; } = string.Empty;

    public string PageCount { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string LastScanned { get; set; } = string.Empty;

    public Bitmap? CoverImage { get; set; }

    public static ComicInfoPanelModel From(ComicTileModel tile, ComicMetadata metadata)
    {
        return new ComicInfoPanelModel
        {
            DisplayTitle = Display(metadata.DisplayTitle, tile.DisplayTitle),
            MetadataSource = metadata.Source == ComicMetadataSource.ComicInfoXml
                ? "Source: ComicInfo.xml"
                : "Source: File name fallback",
            Series = Display(metadata.Series),
            Title = Display(metadata.Title),
            IssueNumber = Display(metadata.IssueNumber),
            Volume = metadata.Volume?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            Year = metadata.Year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            Publisher = Display(metadata.Publisher),
            Writer = Display(metadata.Writer),
            Penciller = Display(metadata.Penciller),
            Inker = Display(metadata.Inker),
            Colorist = Display(metadata.Colorist),
            PageCount = metadata.PageCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            Summary = Display(metadata.Summary),
            FilePath = Display(tile.FilePath),
            FileType = Display(tile.FileTypeTag),
            LastScanned = tile.LastScannedUtc == DateTimeOffset.MinValue
                ? string.Empty
                : tile.LastScannedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            CoverImage = tile.ThumbnailImage
        };
    }

    private static string Display(string? value, string fallback = "")
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var trimmed = value.Trim();
            if (string.Equals(trimmed, "Unspecified", StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }

            return trimmed;
        }

        return fallback;
    }
}
