using ComicSort.Engine.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ComicSort.UI.Services;

public sealed class ComicGridSavedItemQueueService
{
    private readonly object _syncLock = new();
    private readonly List<ComicLibraryItem> _pendingSavedItems = [];
    private readonly HashSet<string> _pendingRemovedPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _drainScheduled;

    public bool EnqueueSaved(ComicLibraryItem item)
    {
        lock (_syncLock)
        {
            _pendingSavedItems.Add(item);
            return EnsureDrainScheduled();
        }
    }

    public bool EnqueueRemoved(string filePath)
    {
        lock (_syncLock)
        {
            _pendingRemovedPaths.Add(filePath);
            return EnsureDrainScheduled();
        }
    }

    public SavedItemsSnapshot TakeSnapshot()
    {
        lock (_syncLock)
        {
            var items = _pendingSavedItems.OrderBy(x => x.SequenceNumber).ToArray();
            var removedPaths = _pendingRemovedPaths.ToArray();
            _pendingSavedItems.Clear();
            _pendingRemovedPaths.Clear();
            _drainScheduled = false;
            return new SavedItemsSnapshot(items, removedPaths);
        }
    }

    public bool ScheduleDrainIfPending()
    {
        lock (_syncLock)
        {
            if ((_pendingSavedItems.Count == 0 && _pendingRemovedPaths.Count == 0) || _drainScheduled)
            {
                return false;
            }

            _drainScheduled = true;
            return true;
        }
    }

    private bool EnsureDrainScheduled()
    {
        if (_drainScheduled)
        {
            return false;
        }

        _drainScheduled = true;
        return true;
    }
}

public sealed class SavedItemsSnapshot
{
    public SavedItemsSnapshot(IReadOnlyList<ComicLibraryItem> items, IReadOnlyList<string> removedPaths)
    {
        Items = items;
        RemovedPaths = removedPaths;
    }

    public IReadOnlyList<ComicLibraryItem> Items { get; }

    public IReadOnlyList<string> RemovedPaths { get; }

    public bool IsEmpty => Items.Count == 0 && RemovedPaths.Count == 0;
}
