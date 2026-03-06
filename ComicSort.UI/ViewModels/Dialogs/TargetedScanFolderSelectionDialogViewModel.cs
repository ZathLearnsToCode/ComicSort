using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ComicSort.UI.ViewModels.Dialogs;

public sealed partial class TargetedScanFolderSelectionDialogViewModel : ViewModelBase
{
    public TargetedScanFolderSelectionDialogViewModel(IReadOnlyList<string> availableFolders)
    {
        var normalizedFolders = availableFolders
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var folder in normalizedFolders)
        {
            var item = new TargetedScanFolderOptionViewModel
            {
                FolderPath = folder
            };

            item.PropertyChanged += OnFolderOptionPropertyChanged;
            FolderOptions.Add(item);
        }

        HasFolders = FolderOptions.Count > 0;
        StatusText = HasFolders
            ? "Select one or more folders to scan."
            : "No configured library folders are available.";
        UpdateCanStartScan();
    }

    public ObservableCollection<TargetedScanFolderOptionViewModel> FolderOptions { get; } = [];

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private bool hasFolders;

    public bool HasNoFolders => !HasFolders;

    [ObservableProperty]
    private bool canStartScan;

    public event EventHandler<TargetedScanFolderSelectionCloseRequestedEventArgs>? CloseRequested;

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var folder in FolderOptions)
        {
            folder.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var folder in FolderOptions)
        {
            folder.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartTargetedScan))]
    private void StartTargetedScan()
    {
        if (!CanStartScan)
        {
            return;
        }

        var selectedFolders = FolderOptions
            .Where(x => x.IsSelected)
            .Select(x => x.FolderPath)
            .ToArray();

        CloseRequested?.Invoke(
            this,
            new TargetedScanFolderSelectionCloseRequestedEventArgs(selectedFolders));
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new TargetedScanFolderSelectionCloseRequestedEventArgs(null));
    }

    partial void OnCanStartScanChanged(bool value)
    {
        StartTargetedScanCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasFoldersChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoFolders));
    }

    private bool CanStartTargetedScan()
    {
        return CanStartScan;
    }

    private void OnFolderOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(TargetedScanFolderOptionViewModel.IsSelected), StringComparison.Ordinal))
        {
            return;
        }

        UpdateCanStartScan();
    }

    private void UpdateCanStartScan()
    {
        CanStartScan = FolderOptions.Any(x => x.IsSelected);
    }
}

public sealed partial class TargetedScanFolderOptionViewModel : ObservableObject
{
    [ObservableProperty]
    private string folderPath = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}

public sealed class TargetedScanFolderSelectionCloseRequestedEventArgs : EventArgs
{
    public TargetedScanFolderSelectionCloseRequestedEventArgs(IReadOnlyList<string>? selectedFolders)
    {
        SelectedFolders = selectedFolders;
    }

    public IReadOnlyList<string>? SelectedFolders { get; }
}
