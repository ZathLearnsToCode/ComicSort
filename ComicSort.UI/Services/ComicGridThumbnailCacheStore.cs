using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ComicSort.UI.Services;

internal sealed class ComicGridThumbnailCacheStore
{
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, Bitmap> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _cacheOrder = new();
    private readonly Dictionary<string, LinkedListNode<string>> _cacheNodes = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string thumbnailPath, out Bitmap bitmap)
    {
        lock (_cacheLock)
        {
            if (!_bitmapCache.TryGetValue(thumbnailPath, out bitmap!))
            {
                bitmap = null!;
                return false;
            }

            MoveNodeToTail(thumbnailPath);
            return true;
        }
    }

    public IReadOnlyList<Bitmap> Upsert(string thumbnailPath, Bitmap bitmap, int capacity)
    {
        var displaced = new List<Bitmap>();
        lock (_cacheLock)
        {
            if (_bitmapCache.TryGetValue(thumbnailPath, out var existing))
            {
                _bitmapCache[thumbnailPath] = bitmap;
                MoveNodeToTail(thumbnailPath);
                if (!ReferenceEquals(existing, bitmap))
                {
                    displaced.Add(existing);
                }
            }
            else
            {
                AddNewNode(thumbnailPath, bitmap);
            }

            displaced.AddRange(EvictOverflowBitmaps(capacity));
        }

        return displaced.Distinct().ToArray();
    }

    public IReadOnlyList<Bitmap> Flush()
    {
        lock (_cacheLock)
        {
            var cached = _bitmapCache.Values.Distinct().ToList();
            _bitmapCache.Clear();
            _cacheNodes.Clear();
            _cacheOrder.Clear();
            return cached;
        }
    }

    public bool IsCached(Bitmap bitmap)
    {
        lock (_cacheLock)
        {
            return _bitmapCache.Values.Any(value => ReferenceEquals(value, bitmap));
        }
    }

    private void MoveNodeToTail(string key)
    {
        if (_cacheNodes.TryGetValue(key, out var existingNode))
        {
            _cacheOrder.Remove(existingNode);
            _cacheOrder.AddLast(existingNode);
        }
    }

    private void AddNewNode(string key, Bitmap bitmap)
    {
        _cacheNodes[key] = _cacheOrder.AddLast(key);
        _bitmapCache[key] = bitmap;
    }

    private IReadOnlyList<Bitmap> EvictOverflowBitmaps(int capacity)
    {
        var removed = new List<Bitmap>();
        while (_bitmapCache.Count > capacity)
        {
            var first = _cacheOrder.First;
            if (first is null)
            {
                break;
            }

            _cacheOrder.RemoveFirst();
            _cacheNodes.Remove(first.Value);
            if (_bitmapCache.Remove(first.Value, out var evicted))
            {
                removed.Add(evicted);
            }
        }

        return removed;
    }
}
