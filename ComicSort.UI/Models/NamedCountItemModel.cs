namespace ComicSort.UI.Models;

public sealed class NamedCountItemModel
{
    public NamedCountItemModel(string name, int count, string glyph)
    {
        Name = name;
        Count = count;
        Glyph = glyph;
    }

    public string Name { get; }

    public int Count { get; }

    public string Glyph { get; }
}
