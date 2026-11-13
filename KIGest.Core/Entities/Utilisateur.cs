using System;

namespace KIGest.Core.Entities;

public class Utilisateur
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string MotDePasseHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Consultation";
}
