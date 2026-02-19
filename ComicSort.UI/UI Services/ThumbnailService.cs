using Avalonia.Media.Imaging;
using ComicSort.Engine.Services;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.UI_Services;

public sealed class ThumbnailService : IThumbnailService
{
    private readonly CoverStreamService _cover;
    private readonly ThumbnailGenerator _gen;

    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _inflight = new();
    private readonly SemaphoreSlim _throttle = new(initialCount: 2, maxCount: 2);

    public ThumbnailService(CoverStreamService cover, ThumbnailGenerator gen)
    {
        _cover = cover;
        _gen = gen;
    }

    public async Task<Bitmap?> GetOrCreateAsync(string comicPath, int targetHeight, CancellationToken ct)
    {
        var cachePath = GetCacheFilePath(comicPath, targetHeight);

        if (!File.Exists(cachePath))
        {
            var lazy = _inflight.GetOrAdd(cachePath, _ =>
                new Lazy<Task<string?>>(() => CreateAsync(comicPath, cachePath, targetHeight, ct)));

            try { await lazy.Value; }
            finally { _inflight.TryRemove(cachePath, out _); }
        }

        if (!File.Exists(cachePath))
            return null;

        // Avoid holding file locks: load into memory
        await using var fs = File.OpenRead(cachePath);
        var ms = new MemoryStream();
        await fs.CopyToAsync(ms, ct);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    private async Task<string?> CreateAsync(string comicPath, string cachePath, int targetHeight, CancellationToken ct)
    {
        await _throttle.WaitAsync(ct);
        try
        {
            if (File.Exists(cachePath))
                return cachePath;

            using var imgStream = _cover.TryOpenFirstImageEntry(comicPath, ct);
            if (imgStream is null)
                return null;

            var ok = await _gen.TryGenerateJpegAsync(imgStream, cachePath, targetHeight, ct);
            return ok ? cachePath : null;
        }
        finally
        {
            _throttle.Release();
        }
    }

    private static string GetCacheFilePath(string comicPath, int targetHeight)
    {
        var fi = new FileInfo(comicPath);
        var key = $"{comicPath}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}|h={targetHeight}";
        var hash = Sha1Hex(key);

        var folder = AppPaths.GetThumbCacheFolder();
        return Path.Combine(folder, hash + ".jpg");
    }

    private static string Sha1Hex(string s)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
