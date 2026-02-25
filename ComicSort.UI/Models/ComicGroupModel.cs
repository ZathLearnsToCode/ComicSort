using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ComicSort.UI.Models;

public sealed partial class ComicGroupModel : ObservableObject
{
    [ObservableProperty]
    private string header = string.Empty;

    [ObservableProperty]
    private bool isExpanded = true;

    public ObservableCollection<ComicTileModel> Items { get; } = [];
}
