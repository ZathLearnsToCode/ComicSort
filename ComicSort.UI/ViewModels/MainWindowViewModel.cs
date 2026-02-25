using ComicSort.Engine.Services;
using ComicSort.UI.Models;
using ComicSort.UI.ViewModels.Controls;
using System.Collections.Generic;

namespace ComicSort.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly LibraryActionsBarViewModel _libraryActionsBarViewModel;

    public LibraryActionsBarViewModel? LibraryActionsBar { get; }

    public MainWindowViewModel(
        LibraryActionsBarViewModel libraryActionsBarViewModel,
        ComicGridViewModel comicGridViewModel,
        SidebarViewModel sidebarViewModel,
        StatusBarViewModel statusBarViewModel)
    {
        _libraryActionsBarViewModel = libraryActionsBarViewModel;

        LibraryActionsBar = _libraryActionsBarViewModel;

        TopToolbar = new TopToolbarViewModel();
        TopToolbar.GroupingSelectionChanged += OnGroupingSelectionChanged;
        Sidebar = sidebarViewModel;
        SeriesPane = new ExplorerPaneViewModel("Series", CreateSeriesPaneData());
        DirectoryPane = new ExplorerPaneViewModel("Title", CreateDirectoryPaneData());
        PublisherPane = new ExplorerPaneViewModel("Publisher", CreatePublisherPaneData());
        ImportsPane = new ExplorerPaneViewModel("Imprint", CreateImportsPaneData());
        PathBar = new PathBarViewModel();
        ComicGrid = comicGridViewModel;
        ComicGrid.ApplyGrouping(TopToolbar.GetGroupingSelection());
        StatusBar = statusBarViewModel;
        
    }

    
    public TopToolbarViewModel TopToolbar { get; }

    public SidebarViewModel Sidebar { get; }

    public ExplorerPaneViewModel SeriesPane { get; }

    public ExplorerPaneViewModel DirectoryPane { get; }

    public ExplorerPaneViewModel PublisherPane { get; }

    public ExplorerPaneViewModel ImportsPane { get; }

    public PathBarViewModel PathBar { get; }

    public ComicGridViewModel ComicGrid { get; }

    public StatusBarViewModel StatusBar { get; }

    private void OnGroupingSelectionChanged(object? sender, System.EventArgs e)
    {
        ComicGrid.ApplyGrouping(TopToolbar.GetGroupingSelection());
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<NamedCountItemModel>> CreateSeriesPaneData() =>
        new Dictionary<string, IReadOnlyList<NamedCountItemModel>>
        {
            ["Series"] =
            [
                new("All Comics", 68585, "[]"),
                new("Favorites", 410, "[]"),
                new("Recently Added", 125, "[]"),
                new("Recently Read", 57, "[]"),
                new("Reading Now", 11, "[]"),
                new("On Deck", 28, "[]")
            ],
            ["Title"] =
            [
                new("Absolute Batman", 12, "[]"),
                new("Action Comics", 142, "[]"),
                new("Amazing Spider-Man", 187, "[]"),
                new("Avengers", 96, "[]"),
                new("Batman", 208, "[]"),
                new("Black Hammer", 43, "[]")
            ],
            ["Publisher"] =
            [
                new("DC", 14555, "[]"),
                new("Image", 5011, "[]"),
                new("Marvel", 20404, "[]"),
                new("Dark Horse", 3880, "[]"),
                new("IDW", 1955, "[]")
            ],
            ["Imprint"] =
            [
                new("Vertigo", 955, "[]"),
                new("Black Label", 214, "[]"),
                new("Ultimate", 411, "[]"),
                new("MAX", 180, "[]"),
                new("Icon", 137, "[]")
            ]
        };

    private static IReadOnlyDictionary<string, IReadOnlyList<NamedCountItemModel>> CreateDirectoryPaneData() =>
        new Dictionary<string, IReadOnlyList<NamedCountItemModel>>
        {
            ["Series"] =
            [
                new("A-B", 704, "[]"),
                new("C-D", 1198, "[]"),
                new("E-H", 1333, "[]"),
                new("I-M", 991, "[]"),
                new("N-Z", 1592, "[]")
            ],
            ["Title"] =
            [
                new(@"J:\COMICS\NEWSCANS", 2225, "[]"),
                new(@"J:\COMICS\MARVEL", 4021, "[]"),
                new(@"J:\COMICS\DC", 3774, "[]"),
                new(@"J:\COMICS\EUROPE", 1158, "[]"),
                new(@"J:\COMICS\INDIE", 954, "[]")
            ],
            ["Publisher"] =
            [
                new(@"J:\COMICS\BY_PUBLISHER\DC", 3774, "[]"),
                new(@"J:\COMICS\BY_PUBLISHER\MARVEL", 4021, "[]"),
                new(@"J:\COMICS\BY_PUBLISHER\IMAGE", 1224, "[]"),
                new(@"J:\COMICS\BY_PUBLISHER\IDW", 511, "[]")
            ],
            ["Imprint"] =
            [
                new(@"J:\COMICS\IMPRINT\VERTIGO", 955, "[]"),
                new(@"J:\COMICS\IMPRINT\BLACK_LABEL", 214, "[]"),
                new(@"J:\COMICS\IMPRINT\ULTIMATE", 411, "[]"),
                new(@"J:\COMICS\IMPRINT\MAX", 180, "[]")
            ]
        };

    private static IReadOnlyDictionary<string, IReadOnlyList<NamedCountItemModel>> CreatePublisherPaneData() =>
        new Dictionary<string, IReadOnlyList<NamedCountItemModel>>
        {
            ["Series"] =
            [
                new("Action Comics", 142, "[]"),
                new("Detective Comics", 231, "[]"),
                new("Hellboy", 77, "[]"),
                new("Invincible", 66, "[]")
            ],
            ["Title"] =
            [
                new("DC", 14555, "[]"),
                new("Marvel", 20404, "[]"),
                new("Image", 5011, "[]"),
                new("Dark Horse", 3880, "[]"),
                new("IDW", 1955, "[]")
            ],
            ["Publisher"] =
            [
                new("DC (22 Imprints)", 14555, "[]"),
                new("Marvel (16 Imprints)", 20404, "[]"),
                new("Image", 5011, "[]"),
                new("Dark Horse", 3880, "[]"),
                new("IDW", 1955, "[]")
            ],
            ["Imprint"] =
            [
                new("Vertigo", 955, "[]"),
                new("Black Label", 214, "[]"),
                new("DCeased", 49, "[]"),
                new("Ultimate", 411, "[]"),
                new("MAX", 180, "[]")
            ]
        };

    private static IReadOnlyDictionary<string, IReadOnlyList<NamedCountItemModel>> CreateImportsPaneData() =>
        new Dictionary<string, IReadOnlyList<NamedCountItemModel>>
        {
            ["Series"] =
            [
                new("All Imports", 54, "[]"),
                new("Daily Pull", 8, "[]"),
                new("Weekly Batch", 17, "[]"),
                new("Backfill", 29, "[]")
            ],
            ["Title"] =
            [
                new("Unspecified", 0, "[]"),
                new("Atolon Lab - Danger Zone", 5, "[]"),
                new("AW1 Studios - Libertate", 7, "[]"),
                new("Beyond Books", 1, "[]"),
                new("Black Crown", 1, "[]")
            ],
            ["Publisher"] =
            [
                new("DC Import Feed", 13, "[]"),
                new("Marvel Import Feed", 19, "[]"),
                new("Image Import Feed", 9, "[]"),
                new("European Feed", 6, "[]"),
                new("Unmatched Feed", 7, "[]")
            ],
            ["Imprint"] =
            [
                new("All (54 imports)", 54, "[]"),
                new("Unspecified", 0, "[]"),
                new("Atolon Lab - Danger Zone", 5, "[]"),
                new("AW1 Studios - Libertate", 7, "[]"),
                new("Beyond Books", 1, "[]"),
                new("Black Crown", 1, "[]")
            ]
        };
}
