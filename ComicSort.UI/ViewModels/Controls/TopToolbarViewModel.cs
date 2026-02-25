using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ComicSort.UI.ViewModels.Controls;

public partial class TopToolbarViewModel : ViewModelBase
{
    private readonly List<string> _selectedPrimaryFilters = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedPrimaryFilter = "Not Grouped";

    [ObservableProperty]
    private string primaryFilterMenuText = "Not Grouped";

    [ObservableProperty]
    private bool isSeriesSelected;

    [ObservableProperty]
    private bool isNotGroupedSelected = true;

    [ObservableProperty]
    private bool isPublisherSelected;

    [ObservableProperty]
    private bool isSmartListSelected;

    [ObservableProperty]
    private bool isFileDirectorySelected;

    [ObservableProperty]
    private bool isFolderSelected;

    [ObservableProperty]
    private bool isImportSourceSelected;

    [ObservableProperty]
    private string selectedSecondaryFilter = "File Directory";

    [ObservableProperty]
    private string selectedSortOption = "Name";

    [ObservableProperty]
    private string globalSearchText = string.Empty;

    public ObservableCollection<string> PrimaryFilters { get; } =
    [
        "Series",
        "Publisher",
        "Smart List"
    ];

    public ObservableCollection<string> SecondaryFilters { get; } =
    [
        "File Directory",
        "Folder",
        "Import Source"
    ];

    public ObservableCollection<string> SortOptions { get; } =
    [
        "Name",
        "Recent",
        "Count"
    ];

    public event EventHandler? GroupingSelectionChanged;

    public void ApplyPrimaryFilterSelection(string filterName, bool appendSelection)
    {
        if (!IsKnownPrimaryFilter(filterName))
        {
            return;
        }

        if (string.Equals(filterName, "Not Grouped", StringComparison.Ordinal))
        {
            _selectedPrimaryFilters.Clear();
            SyncPrimaryFilterState();
            return;
        }

        if (!appendSelection)
        {
            _selectedPrimaryFilters.Clear();
            _selectedPrimaryFilters.Add(filterName);
            SyncPrimaryFilterState();
            return;
        }

        if (IsNotGroupedSelected)
        {
            _selectedPrimaryFilters.Clear();
            _selectedPrimaryFilters.Add(filterName);
            SyncPrimaryFilterState();
            return;
        }

        if (_selectedPrimaryFilters.Contains(filterName))
        {
            if (_selectedPrimaryFilters.Count == 1)
            {
                _selectedPrimaryFilters.Clear();
                SyncPrimaryFilterState();
                return;
            }

            _selectedPrimaryFilters.Remove(filterName);
            SyncPrimaryFilterState();
            return;
        }

        _selectedPrimaryFilters.Add(filterName);
        SyncPrimaryFilterState();
    }

    public IReadOnlyList<string> GetGroupingSelection()
    {
        if (_selectedPrimaryFilters.Count == 0)
        {
            return ["Not Grouped"];
        }

        return [.. _selectedPrimaryFilters];
    }

    private void SyncPrimaryFilterState()
    {
        IsNotGroupedSelected = _selectedPrimaryFilters.Count == 0;
        IsSeriesSelected = _selectedPrimaryFilters.Contains("Series");
        IsPublisherSelected = _selectedPrimaryFilters.Contains("Publisher");
        IsSmartListSelected = _selectedPrimaryFilters.Contains("Smart List");
        IsFileDirectorySelected = _selectedPrimaryFilters.Contains("File Directory");
        IsFolderSelected = _selectedPrimaryFilters.Contains("Folder");
        IsImportSourceSelected = _selectedPrimaryFilters.Contains("Import Source");

        SelectedPrimaryFilter = _selectedPrimaryFilters.FirstOrDefault() ?? "Not Grouped";
        PrimaryFilterMenuText = _selectedPrimaryFilters.Count == 0
            ? "Not Grouped"
            : string.Join(" > ", _selectedPrimaryFilters);

        GroupingSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsKnownPrimaryFilter(string filterName)
    {
        return filterName is "Not Grouped"
            or "Series"
            or "Publisher"
            or "Smart List"
            or "File Directory"
            or "Folder"
            or "Import Source";
    }

}
