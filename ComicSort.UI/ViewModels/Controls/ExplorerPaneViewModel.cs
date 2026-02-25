using ComicSort.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ComicSort.UI.ViewModels.Controls;

public partial class ExplorerPaneViewModel : ViewModelBase
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<NamedCountItemModel>> itemsByOption;

    public ExplorerPaneViewModel(
        string selectedPaneOption,
        IReadOnlyDictionary<string, IReadOnlyList<NamedCountItemModel>> itemsByOption)
    {
        this.itemsByOption = itemsByOption;
        PaneOptions = new ObservableCollection<string>(itemsByOption.Keys);
        VisibleItems = new ObservableCollection<NamedCountItemModel>();

        SelectedPaneOption = PaneOptions.Contains(selectedPaneOption)
            ? selectedPaneOption
            : PaneOptions.FirstOrDefault() ?? string.Empty;

        RefreshVisibleItems();
    }

    [ObservableProperty]
    private string selectedPaneOption;

    [ObservableProperty]
    private NamedCountItemModel? selectedItem;

    public ObservableCollection<string> PaneOptions { get; }

    public ObservableCollection<NamedCountItemModel> VisibleItems { get; }

    partial void OnSelectedPaneOptionChanged(string value)
    {
        RefreshVisibleItems();
    }

    private void RefreshVisibleItems()
    {
        VisibleItems.Clear();

        if (itemsByOption.TryGetValue(SelectedPaneOption, out var items))
        {
            foreach (var item in items)
            {
                VisibleItems.Add(item);
            }
        }

        SelectedItem = VisibleItems.FirstOrDefault();
    }
}
