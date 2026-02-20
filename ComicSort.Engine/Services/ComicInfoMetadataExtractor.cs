using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ComicSort.Engine.Models;
using SharpCompress.Archives;

namespace ComicSort.Engine.Services;

public sealed class ComicInfoMetadataExtractor : IComicMetadataExtractor
{
    public Task<ComicMetadata?> TryExtractAsync(string filePath, CancellationToken ct)
        => Task.Run(() => TryExtractCore(filePath, ct), ct);

    private static ComicMetadata? TryExtractCore(string filePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        var ext = Path.GetExtension(filePath);
        // Only archive-based formats for this micro-step
        if (!ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".cbr", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".cb7", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".rar", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        ct.ThrowIfCancellationRequested();

        using var archive = ArchiveFactory.OpenArchive(filePath);

        // ComicInfo.xml may be inside subfolders; Key is the path inside the archive.
        var entry = archive.Entries
            .FirstOrDefault(e =>
                !e.IsDirectory &&
                e.Key.EndsWith("ComicInfo.xml", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return null;

        ct.ThrowIfCancellationRequested();

        using var s = entry.OpenEntryStream();
        return ParseComicInfoXml(s);
    }

    private static ComicMetadata? ParseComicInfoXml(Stream xmlStream)
    {
        try
        {
            var doc = XDocument.Load(xmlStream);
            var root = doc.Root;
            if (root is null)
                return null;

            // Helper (ComicInfo.xml is typically simple element-only)
            static string? GetString(XElement root, string name)
                => root.Element(name)?.Value?.Trim();

            static int? GetInt(XElement root, string name)
            {
                var s = root.Element(name)?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(s)) return null;
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    return v;
                return null;
            }

            var md = new ComicMetadata
            {
                Series = GetString(root, "Series"),
                Title = GetString(root, "Title"),
                Summary = GetString(root, "Summary"),
                Number = GetString(root, "Number"),
                Volume = GetInt(root, "Volume"),
                Year = GetInt(root, "Year"),
            };

            // If everything is empty, treat as "not useful"
            if (string.IsNullOrWhiteSpace(md.Series) &&
                string.IsNullOrWhiteSpace(md.Title) &&
                string.IsNullOrWhiteSpace(md.Summary) &&
                string.IsNullOrWhiteSpace(md.Number) &&
                md.Volume is null &&
                md.Year is null)
            {
                return null;
            }

            return md;
        }
        catch
        {
            // For now: treat invalid ComicInfo.xml as "no metadata"
            return null;
        }
    }
}
