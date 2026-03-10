using ComicSort.Engine.Models;
using ComicSort.UI.Models;
using System.Collections.Generic;

namespace ComicSort.UI.Services;

public interface IComicGridArrangementService
{
    IReadOnlyList<string> NormalizeGrouping(IReadOnlyList<string> grouping);

    string NormalizeArrangement(string? arrangeBy);

    IReadOnlyList<ComicTileModel> ArrangeItems(IReadOnlyList<ComicTileModel> items, string arrangeBy);

    IReadOnlyList<ComicGroupModel> BuildGroups(IReadOnlyList<ComicTileModel> items, IReadOnlyList<string> grouping);

    ComicTileModel CreateTile(ComicLibraryItem item);

    void UpdateTile(ComicTileModel tile, ComicLibraryItem item);

    string CoalesceGroupValue(string? value, string? displayTitle = null);
}
