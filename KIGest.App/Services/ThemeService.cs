using System;
using System.Linq;
using System.Windows;

namespace KIGest.App.Services;

/// <summary>
/// Gère la bascule clair / sombre en échangeant le dictionnaire composite correspondant
/// (BundledTheme Material Design + thème métier KI-GEST Pointage).
/// </summary>
public static class ThemeService
{
    public const string Clair = "Clair";
    public const string Sombre = "Sombre";

    private static string _themeCourant = Sombre;

    public static string ThemeCourant => _themeCourant;

    /// <summary>Applique le thème.</summary>
    public static void Appliquer(string theme)
    {
        theme = string.Equals(theme, Clair, StringComparison.OrdinalIgnoreCase) ? Clair : Sombre;
        _themeCourant = theme;

        // Retire tout dictionnaire composite déjà présent
        var composites = Application.Current.Resources.MergedDictionaries
            .Where(d => d.Source?.OriginalString is string s
                        && (s.Contains("BundledThemeDark.xaml") || s.Contains("BundledThemeLight.xaml")))
            .ToList();

        foreach (var d in composites)
            Application.Current.Resources.MergedDictionaries.Remove(d);

        string source = theme == Clair
            ? "Resources/BundledThemeLight.xaml"
            : "Resources/BundledThemeDark.xaml";

        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/KIGest.App;component/" + source, UriKind.Absolute)
        });
    }
}
