using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using System.Collections.Generic;

namespace ComicSort.UI.Services;

public sealed class ThemeService : IThemeService
{
    public IReadOnlyList<string> AvailableThemes => ThemePaletteCatalog.AvailableThemeNames;

    public string NormalizeThemeName(string? themeName, string? fallbackThemeName = null)
    {
        return ThemePaletteCatalog.NormalizeThemeName(themeName, fallbackThemeName);
    }

    public bool IsDarkTheme(string? themeName)
    {
        var normalized = NormalizeThemeName(themeName);
        return ThemePaletteCatalog.TryGetPalette(normalized, out var palette) && palette.DarkMode;
    }

    public bool ApplyTheme(string? themeName)
    {
        var normalized = NormalizeThemeName(themeName);
        if (!ThemePaletteCatalog.TryGetPalette(normalized, out var palette) || Application.Current is null)
        {
            return false;
        }

        var resources = Application.Current.Resources;

        resources["Theme.BackgroundColor"] = Color.Parse(palette.Background);
        resources["Theme.SurfaceColor"] = Color.Parse(palette.Surface);
        resources["Theme.PanelColor"] = Color.Parse(palette.Panel);
        resources["Theme.BorderColor"] = Color.Parse(palette.Border);
        resources["Theme.PrimaryTextColor"] = Color.Parse(palette.PrimaryText);
        resources["Theme.SecondaryTextColor"] = Color.Parse(palette.SecondaryText);
        resources["Theme.AccentColor"] = Color.Parse(palette.Accent);
        resources["Theme.AccentHoverColor"] = Color.Parse(palette.AccentHover);
        resources["Theme.SelectionColor"] = Color.Parse(palette.Selection);

        Application.Current.RequestedThemeVariant = palette.DarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        return true;
    }
}
