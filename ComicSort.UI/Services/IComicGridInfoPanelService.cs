using ComicSort.UI.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.Services;

public interface IComicGridInfoPanelService
{
    Task<ComicGridInfoPanelLoadResult> LoadAsync(
        ComicTileModel tile,
        CancellationToken cancellationToken = default);
}

public sealed class ComicGridInfoPanelLoadResult
{
    public ComicInfoPanelModel Panel { get; init; } = new();

    public string? ErrorMessage { get; init; }
}
