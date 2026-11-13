using System.Reflection;

namespace KIGest.App.Services;

/// <summary>Informations produit (source = métadonnées d'assembly, alignées sur KIGest.App.csproj).</summary>
public static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString()
        ?? "1.1.0";

    public static string NomProduit => "KI-GEST Pointage";
    public static string Affichage => $"{NomProduit} v{Version}";
}
