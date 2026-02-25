using ComicSort.Engine.Models;
using SkiaSharp;
using System.Security.Cryptography;
using System.Text;

namespace ComicSort.Engine.Services;

public sealed class ThumbnailCacheService : IThumbnailCacheService
{
    private readonly ISettingsService _settingsService;

    public ThumbnailCacheService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string BaseDirectory => _settingsService.CurrentSettings.ThumbnailCacheDirectory;

    public string ComputeKey(string normalizedPath, string fingerprint)
    {
        var input = $"{normalizedPath}|{fingerprint}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool TryGetCachedPath(string key, out string cachedPath)
    {
        cachedPath = GetExpectedPath(key);
        return File.Exists(cachedPath);
    }

    public string GetExpectedPath(string key)
    {
        var root = BaseDirectory;
        var shard1 = key.Length >= 2 ? key[..2] : "00";
        var shard2 = key.Length >= 4 ? key.Substring(2, 2) : "00";
        return Path.Combine(root, shard1, shard2, $"{key}.jpg");
    }

    public async Task<ThumbnailWriteResult> WriteThumbnailAsync(
        string key,
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var outputPath = GetExpectedPath(key);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            using var sourceBitmap = SKBitmap.Decode(imageBytes);
            if (sourceBitmap is null)
            {
                return new ThumbnailWriteResult
                {
                    Success = false,
                    Error = "Unable to decode image bytes into bitmap."
                };
            }

            using var targetSurface = SKSurface.Create(new SKImageInfo(92, 132, SKColorType.Bgra8888, SKAlphaType.Premul));
            if (targetSurface is null)
            {
                return new ThumbnailWriteResult
                {
                    Success = false,
                    Error = "Unable to allocate thumbnail surface."
                };
            }

            var canvas = targetSurface.Canvas;
            canvas.Clear(SKColors.Black);

            var sourceRect = new SKRect(0, 0, sourceBitmap.Width, sourceBitmap.Height);
            var destinationRect = new SKRect(0, 0, 92, 132);
            canvas.DrawBitmap(sourceBitmap, sourceRect, destinationRect);

            using var image = targetSurface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality: 85);
            if (encoded is null)
            {
                return new ThumbnailWriteResult
                {
                    Success = false,
                    Error = "Unable to encode thumbnail image."
                };
            }

            await using var stream = File.Create(outputPath);
            encoded.SaveTo(stream);
            await stream.FlushAsync(cancellationToken);

            return new ThumbnailWriteResult
            {
                Success = true,
                ThumbnailPath = outputPath
            };
        }
        catch (Exception ex)
        {
            return new ThumbnailWriteResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}
