using ComicSort.Engine.Settings;
using System;

namespace ComicSort.UI.Models;

public enum ComicGridFilterMode
{
    AllComics = 0,
    SmartList = 1
}

public sealed class ComicGridFilterRequest
{
    public ComicGridFilterMode Mode { get; init; } = ComicGridFilterMode.AllComics;

    public Guid? SmartListId { get; init; }

    public string SmartListName { get; init; } = "All Comics";

    public ComicListItem? SmartList { get; init; }
}
