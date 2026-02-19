using SharpCompress.Archives;
using SharpCompress.Common;

namespace ComicSort.Engine.Services;

public sealed class CoverStreamService
{
    private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public Stream? TryOpenFirstImageEntry(string comicFilePath, CancellationToken ct)
    {
        // SharpCompress opens archive and lets us stream entries without extracting
        using var archive = ArchiveFactory.OpenArchive(comicFilePath);

        // Pick first “real” image entry in a stable order
        var entry = archive.Entries
            .Where(e => !e.IsDirectory)
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(e => ImageExts.Contains(Path.GetExtension(e.Key)));

        if (entry is null)
            return null;

        ct.ThrowIfCancellationRequested();
        // IMPORTANT: return a stream the caller will copy; archive disposed after this method
        // So we must COPY to a MemoryStream here.
        using var entryStream = entry.OpenEntryStream();
        var ms = new MemoryStream();
        entryStream.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }
}
