using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ComicSort.UI.Models;

public sealed partial class ComicTileModel : ObservableObject
{
    [ObservableProperty]
    private string filePath = string.Empty;

    [ObservableProperty]
    private string fileDirectory = string.Empty;

    [ObservableProperty]
    private string displayTitle = string.Empty;

    [ObservableProperty]
    private string series = string.Empty;

    [ObservableProperty]
    private string publisher = "Unspecified";

    [ObservableProperty]
    private string? thumbnailPath;

    [ObservableProperty]
    private Bitmap? thumbnailImage;

    [ObservableProperty]
    private bool isThumbnailReady;

    [ObservableProperty]
    private string fileTypeTag = string.Empty;

    [ObservableProperty]
    private DateTimeOffset lastScannedUtc;
}
