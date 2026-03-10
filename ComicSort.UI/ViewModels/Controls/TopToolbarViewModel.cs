using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ComicSort.UI.ViewModels.Controls;

public partial class TopToolbarViewModel : ViewModelBase
{
    private readonly List<string> _selectedPrimaryFilters = [];

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedPrimaryFilter = TopToolbarSelectionRules.NotGrouped;
    [ObservableProperty] private string primaryFilterMenuText = TopToolbarSelectionRules.NotGrouped;
    [ObservableProperty] private bool isSeriesSelected;
    [ObservableProperty] private bool isNotGroupedSelected = true;
    [ObservableProperty] private bool isPublisherSelected;
    [ObservableProperty] private bool isSmartListSelected;
    [ObservableProperty] private bool isFileDirectorySelected;
    [ObservableProperty] private bool isFolderSelected;
    [ObservableProperty] private bool isImportSourceSelected;
    [ObservableProperty] private string selectedSecondaryFilter = "File Directory";
    [ObservableProperty] private string selectedSortOption = "Name";
    [ObservableProperty] private string arrangeByMenuText = TopToolbarSelectionRules.ArrangeNotSorted;
    [ObservableProperty] private string selectedArrangeBy = TopToolbarSelectionRules.ArrangeNotSorted;
    [ObservableProperty] private bool isArrangeNotSortedSelected = true;
    [ObservableProperty] private bool isArrangeSeriesSelected;
    [ObservableProperty] private bool isArrangePositionSelected;
    [ObservableProperty] private bool isArrangeFilePathSelected;
    [ObservableProperty] private string globalSearchText = string.Empty;

    public ObservableCollection<string> PrimaryFilters { get; } = [.. TopToolbarSelectionRules.PrimaryFilters];
    public ObservableCollection<string> SecondaryFilters { get; } = [.. TopToolbarSelectionRules.SecondaryFilters];
    public ObservableCollection<string> SortOptions { get; } = [.. TopToolbarSelectionRules.SortOptions];

    public event EventHandler? GroupingSelectionChanged;
    public event EventHandler? ArrangeSelectionChanged;

    public void ApplyPrimaryFilterSelection(string filterName, bool appendSelection)
    {
        var nextSelection = TopToolbarSelectionRules.ApplyPrimarySelection(_selectedPrimaryFilters, filterName, appendSelection);
        if (_selectedPrimaryFilters.SequenceEqual(nextSelection, StringComparer.Ordinal))
        {
            return;
        }

        _selectedPrimaryFilters.Clear();
        _selectedPrimaryFilters.AddRange(nextSelection);
        SyncPrimaryFilterState();
    }

    public IReadOnlyList<string> GetGroupingSelection()
    {
        return _selectedPrimaryFilters.Count == 0
            ? [TopToolbarSelectionRules.NotGrouped]
            : [.. _selectedPrimaryFilters];
    }

    public void ApplyArrangeSelection(string arrangeBy)
    {
        if (!TopToolbarSelectionRules.IsKnownArrangeBy(arrangeBy))
        {
            return;
        }

        SelectedArrangeBy = arrangeBy;
        ArrangeByMenuText = arrangeBy;
        IsArrangeNotSortedSelected = string.Equals(arrangeBy, TopToolbarSelectionRules.ArrangeNotSorted, StringComparison.Ordinal);
        IsArrangeSeriesSelected = string.Equals(arrangeBy, TopToolbarSelectionRules.ArrangeSeries, StringComparison.Ordinal);
        IsArrangePositionSelected = string.Equals(arrangeBy, TopToolbarSelectionRules.ArrangePosition, StringComparison.Ordinal);
        IsArrangeFilePathSelected = string.Equals(arrangeBy, TopToolbarSelectionRules.ArrangeFilePath, StringComparison.Ordinal);
        ArrangeSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncPrimaryFilterState()
    {
        IsNotGroupedSelected = _selectedPrimaryFilters.Count == 0;
        IsSeriesSelected = _selectedPrimaryFilters.Contains("Series", StringComparer.Ordinal);
        IsPublisherSelected = _selectedPrimaryFilters.Contains("Publisher", StringComparer.Ordinal);
        IsSmartListSelected = _selectedPrimaryFilters.Contains("Smart List", StringComparer.Ordinal);
        IsFileDirectorySelected = _selectedPrimaryFilters.Contains("File Directory", StringComparer.Ordinal);
        IsFolderSelected = _selectedPrimaryFilters.Contains("Folder", StringComparer.Ordinal);
        IsImportSourceSelected = _selectedPrimaryFilters.Contains("Import Source", StringComparer.Ordinal);
        SelectedPrimaryFilter = _selectedPrimaryFilters.FirstOrDefault() ?? TopToolbarSelectionRules.NotGrouped;
        PrimaryFilterMenuText = _selectedPrimaryFilters.Count == 0
            ? TopToolbarSelectionRules.NotGrouped
            : string.Join(" > ", _selectedPrimaryFilters);
        GroupingSelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
