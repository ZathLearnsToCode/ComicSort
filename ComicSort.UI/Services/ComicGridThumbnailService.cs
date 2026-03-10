using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ComicSort.UI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ComicSort.UI.Services;

public sealed class ComicGridThumbnailService : IComicGridThumbnailService
{
    private const int ThumbnailCacheCapacity = 512;
    private readonly ComicGridThumbnailCacheStore _cacheStore = new();

    public void ApplyThumbnail(ComicTileModel tile, string? thumbnailPath, IReadOnlyList<ComicTileModel> items)
    {
        tile.ThumbnailPath = thumbnailPath;
        tile.ThumbnailVersion++;
        if (!CanUseThumbnail(tile, thumbnailPath))
        {
            SetTileImage(tile, null, items);
            return;
        }

        if (TryGetCachedBitmap(thumbnailPath!, out var bitmap))
        {
            SetTileImage(tile, bitmap, items);
            return;
        }

        SetTileImage(tile, null, items);
        _ = LoadAndAssignAsync(tile, thumbnailPath!, tile.ThumbnailVersion, items);
    }

    public void ReleaseTileThumbnail(ComicTileModel tile, IReadOnlyList<ComicTileModel> items)
    {
        SetTileImage(tile, null, items);
    }

    public void Clear(IReadOnlyList<ComicTileModel> items)
    {
        foreach (var tile in items)
        {
            tile.ThumbnailImage = null;
        }

        foreach (var bitmap in _cacheStore.Flush())
        {
            ReleaseBitmapIfUnused(bitmap, items);
        }
    }

    private static bool CanUseThumbnail(ComicTileModel tile, string? thumbnailPath)
    {
        return !string.IsNullOrWhiteSpace(thumbnailPath) && tile.IsThumbnailReady;
    }

    private bool TryGetCachedBitmap(string thumbnailPath, out Bitmap bitmap)
    {
        return _cacheStore.TryGet(thumbnailPath, out bitmap);
    }

    private async Task LoadAndAssignAsync(ComicTileModel tile, string thumbnailPath, int version, IReadOnlyList<ComicTileModel> items)
    {
        var bitmap = await TryLoadBitmapAsync(thumbnailPath);
        if (bitmap is null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!IsTileVersionCurrent(tile, version, thumbnailPath))
            {
                bitmap.Dispose();
                return;
            }

            CacheBitmap(thumbnailPath, bitmap, items);
            SetTileImage(tile, bitmap, items);
        });
    }

    private static bool IsTileVersionCurrent(ComicTileModel tile, int version, string thumbnailPath)
    {
        return tile.ThumbnailVersion == version &&
               string.Equals(tile.ThumbnailPath, thumbnailPath, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Bitmap?> TryLoadBitmapAsync(string thumbnailPath)
    {
        try
        {
            return await Task.Run(() => new Bitmap(thumbnailPath));
        }
        catch
        {
            return null;
        }
    }

    private void CacheBitmap(string thumbnailPath, Bitmap bitmap, IReadOnlyList<ComicTileModel> items)
    {
        var displaced = _cacheStore.Upsert(thumbnailPath, bitmap, ThumbnailCacheCapacity);
        foreach (var stale in displaced)
        {
            ReleaseBitmapIfUnused(stale, items);
        }
    }

    private void SetTileImage(ComicTileModel tile, Bitmap? bitmap, IReadOnlyList<ComicTileModel> items)
    {
        if (ReferenceEquals(tile.ThumbnailImage, bitmap))
        {
            return;
        }

        var previous = tile.ThumbnailImage;
        tile.ThumbnailImage = bitmap;
        ReleaseBitmapIfUnused(previous, items);
    }

    private void ReleaseBitmapIfUnused(Bitmap? bitmap, IReadOnlyList<ComicTileModel> items)
    {
        if (bitmap is null || IsReferencedByTile(bitmap, items) || IsCached(bitmap))
        {
            return;
        }

        bitmap.Dispose();
    }

    private static bool IsReferencedByTile(Bitmap bitmap, IEnumerable<ComicTileModel> items)
    {
        return items.Any(tile => ReferenceEquals(tile.ThumbnailImage, bitmap));
    }

    private bool IsCached(Bitmap bitmap)
    {
        return _cacheStore.IsCached(bitmap);
    }
}
