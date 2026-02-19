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
        private readonly LibraryService _library = new();
        private readonly SearchEngine _searchEngine = new();
        private readonly string _libraryPath = AppPaths.GetLibraryJsonPath();
        private readonly ScanQueueService _scanQueue;
        private readonly LibraryIndex _index = new();
        private readonly IDialogServices _dialogServices;
        

        private readonly SearchController<ComicBook[]> _searchController;

        // ======== UI-bound SNAPSHOT (stable array) ========
        private ComicBook[] _items = Array.Empty<ComicBook>();
        public IReadOnlyList<ComicBook> Items => _items;

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

        [ObservableProperty] private ComicBook? selectedBook;

        // ======== Scan UI ========
        [ObservableProperty] private bool isScanning;
        [ObservableProperty] private int scanProcessed;
        [ObservableProperty] private int scanAdded;
        [ObservableProperty] private string? scanCurrentFile;
        [ObservableProperty] private int scanQueueCount;
        [ObservableProperty] private string? scanCurrentFolder;

        [ObservableProperty] private string statusText = "Ready";

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

        public MainWindowViewModel(IDialogServices dialogServices)
        {
            _scanQueue = new ScanQueueService(_library, _libraryPath);

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
            _dialogServices = dialogServices;
        }

        // ======== Startup ========
        public async Task InitializeAsync()
        {
            StatusText = "Loading library...";
            await _library.LoadAsync(_libraryPath);

            _index.Rebuild(_library.Books);

            // Show all initially
            _items = _index.Books;
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
                    _items = results;
                    ResultsCount = _items.Length;
                    OnPropertyChanged(nameof(Items));

                    // Keep selection sane if filtered out
                    if (SelectedBook is not null && Array.IndexOf(_items, SelectedBook) < 0)
                        SelectedBook = null;
                },

                externalCt: externalCt);
        }
    }
}
