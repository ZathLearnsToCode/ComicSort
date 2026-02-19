using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ComicSort.UI.UI_Services
{
    public sealed class ScanQueueService : IAsyncDisposable
    {
        private readonly LibraryService _library;
        private readonly string _libraryPath;

        private readonly Channel<ScanRequest> _queue =
            Channel.CreateUnbounded<ScanRequest>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        private readonly CancellationTokenSource _shutdown = new();
        private CancellationTokenSource? _currentScanCts;

        public bool IsWorkerRunning => _worker is not null;
        private Task? _worker;

        private int _pendingCount;

        public event Action<string>? OnStatus;                    // e.g. "Queued", "Started"
        public event Action<ScanProgress>? OnProgress;            // progress updates
        public event Action<int>? OnScanCompletedAddedCount;      // added count
        public event Action<Exception>? OnError;
        public event Action<int>? OnQueueCountChanged;
        public event Action<string?>? OnCurrentFolderChanged;

        public ScanQueueService(LibraryService library, string libraryPath)
        {
            _library = library;
            _libraryPath = libraryPath;
        }

        public void Start()
        {
            _worker ??= Task.Run(WorkerLoopAsync);
        }

        public async ValueTask EnqueueAsync(ScanRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.FolderPath))
                throw new ArgumentException("FolderPath is required.", nameof(request));

            await _queue.Writer.WriteAsync(request);
            Interlocked.Increment(ref _pendingCount);

            OnStatus?.Invoke($"Queued: {request.FolderPath}");
            OnQueueCountChanged?.Invoke(GetApproxQueueCount());
        }

        private int GetApproxQueueCount()
        {
            return Volatile.Read(ref _pendingCount);
        }

        public void CancelCurrent()
        {
            _currentScanCts?.Cancel();
        }

        public void ClearQueue()
        {
            int drained = 0;

            while (_queue.Reader.TryRead(out _))
                drained++;

            if (drained != 0)
                Interlocked.Add(ref _pendingCount, -drained);

            OnStatus?.Invoke("Queue cleared.");
            OnQueueCountChanged?.Invoke(GetApproxQueueCount());
        }

        private async Task WorkerLoopAsync()
        {
            try
            {
                while (await _queue.Reader.WaitToReadAsync(_shutdown.Token))
                {
                    while (_queue.Reader.TryRead(out var req))
                    {
                        if (_shutdown.IsCancellationRequested)
                            return;

                        Interlocked.Decrement(ref _pendingCount);
                        OnQueueCountChanged?.Invoke(GetApproxQueueCount());
                        OnCurrentFolderChanged?.Invoke(req.FolderPath);
                                               

                        _currentScanCts?.Dispose();
                        _currentScanCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);

                        var sw = Stopwatch.StartNew();
                        long lastReportMs = 0;

                        var progress = new Progress<ScanProgress>(p =>
                        {
                            // Always forward the final progress (CurrentFile == null),
                            // otherwise throttle to avoid UI message-queue floods.
                            var now = sw.ElapsedMilliseconds;

                            bool isFinal = p.CurrentFile is null;
                            bool timeElapsed = (now - lastReportMs) >= 150;      // update at most ~6-7 times/sec
                            bool everyN = (p.Processed % 100) == 0;              // or every 100 files

                            if (isFinal || timeElapsed || everyN)
                            {
                                lastReportMs = now;
                                OnProgress?.Invoke(p);
                            }
                        });

                        OnStatus?.Invoke($"Scanning: {req.FolderPath}");

                        try
                        {
                            int added = await _library.ScanAndAddAsync(
                                req.FolderPath,
                                _libraryPath,
                                progress,
                                _currentScanCts.Token);

                            OnScanCompletedAddedCount?.Invoke(added);
                            OnStatus?.Invoke($"Scan complete: {req.FolderPath}");
                            OnCurrentFolderChanged?.Invoke(null);
                        }
                        catch (OperationCanceledException)
                        {
                            OnStatus?.Invoke($"Cancelled: {req.FolderPath}");
                            OnCurrentFolderChanged?.Invoke(null);
                        }
                        catch (Exception ex)
                        {
                            OnError?.Invoke(ex);
                            OnStatus?.Invoke($"Error scanning: {req.FolderPath}");
                            OnCurrentFolderChanged?.Invoke(null);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        public async ValueTask DisposeAsync()
        {
            _shutdown.Cancel();
            _queue.Writer.TryComplete();
            _currentScanCts?.Cancel();
            _currentScanCts?.Dispose();
            if (_worker is not null)
            {
                try { await _worker; } catch { }
            }
            _shutdown.Dispose();
        }
    }
}
