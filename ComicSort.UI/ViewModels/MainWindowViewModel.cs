using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using ComicSort.UI.UI_Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly LibraryService _library;
        private readonly SearchEngine _searchEngine;
        private readonly string _libraryPath;
        private readonly ScanQueueService _scanQueue;
        private readonly LibraryIndex _index;
        private readonly IDialogServices _dialogServices;
        private readonly IThumbnailService _thumbnails;

        private readonly SearchController<ComicBook[]> _searchController;

        // ======== UI-bound SNAPSHOT (stable array) ========
        private ComicItemViewModel[] _items = Array.Empty<ComicItemViewModel>();
        public IReadOnlyList<ComicItemViewModel> Items => _items;

        // Reuse VM instances so already-loaded thumbs stay
        private readonly Dictionary<string, ComicItemViewModel> _itemCacheByPath = new(StringComparer.OrdinalIgnoreCase);

        // ======== Search UI ========
        [ObservableProperty] private string query = "";
        [ObservableProperty] private int resultsCount;
        [ObservableProperty] private bool isSearching;
        [ObservableProperty] private int lastSearchMs;

        public IReadOnlyList<string> ExtensionOptions { get; } =
            new[] { "All", ".cbr", ".cbz", ".pdf", ".cb7", ".webp" };

        [ObservableProperty] private string extensionFilter = "All";

        public IReadOnlyList<string> SortOptions { get; } =
            new[] { "Name", "AddedOn", "Size" };

        [ObservableProperty] private string sortMode = "Name";
        [ObservableProperty] private bool sortDescending;

        [ObservableProperty] private ComicItemViewModel? selectedItem;
        public ComicBook? SelectedBook => SelectedItem?.Book;

        // ======== Scan UI ========
        [ObservableProperty] private bool isScanning;
        [ObservableProperty] private int scanProcessed;
        [ObservableProperty] private int scanAdded;
        [ObservableProperty] private string? scanCurrentFile;
        [ObservableProperty] private int scanQueueCount;
        [ObservableProperty] private string? scanCurrentFolder;

        [ObservableProperty] private string statusText = "Ready";

        [ObservableProperty] private Bitmap? selectedThumbnail;

        private readonly ThumbnailCacheService _thumbs = new();
        private CancellationTokenSource? _thumbCts;

        [ObservableProperty] private Bitmap? selectedCover;
        private CancellationTokenSource? _selectedCoverCts;


        // We track pending queue count ourselves (Channel doesn't expose Count)
        private int _pendingQueue;

        [RelayCommand]
        private async Task AddFolder()
        {
            var folders = await _dialogServices.ShowOpenFolderDialogAsync("Select a Folder...");
            if (string.IsNullOrWhiteSpace(folders))
                return;

            await EnqueueFolderScanAsync(folders);
        }

        [RelayCommand]
        private void Cancel()
        {
            CancelScan();
        }

        [RelayCommand]
        private void ClearQueue()
        {
            ClearScanQueue();
        }

        public MainWindowViewModel(
            IDialogServices dialogServices,
            LibraryService library,
            LibraryIndex index,
            SearchEngine searchEngine,
            ScanQueueService scanQueue,
            IThumbnailService thumbnails)
        {
            _dialogServices = dialogServices;
            _library = library;
            _index = index;
            _searchEngine = searchEngine;
            _scanQueue = scanQueue;
            _thumbnails = thumbnails;

            _libraryPath = AppPaths.GetLibraryJsonPath();

            // Search controller (Step C)
            _searchController = new SearchController<ComicBook[]>(
                dispatchToUi: action => Dispatcher.UIThread.Post(action),
                debounce: TimeSpan.FromMilliseconds(250),
                showSearchingDelay: TimeSpan.FromMilliseconds(150));

            // ---- Scan queue wiring ----

            _scanQueue.OnStatus += msg =>
                Dispatcher.UIThread.Post(() =>
                {
                    StatusText = msg;

                    if (msg.StartsWith("Queued:", StringComparison.OrdinalIgnoreCase))
                    {
                        _pendingQueue++;
                        ScanQueueCount = _pendingQueue;
                    }
                    else if (msg.StartsWith("Scanning:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_pendingQueue > 0) _pendingQueue--;
                        ScanQueueCount = _pendingQueue;

                        ScanCurrentFolder = msg.Substring("Scanning:".Length).Trim();
                        IsScanning = true;
                        ScanCurrentFile = null;
                        ScanProcessed = 0;
                        ScanAdded = 0;
                    }
                    else if (msg.StartsWith("Scan complete:", StringComparison.OrdinalIgnoreCase) ||
                             msg.StartsWith("Cancelled:", StringComparison.OrdinalIgnoreCase) ||
                             msg.StartsWith("Error scanning:", StringComparison.OrdinalIgnoreCase))
                    {
                        ScanCurrentFolder = null;
                        IsScanning = false;
                        ScanCurrentFile = null;
                    }
                });

            _scanQueue.OnProgress += p =>
                Dispatcher.UIThread.Post(() =>
                {
                    IsScanning = true;
                    ScanProcessed = p.Processed;
                    ScanAdded = p.Added;
                    ScanCurrentFile = p.CurrentFile;
                });

            // IMPORTANT: Use InvokeAsync for async UI work (not Post(async ...))
            _scanQueue.OnScanCompletedAddedCount += _ =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    // Incremental index update
                    if (_library.Books.Count > _index.IndexedCount)
                    {
                        var newBooks = _library.Books
                            .Skip(_index.IndexedCount)
                            .ToArray();

                        _index.Append(newBooks);
                    }

                    await RequestSearchAsync(Query, CancellationToken.None);
                    ScanCurrentFile = null;
                });
            };

            _scanQueue.OnError += ex =>
                Dispatcher.UIThread.Post(() => StatusText = $"Error: {ex.Message}");

            _scanQueue.Start();
            
        }

        // ======== Startup ========
        public async Task InitializeAsync()
        {
            StatusText = "Loading library...";
            await _library.LoadAsync(_libraryPath);

            _index.Rebuild(_library.Books);

            
            // Show all initially
            var books = _library.Books;

            var mapped = new ComicItemViewModel[books.Count];

            for (int i = 0; i < books.Count; i++)
            {
                var book = books[i];

                if (!_itemCacheByPath.TryGetValue(book.FilePath, out var vm))
                {
                    vm = new ComicItemViewModel(book);
                    _itemCacheByPath[book.FilePath] = vm;
                }

                mapped[i] = vm;
            }

            _items = mapped;
            ResultsCount = _items.Length;
            OnPropertyChanged(nameof(Items));

            StatusText = $"Loaded {_index.Books.Length} books";

        }

        // ======== Scan actions ========
        public async Task EnqueueFolderScanAsync(string folderPath)
        {
            await _scanQueue.EnqueueAsync(new ScanRequest
            {
                FolderPath = folderPath,
                Recursive = true
            });
        }

        public void CancelScan() => _scanQueue.CancelCurrent();

        public void ClearScanQueue()
        {
            _scanQueue.ClearQueue();
            _pendingQueue = 0;
            ScanQueueCount = 0;
        }

        // ======== Search plumbing ========
        partial void OnQueryChanged(string value) => _ = RequestSearchAsync(value, CancellationToken.None);
        partial void OnExtensionFilterChanged(string value) => _ = RequestSearchAsync(Query, CancellationToken.None);
        partial void OnSortModeChanged(string value) => _ = RequestSearchAsync(Query, CancellationToken.None);
        partial void OnSortDescendingChanged(bool value) => _ = RequestSearchAsync(Query, CancellationToken.None);

        private Task RequestSearchAsync(string value, CancellationToken externalCt)
        {
            return _searchController.RequestAsync(
                runAsync: ct => Task.Run(() =>
                    _searchEngine.Search(
                        _index.Books,
                        _index.NameBlobsLower,
                        value,
                        ExtensionFilter,
                        SortMode,
                        SortDescending,
                        ct), ct),

                setSearching: b => IsSearching = b,
                setElapsedMs: ms => LastSearchMs = ms,
                publishResults: results =>
                {
                    var mapped = new ComicItemViewModel[results.Length];
                    for (int i = 0; i < results.Length; i++)
                    {
                        var b = results[i];
                        if (!_itemCacheByPath.TryGetValue(b.FilePath, out var vm))
                        {
                            vm = new ComicItemViewModel(b);
                            _itemCacheByPath[b.FilePath] = vm;
                        }
                        mapped[i] = vm;
                    }

                    _items = mapped;
                    ResultsCount = _items.Length;
                    OnPropertyChanged(nameof(Items));

                    // Keep selection sane if filtered out
                    if (SelectedItem is not null && Array.IndexOf(_items, SelectedItem) < 0)
                        SelectedItem = null;
                },

                externalCt: externalCt);
        }

        partial void OnSelectedItemChanged(ComicItemViewModel? value)
        {
            OnPropertyChanged(nameof(SelectedBook));

            _selectedCoverCts?.Cancel();
            _selectedCoverCts?.Dispose();
            _selectedCoverCts = null;

            SelectedCover = null;

            if (value is null)
                return;

            _selectedCoverCts = new CancellationTokenSource();
            var ct = _selectedCoverCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    var bmp = await _thumbnails.GetOrCreateAsync(value.Book.FilePath, targetHeight: 260, ct);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!ct.IsCancellationRequested)
                            SelectedCover = bmp;
                    });
                }
                catch (OperationCanceledException) { }
                catch { /* ignore for now */ }
            }, ct);
        }


        public void RequestThumbnailForRow(ComicItemViewModel row)
        {
            if (row.IsThumbnailRequested || row.Thumbnail is not null)
                return;

            row.IsThumbnailRequested = true;

            _ = Task.Run(async () =>
            {
                try
                {
                    var bmp = await _thumbnails.GetOrCreateAsync(row.Book.FilePath, targetHeight: 56, CancellationToken.None);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        row.Thumbnail = bmp;
                    });
                }
                catch
                {
                    // ignore thumb failures for now
                }
            });
        }


    }

}

