using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ComicSort.UI.Services;

internal static class ThemePaletteCatalog
{
    private const string SoftNeutralPro = "Soft Neutral Pro";
    private const string WarmEditorial = "Warm Editorial";
    private const string MinimalHighContrast = "Minimal High Contrast";
    private const string DeepSlate = "Deep Slate";
    private const string GraphiteTeal = "Graphite + Teal";
    private const string AmoledFriendly = "AMOLED Friendly";

    public static IReadOnlyList<string> AvailableThemeNames { get; } = new ReadOnlyCollection<string>(
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
        [SoftNeutralPro] = new("#F7F8FA", "#FFFFFF", "#ECEFF3", "#D6D9DF", "#1F2933", "#6B7280", "#2563EB", "#1D4ED8", "#DBEAFE", false),
        [WarmEditorial] = new("#FAF8F5", "#FFFFFF", "#F1ECE6", "#D9D3CC", "#2B2B2B", "#7A746C", "#0D9488", "#0F766E", "#CCFBF1", false),
        [MinimalHighContrast] = new("#FFFFFF", "#F3F4F6", "#F3F4F6", "#D1D5DB", "#111827", "#6B7280", "#4F46E5", "#4F46E5", "#E0E7FF", false),
        [DeepSlate] = new("#0F172A", "#1E293B", "#273449", "#334155", "#E2E8F0", "#94A3B8", "#3B82F6", "#2563EB", "#1E3A8A", true),
        [GraphiteTeal] = new("#121212", "#1E1E1E", "#252525", "#2E2E2E", "#F5F5F5", "#B3B3B3", "#14B8A6", "#0D9488", "#134E4A", true),
        [AmoledFriendly] = new("#000000", "#121212", "#121212", "#1F1F1F", "#FFFFFF", "#9CA3AF", "#3B82F6", "#3B82F6", "#1D4ED8", true)
    };

    public static string NormalizeThemeName(string? themeName, string? fallbackThemeName = null)
    {
        if (TryGetCanonicalThemeName(themeName, out var canonicalTheme))
        {
            return canonicalTheme;
        }

        if (TryGetCanonicalThemeName(fallbackThemeName, out canonicalTheme))
        {
            return canonicalTheme;
        }

        return SoftNeutralPro;
    }

    public static bool TryGetPalette(string themeName, out ThemePaletteDefinition palette)
    {
        return Palettes.TryGetValue(themeName, out palette!);
    }

    private static bool TryGetCanonicalThemeName(string? themeName, out string canonicalThemeName)
    {
        canonicalThemeName = SoftNeutralPro;
        if (string.IsNullOrWhiteSpace(themeName))
        {
            return false;
        }

        var trimmedThemeName = themeName.Trim();
        if (!Palettes.ContainsKey(trimmedThemeName))
        {
            return false;
        }

        canonicalThemeName = GetCanonicalThemeName(trimmedThemeName);
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
}

internal sealed record ThemePaletteDefinition(
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
