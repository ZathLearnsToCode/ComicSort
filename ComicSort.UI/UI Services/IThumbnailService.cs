using Avalonia.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.UI_Services;

public interface IThumbnailService
{
    Task<Bitmap?> GetOrCreateAsync(string comicPath, int targetHeight, CancellationToken ct);
}
