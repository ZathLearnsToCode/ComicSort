using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ComicSort.UI.Services;

public sealed class ThemeService : IThemeService
{
    private const string SoftNeutralPro = "Soft Neutral Pro";
    private const string WarmEditorial = "Warm Editorial";
    private const string MinimalHighContrast = "Minimal High Contrast";
    private const string DeepSlate = "Deep Slate";
    private const string GraphiteTeal = "Graphite + Teal";
    private const string AmoledFriendly = "AMOLED Friendly";

    private static readonly IReadOnlyList<string> AvailableThemeNames = new ReadOnlyCollection<string>(
    [
        SoftNeutralPro,
        WarmEditorial,
        MinimalHighContrast,
        DeepSlate,
        GraphiteTeal,
        AmoledFriendly
    ]);

    private static readonly Dictionary<string, ThemePaletteDefinition> Palettes = new(StringComparer.OrdinalIgnoreCase)
    {
        [SoftNeutralPro] = new ThemePaletteDefinition(
            "#F7F8FA",
            "#FFFFFF",
            "#ECEFF3",
            "#D6D9DF",
            "#1F2933",
            "#6B7280",
            "#2563EB",
            "#1D4ED8",
            "#DBEAFE",
            false),
        [WarmEditorial] = new ThemePaletteDefinition(
            "#FAF8F5",
            "#FFFFFF",
            "#F1ECE6",
            "#D9D3CC",
            "#2B2B2B",
            "#7A746C",
            "#0D9488",
            "#0F766E",
            "#CCFBF1",
            false),
        [MinimalHighContrast] = new ThemePaletteDefinition(
            "#FFFFFF",
            "#F3F4F6",
            "#F3F4F6",
            "#D1D5DB",
            "#111827",
            "#6B7280",
            "#4F46E5",
            "#4F46E5",
            "#E0E7FF",
            false),
        [DeepSlate] = new ThemePaletteDefinition(
            "#0F172A",
            "#1E293B",
            "#273449",
            "#334155",
            "#E2E8F0",
            "#94A3B8",
            "#3B82F6",
            "#2563EB",
            "#1E3A8A",
            true),
        [GraphiteTeal] = new ThemePaletteDefinition(
            "#121212",
            "#1E1E1E",
            "#252525",
            "#2E2E2E",
            "#F5F5F5",
            "#B3B3B3",
            "#14B8A6",
            "#0D9488",
            "#134E4A",
            true),
        [AmoledFriendly] = new ThemePaletteDefinition(
            "#000000",
            "#121212",
            "#121212",
            "#1F1F1F",
            "#FFFFFF",
            "#9CA3AF",
            "#3B82F6",
            "#3B82F6",
            "#1D4ED8",
            true)
    };

    public IReadOnlyList<string> AvailableThemes => AvailableThemeNames;

    public string NormalizeThemeName(string? themeName, string? fallbackThemeName = null)
    {
        if (!string.IsNullOrWhiteSpace(themeName) && Palettes.ContainsKey(themeName.Trim()))
        {
            return GetCanonicalThemeName(themeName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(fallbackThemeName) && Palettes.ContainsKey(fallbackThemeName.Trim()))
        {
            return GetCanonicalThemeName(fallbackThemeName.Trim());
        }

        return SoftNeutralPro;
    }

    public bool IsDarkTheme(string? themeName)
    {
        var normalized = NormalizeThemeName(themeName);
        return Palettes.TryGetValue(normalized, out var palette) && palette.DarkMode;
    }

    public bool ApplyTheme(string? themeName)
    {
        var normalized = NormalizeThemeName(themeName);
        if (!Palettes.TryGetValue(normalized, out var palette) || Application.Current is null)
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

    private static string GetCanonicalThemeName(string themeName)
    {
        foreach (var knownTheme in AvailableThemeNames)
        {
            if (string.Equals(knownTheme, themeName, StringComparison.OrdinalIgnoreCase))
            {
                return knownTheme;
            }
        }

        return SoftNeutralPro;
    }

    private sealed record ThemePaletteDefinition(
        string Background,
        string Surface,
        string Panel,
        string Border,
        string PrimaryText,
        string SecondaryText,
        string Accent,
        string AccentHover,
        string Selection,
        bool DarkMode);
}
