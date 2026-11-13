using System;

namespace KIGest.App.Services;

/// <summary>Session utilisateur en mémoire.</summary>
public static class Session
{
    public static UtilisateurSession? Current { get; set; }
    public static bool EstConnecte => Current != null;
    public static bool EstAdmin => Current?.Role == "Admin";

    public static string? CurrentUserLogin => Current?.Login;
    public static string? CurrentUserRole => Current?.Role;
}

public class UtilisateurSession
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
}
