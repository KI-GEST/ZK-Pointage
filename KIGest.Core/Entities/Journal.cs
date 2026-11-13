using System;

namespace KIGest.Core.Entities;

public class Journal
{
    public int Id { get; set; }
    public DateTime DateHeure { get; set; } = DateTime.Now;
    public int? UtilisateurId { get; set; }
    public Utilisateur? Utilisateur { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
}
