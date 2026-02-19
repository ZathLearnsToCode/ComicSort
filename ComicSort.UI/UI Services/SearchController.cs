using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.UI_Services
{
    /// <summary>
    /// Debounces search requests, cancels prior searches, ensures "latest wins",
    /// and provides optional delayed "Searching..." indicator + timing.
    /// 
    /// Uses a simple dispatcher: Action<Action> (e.g. Dispatcher.UIThread.Post).
    /// </summary>
    public sealed class SearchController<TResult> : IDisposable
    {
        private readonly Action<Action> _dispatchToUi;
        private readonly TimeSpan _debounce;
        private readonly TimeSpan _showSearchingDelay;

        private CancellationTokenSource? _cts;
        private int _sequence;

        public SearchController(
            Action<Action> dispatchToUi,
            TimeSpan debounce,
            TimeSpan showSearchingDelay)
        {
            _dispatchToUi = dispatchToUi ?? throw new ArgumentNullException(nameof(dispatchToUi));
            _debounce = debounce;
            _showSearchingDelay = showSearchingDelay;
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            _cts?.Dispose();
            _cts = null;
        }

        public async Task RequestAsync(
            Func<CancellationToken, Task<TResult>> runAsync,
            Action<bool>? setSearching,
            Action<int>? setElapsedMs,
            Action<TResult>? publishResults,
            CancellationToken externalCt = default)
        {
            if (runAsync is null) throw new ArgumentNullException(nameof(runAsync));

            // Cancel previous
            _cts?.Cancel();
            _cts?.Dispose();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _cts.Token;

            int mySeq = Interlocked.Increment(ref _sequence);

            try
            {
                // Debounce
                await Task.Delay(_debounce, ct).ConfigureAwait(false);

                // Show "Searching..." only if still running after delay (prevents flicker)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(_showSearchingDelay, ct).ConfigureAwait(false);
                        if (!ct.IsCancellationRequested && mySeq == Volatile.Read(ref _sequence))
                        {
                            if (setSearching is not null)
                                _dispatchToUi(() => setSearching(true));
                        }
                    }
                    catch (OperationCanceledException) { }
                }, CancellationToken.None);

                var sw = Stopwatch.StartNew();

                var result = await runAsync(ct).ConfigureAwait(false);

                sw.Stop();

                if (ct.IsCancellationRequested || mySeq != Volatile.Read(ref _sequence))
                    return;

                _dispatchToUi(() =>
                {
                    publishResults?.Invoke(result);
                    setElapsedMs?.Invoke((int)sw.ElapsedMilliseconds);
                    setSearching?.Invoke(false);
                });
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
        }
    }
}
