using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ComicSort.UI.Models;

public partial class SidebarItem : ObservableObject
{
    public SidebarItem(string name, int? count = null, bool isHeader = false)
    {
        Name = name;
        Count = count;
        IsHeader = isHeader;
    }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private int? count;

    [ObservableProperty]
    private bool isHeader;

    [ObservableProperty]
    private bool isEditing;

    public ObservableCollection<SidebarItem> Children { get; } = [];

    public bool HasCount => Count.HasValue;
}
