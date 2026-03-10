using ComicSort.Engine.Models;
using ComicSort.UI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ComicSort.UI.Services;

public sealed class ComicGridConversionResultService
{
    public bool Apply(
        IReadOnlyList<ComicTileModel> conversionTargets,
        CbzConversionBatchResult conversionResult,
        IDictionary<string, ComicTileModel> itemIndex,
        IList<ComicTileModel> items)
    {
        var hadChanges = false;
        foreach (var convertedFile in conversionResult.Files.Where(x => x.Success && x.DestinationPath is not null))
        {
            var sourceTile = conversionTargets.FirstOrDefault(x =>
                string.Equals(x.FilePath, convertedFile.SourcePath, StringComparison.OrdinalIgnoreCase));
            if (sourceTile is null)
            {
                continue;
            }

            hadChanges |= ApplySuccess(sourceTile, convertedFile.DestinationPath!, convertedFile.OriginalRemoved, itemIndex, items);
        }

        return hadChanges;
    }

    private static bool ApplySuccess(
        ComicTileModel sourceTile,
        string destinationPath,
        bool originalRemoved,
        IDictionary<string, ComicTileModel> itemIndex,
        IList<ComicTileModel> items)
    {
        var normalizedDestinationPath = Path.GetFullPath(destinationPath).Trim();
        var destinationDirectory = Path.GetDirectoryName(normalizedDestinationPath) ?? string.Empty;
        if (originalRemoved)
        {
            itemIndex.Remove(sourceTile.FilePath);
            sourceTile.FilePath = normalizedDestinationPath;
            sourceTile.FileDirectory = destinationDirectory;
            sourceTile.DisplayTitle = Path.GetFileNameWithoutExtension(normalizedDestinationPath);
            sourceTile.FileTypeTag = "CBZ";
            sourceTile.LastScannedUtc = DateTimeOffset.UtcNow;
            itemIndex[normalizedDestinationPath] = sourceTile;
            return true;
        }

        if (itemIndex.ContainsKey(normalizedDestinationPath))
        {
            return false;
        }

        var duplicateTile = new ComicTileModel
        {
            FilePath = normalizedDestinationPath,
            FileDirectory = destinationDirectory,
            DisplayTitle = Path.GetFileNameWithoutExtension(normalizedDestinationPath),
            Series = sourceTile.Series,
            Publisher = sourceTile.Publisher,
            ThumbnailPath = sourceTile.ThumbnailPath,
            ThumbnailImage = sourceTile.ThumbnailImage,
            IsThumbnailReady = sourceTile.IsThumbnailReady,
            FileTypeTag = "CBZ",
            LastScannedUtc = DateTimeOffset.UtcNow
        };

        items.Insert(0, duplicateTile);
        itemIndex[normalizedDestinationPath] = duplicateTile;
        return true;
    }
}
