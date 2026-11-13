using System;

namespace KIGest.Core.Entities;

/// <summary>
/// Paramètre clé/valeur stocké en base (société, tolérance, jours fériés, options...).
/// </summary>
public class Parametre
{
    public int Id { get; set; }
    public string Cle { get; set; } = string.Empty;
    public string? Valeur { get; set; }
    public string? Description { get; set; }
    public string Type { get; set; } = "Texte";
    public string? Groupe { get; set; }
    public DateTime DateModification { get; set; } = DateTime.Now;
}
