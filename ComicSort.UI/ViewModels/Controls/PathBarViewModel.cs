using CommunityToolkit.Mvvm.ComponentModel;

namespace ComicSort.UI.ViewModels.Controls;

public partial class PathBarViewModel : ViewModelBase
{
    [ObservableProperty]
    private string currentPath = @"J:\COMICS\NEWSCANS\2019.01.01-DAY_WEEK OF 2019.01.02";

    [ObservableProperty]
    private string pageSummary = "(12 / 125)";
}
