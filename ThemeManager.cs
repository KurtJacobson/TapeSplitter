using System.Windows;
using Microsoft.Win32;

namespace TapeSplitterWpf;

public enum ThemeMode { Light, Dark, System }

public static class ThemeManager
{
    public static ThemeMode Current { get; private set; } = ThemeMode.System;

    /// <summary>Fires after the resource dictionary has been swapped.</summary>
    public static event Action? ThemeChanged;

    public static void Apply(ThemeMode mode)
    {
        Current = mode;
        ThemeMode effective = mode == ThemeMode.System ? DetectSystem() : mode;
        SwapDictionary(effective == ThemeMode.Dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml");
        ThemeChanged?.Invoke();
    }

    /// <summary>Re-evaluates the system preference (call on app start and on System menu pick).</summary>
    public static void Refresh() => Apply(Current);

    // ── Internal ──────────────────────────────────────────────────────────────

    private static void SwapDictionary(string relativeUri)
    {
        var dicts = Application.Current.Resources.MergedDictionaries;

        // Remove previous theme dictionary (identified by its Source path)
        var old = dicts.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true);
        if (old != null) dicts.Remove(old);

        dicts.Add(new ResourceDictionary { Source = new Uri(relativeUri, UriKind.Relative) });
    }

    private static ThemeMode DetectSystem()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int v && v == 0)
                return ThemeMode.Dark;
        }
        catch { }
        return ThemeMode.Light;
    }
}
