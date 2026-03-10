using ComicSort.UI.Models;
using System.Collections.Generic;

namespace ComicSort.UI.Services;

public interface IComicGridThumbnailService
{
    void ApplyThumbnail(ComicTileModel tile, string? thumbnailPath, IReadOnlyList<ComicTileModel> items);

    void ReleaseTileThumbnail(ComicTileModel tile, IReadOnlyList<ComicTileModel> items);

    void Clear(IReadOnlyList<ComicTileModel> items);
}
