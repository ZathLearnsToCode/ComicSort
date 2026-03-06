using Avalonia.Threading;
using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using ComicSort.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels.Controls;

public partial class LibraryActionsBarViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly IScanService _scanService;

    [ObservableProperty]
    private string addFolderButtonText = "Add Folder to Library";

    [ObservableProperty]
    private string scanLibraryButtonText = "Full Scan";

    [ObservableProperty]
    private string targetedScanButtonText = "Targeted Scan";

    [ObservableProperty]
    private string cancelScanButtonText = "Cancel Scan";

    [ObservableProperty]
    private string settingsButtonText = "Settings";

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private bool addFolderEnabled = true;

    [ObservableProperty]
    private bool scanLibraryEnabled = true;

    [ObservableProperty]
    private bool isScanRunning;

    public LibraryActionsBarViewModel(
        IDialogService dialogService,
        ISettingsService settingsService,
        IScanService scanService)
    {
        _dialogService = dialogService;
        _settingsService = settingsService;
        _scanService = scanService;

        _scanService.ProgressChanged += OnScanProgressChanged;
        _scanService.StateChanged += OnScanStateChanged;

        IsScanRunning = _scanService.IsRunning;
    }

    partial void OnIsScanRunningChanged(bool value)
    {
        AddFolderEnabled = !value;
        ScanLibraryEnabled = !value;
    }

    [RelayCommand]
    private async Task AddFolderToLibraryAsync()
    {
        if (IsScanRunning)
        {
            return;
        }

        var folder = await _dialogService.ShowOpenFolderDialogAsync("Select a folder to add to the library");
        if (folder is null)
        {
            return;
        }

        await _settingsService.SavetoSettings(folder);
        StatusText = $"Added folder: {folder}";
    }

    [RelayCommand]
    private Task ScanLibraryAsync()
    {
        if (!ScanLibraryEnabled)
        {
            return Task.CompletedTask;
        }

        StatusText = "Starting scan...";
        _ = _scanService.StartScanAsync();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task TargetedScanLibraryAsync()
    {
        if (!ScanLibraryEnabled)
        {
            return;
        }

        await _settingsService.InitializeAsync();

        var availableFolders = _settingsService.CurrentSettings.LibraryFolders
            .Where(x => !string.IsNullOrWhiteSpace(x.Folder))
            .Select(x => x.Folder.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (availableFolders.Length == 0)
        {
            StatusText = "No library folders configured.";
            return;
        }

        var selectedFolders = await _dialogService.ShowTargetedScanFolderSelectionDialogAsync(availableFolders);
        if (selectedFolders is null)
        {
            return;
        }

        if (selectedFolders.Count == 0)
        {
            StatusText = "No folders selected for targeted scan.";
            return;
        }

        StatusText = $"Starting targeted scan ({selectedFolders.Count} folders)...";
        _ = _scanService.StartScanAsync(selectedFolders);
    }

    [RelayCommand]
    private void CancelScan()
    {
        if (!IsScanRunning)
        {
            return;
        }

        StatusText = "Cancelling scan...";
        _scanService.CancelScan();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var saved = await _dialogService.ShowSettingsDialogAsync();
        StatusText = saved ? "Settings saved" : "Settings closed";
    }

    private void OnScanProgressChanged(object? sender, ScanProgressUpdate update)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var pathText = string.IsNullOrWhiteSpace(update.CurrentFilePath)
                ? string.Empty
                : $" | {update.CurrentFilePath}";

            StatusText =
                $"{update.Stage} E:{update.FilesEnumerated} I:{update.FilesInserted} U:{update.FilesUpdated} S:{update.FilesSkipped} F:{update.FilesFailed}{pathText}";
        });
    }

    private void OnScanStateChanged(object? sender, ScanStateChangedEventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsScanRunning = eventArgs.IsRunning;
            if (!eventArgs.IsRunning && string.IsNullOrWhiteSpace(StatusText))
            {
                StatusText = eventArgs.Stage;
            }
        });
    }
}
