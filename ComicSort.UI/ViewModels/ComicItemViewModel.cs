using Avalonia.Media.Imaging;
using ComicSort.Engine.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ComicSort.UI.ViewModels;

public sealed partial class ComicItemViewModel : ViewModelBase
{
    public ComicBook Book { get; }

    [ObservableProperty] private Bitmap? thumbnail;
    [ObservableProperty] private bool isThumbnailRequested;

    public ComicItemViewModel(ComicBook book) => Book = book;
}
