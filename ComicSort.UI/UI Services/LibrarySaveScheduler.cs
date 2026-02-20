using ComicSort.Engine.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.UI_Services;

public sealed class LibrarySaveScheduler : ILibrarySaveScheduler, IDisposable
{
    private readonly LibraryService _library;
    private readonly string _libraryPath;

    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public LibrarySaveScheduler(LibraryService library)
    {
        _library = library;
        _libraryPath = AppPaths.GetLibraryJsonPath();
    }

    public void RequestSave()
    {
        lock (_gate)
        {
            if (_disposed) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            var ct = _cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    // debounce window
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);

                    // Do the save once things are quiet
                    await _library.SaveAsync(_libraryPath);
                }
                catch (OperationCanceledException) { }
                catch
                {
                    // ignore for now; later we can surface StatusText
                }
            }, ct);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
