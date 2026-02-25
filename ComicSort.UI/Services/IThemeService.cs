using System.Collections.Generic;

namespace ComicSort.UI.Services;

public interface IThemeService
{
    IReadOnlyList<string> AvailableThemes { get; }

    string NormalizeThemeName(string? themeName, string? fallbackThemeName = null);

    bool IsDarkTheme(string? themeName);

    bool ApplyTheme(string? themeName);
}
