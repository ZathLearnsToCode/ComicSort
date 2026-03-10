using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using ComicSort.UI.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.Services;

public sealed class ComicGridInfoPanelService : IComicGridInfoPanelService
{
    private readonly IComicMetadataService _comicMetadataService;

    public ComicGridInfoPanelService(IComicMetadataService comicMetadataService)
    {
        _comicMetadataService = comicMetadataService;
    }

    public async Task<ComicGridInfoPanelLoadResult> LoadAsync(
        ComicTileModel tile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _comicMetadataService.GetMetadataAsync(tile.FilePath, cancellationToken);
            return new ComicGridInfoPanelLoadResult
            {
                Panel = ComicInfoPanelModel.From(tile, metadata)
            };
        }
        catch (Exception ex)
        {
            return new ComicGridInfoPanelLoadResult
            {
                Panel = BuildFallbackPanel(tile),
                ErrorMessage = $"Unable to load metadata: {ex.Message}"
            };
        }
    }

    private static ComicInfoPanelModel BuildFallbackPanel(ComicTileModel tile)
    {
        return ComicInfoPanelModel.From(tile, new ComicMetadata
        {
            FilePath = tile.FilePath,
            FileName = Path.GetFileName(tile.FilePath),
            DisplayTitle = tile.DisplayTitle,
            Title = tile.DisplayTitle,
            Series = tile.Series,
            Publisher = tile.Publisher,
            Source = ComicMetadataSource.FileNameFallback
        });
    }
}
