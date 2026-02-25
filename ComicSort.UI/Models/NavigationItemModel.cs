namespace ComicSort.UI.Models;

public sealed class NavigationItemModel
{
    public NavigationItemModel(string title, string count, string glyph, bool isSelected = false)
    {
        Title = title;
        Count = count;
        Glyph = glyph;
        IsSelected = isSelected;
    }

    public string Title { get; }

    public string Count { get; }

    public string Glyph { get; }

    public bool IsSelected { get; }

    public bool HasCount => !string.IsNullOrWhiteSpace(Count);
}
