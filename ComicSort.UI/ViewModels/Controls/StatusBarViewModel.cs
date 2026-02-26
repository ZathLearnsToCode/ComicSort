using Avalonia.Threading;
using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels.Controls;

public partial class StatusBarViewModel : ViewModelBase
{
    private readonly IScanRepository _scanRepository;
    private readonly IScanService _scanService;
    private readonly IComicDatabaseService _comicDatabaseService;
    private int _libraryCountBaseline;

    public StatusBarViewModel(
        IScanRepository scanRepository,
        IScanService scanService,
        IComicDatabaseService comicDatabaseService,
        ComicGridViewModel comicGridViewModel)
    {
        _scanRepository = scanRepository;
        _scanService = scanService;
        _comicDatabaseService = comicDatabaseService;

        _scanService.ProgressChanged += OnScanProgressChanged;
        _scanService.StateChanged += OnScanStateChanged;
        comicGridViewModel.SelectedItemChanged += OnSelectedItemChanged;
        comicGridViewModel.FilterSummaryChanged += OnFilterSummaryChanged;

        SelectedFile = comicGridViewModel.SelectedItem?.FilePath ?? "No book selected";
        FilterSummary = comicGridViewModel.FilterSummary;
        _ = LoadLibrarySummaryAsync();
    }

    [ObservableProperty]
    private string librarySummary = "Library: 0 Books";

    [ObservableProperty]
    private string selectedFile = "No book selected";

    [ObservableProperty]
    private string filterSummary = "Filter: All Comics";

    [ObservableProperty]
    private string transferSummary = "Scanned: 0";

    [ObservableProperty]
    private string ratioSummary = "Inserted: 0";

    [ObservableProperty]
    private string issueSummary = "Updated: 0";

    [ObservableProperty]
    private string yearSummary = "Failed: 0";

    private async Task LoadLibrarySummaryAsync()
    {
        await _comicDatabaseService.InitializeAsync();
        var totalCount = await _scanRepository.GetTotalCountAsync();
        Interlocked.Exchange(ref _libraryCountBaseline, totalCount);

        Dispatcher.UIThread.Post(() =>
        {
            LibrarySummary = $"Library: {totalCount} Books";
        });
    }

    private void OnSelectedItemChanged(object? sender, ComicSort.UI.Models.ComicTileModel? selectedItem)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SelectedFile = selectedItem?.FilePath ?? "No book selected";
        });
    }

    private void OnFilterSummaryChanged(object? sender, string summary)
    {
        Dispatcher.UIThread.Post(() =>
        {
            FilterSummary = summary;
        });
    }

    private void OnScanProgressChanged(object? sender, ScanProgressUpdate update)
    {
        var baseline = Volatile.Read(ref _libraryCountBaseline);
        var totalBooks = baseline + update.FilesInserted;
        if (totalBooks < 0)
        {
            totalBooks = 0;
        }

        Dispatcher.UIThread.Post(() =>
        {
            LibrarySummary = $"Library: {totalBooks} Books";
            TransferSummary = $"Scanned: {update.FilesEnumerated}";
            RatioSummary = $"Inserted: {update.FilesInserted}";
            IssueSummary = $"Updated: {update.FilesUpdated}";
            YearSummary = $"Failed: {update.FilesFailed}";
        });
    }

    private void OnScanStateChanged(object? sender, ScanStateChangedEventArgs eventArgs)
    {
        if (eventArgs.IsRunning)
        {
            return;
        }

        _ = LoadLibrarySummaryAsync();
    }
}
