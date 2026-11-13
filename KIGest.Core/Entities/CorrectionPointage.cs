using System;

namespace KIGest.Core.Entities;

/// <summary>
/// Correction manuelle d'un pointage (entrée et/ou sortie) pour une journée donnée.
/// Conformément au §12 du cahier des charges, les pointages bruts ne sont jamais modifiés :
/// la correction est un enregistrement séparé, en plus des pointages bruts.
/// </summary>
public class CorrectionPointage
{
    public int Id { get; set; }

    public int EmployeId { get; set; }
    public Employe? Employe { get; set; }

    /// <summary>Journée concernée (date sans heure).</summary>
    public DateTime Date { get; set; }

    /// <summary>Nouvelle heure d'entrée (null = pas de correction d'entrée).</summary>
    public DateTime? EntreeCorrigee { get; set; }

    /// <summary>Nouvelle heure de sortie (null = pas de correction de sortie).</summary>
    public DateTime? SortieCorrigee { get; set; }

    public string? Motif { get; set; }
    public string? Auteur { get; set; }
    public DateTime DateModification { get; set; } = DateTime.Now;

    /// <summary>Pointages bruts d'origine au moment de la correction (facultatif).</summary>
    public string? PointagesBrutsOriginal { get; set; }
}
