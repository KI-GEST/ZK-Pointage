using System;

namespace KIGest.Core.Entities;

public class Employe
{
    public int Id { get; set; }
    public string Matricule { get; set; } = string.Empty;
    public string BadgeNumero { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string? CIN { get; set; }
    public int? ServiceId { get; set; }
    public Service? Service { get; set; }
    public int? PosteId { get; set; }
    public DateTime? DateEmbauche { get; set; }
    public bool Actif { get; set; } = true;
    public byte[]? Photo { get; set; }
}
