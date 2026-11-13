using System;
using System.Collections.Generic;
using System.Linq;
using KIGest.Core.Entities;

namespace KIGest.Core.Services;

public enum StatutPresence
{
    Present = 0,
    Retard = 1,
    Absent = 2,
    Conge = 3,
    Ferie = 4,
    Repos = 5,
    Inconnu = 6,
    /// <summary>Journée à venir : aucun pointage n'est encore possible.</summary>
    PasDePointage = 7
}

/// <summary>Pointage brut horodaté, avec le sens de passage si la pointeuse le renseigne.</summary>
public record PointageHorodate(DateTime DateHeure, int Sens);

/// <summary>
/// Résultat du calcul quotidien d'une présence (règles §9 du cahier des charges).
/// </summary>
public class PresenceCalculee
{
    public int EmployeId { get; set; }
    public DateTime Date { get; set; }
    public DateTime? Entree { get; set; }
    public DateTime? Sortie { get; set; }
    public double HeuresTravaillees { get; set; }
    public int RetardMin { get; set; }
    public int DepartAnticipeMin { get; set; }
    public double HeuresSup { get; set; }
    public StatutPresence Statut { get; set; } = StatutPresence.Inconnu;

    /// <summary>Message d'alerte (pointage incomplet, pointage isolé, etc.).</summary>
    public string? Alerte { get; set; }

    /// <summary>Nombre de pointages bruts retenus pour la journée.</summary>
    public int NombrePointages { get; set; }

    /// <summary>Vrai si la journée est encore ouverte (entrée sans sortie enregistrée).</summary>
    public bool JourneeEnCours => Entree.HasValue && !Sortie.HasValue;

    /// <summary>Vrai si au moins un pointage est un départ anticipé ou un retard notable.</summary>
    public bool Anormalie => RetardMin > 0 || DepartAnticipeMin > 0 || Alerte != null;
}

/// <summary>
/// Contexte nécessaire au calcul d'une journée.
/// </summary>
public class ContexteCalcul
{
    public int EmployeId { get; set; }
    public DateTime Date { get; set; }
    public IReadOnlyList<PointageHorodate> Pointages { get; set; } = Array.Empty<PointageHorodate>();
    public Horaire? Horaire { get; set; }
    public bool EstConge { get; set; }
    public bool EstFerie { get; set; }
    public bool EstJourDeRepos { get; set; }

    /// <summary>Tolérance de retard par défaut (minutes), si l'horaire n'en définit pas.</summary>
    public int ToleranceRetardMin { get; set; } = 0;

    /// <summary>Fenêtre anti-rebond : deux pointages à moins de N minutes sont fusionnés.</summary>
    public int AntiRebondMin { get; set; } = 2;

    /// <summary>Seuil d'alerte pour un retard important (minutes).</summary>
    public int SeuilAlerteRetardMin { get; set; } = 30;

    /// <summary>Durée de travail planifiée (hors pause) en minutes, si l'horaire est connu.</summary>
    public int? DureePlanifieeMin { get; set; }

    /// <summary>Date de référence (aujourd'hui). Les jours après deviennent « Pas de pointage ».</summary>
    public DateTime DateReference { get; set; } = DateTime.Today;

    /// <summary>Correction manuelle de l'heure d'entrée (remplace le pointage réel si fournie).</summary>
    public DateTime? EntreeCorrigee { get; set; }

    /// <summary>Correction manuelle de l'heure de sortie (remplace le pointage réel si fournie).</summary>
    public DateTime? SortieCorrigee { get; set; }
}

/// <summary>
/// Moteur de calcul des présences journalières.
/// </summary>
public class PresenceCalculator
{
    /// <summary>
    /// Calcule la présence d'un employé pour une journée donnée.
    /// </summary>
    public PresenceCalculee Calculer(ContexteCalcul ctx)
    {
        var resultat = new PresenceCalculee
        {
            EmployeId = ctx.EmployeId,
            Date = ctx.Date.Date
        };

        // 0. Jour à venir : aucun pointage n'est encore possible
        if (ctx.Date.Date > ctx.DateReference.Date)
        {
            resultat.Statut = StatutPresence.PasDePointage;
            resultat.Alerte = ctx.EstFerie
                ? "Jour férié à venir"
                : ctx.EstJourDeRepos
                    ? "Jour de repos à venir"
                    : "Journée à venir";
            return resultat;
        }

        // 1. Cas particuliers : congé, férié, jour de repos
        if (ctx.EstConge)
        {
            resultat.Statut = StatutPresence.Conge;
            return resultat;
        }

        if (ctx.EstFerie)
        {
            resultat.Statut = StatutPresence.Ferie;
            return resultat;
        }

        if (ctx.EstJourDeRepos)
        {
            resultat.Statut = StatutPresence.Repos;
            return resultat;
        }

        // 2. Filtrage anti-rebond (règle 5)
        var pointages = AppliquerAntiRebond(ctx.Pointages, Math.Max(0, ctx.AntiRebondMin));

        // 3. Fenêtre d'horaire de nuit (règle 6) : la journée suit la date de début de poste
        if (ctx.Horaire?.NuitOuiNon == true)
        {
            pointages = pointages
                .Where(p => p.DateHeure >= ctx.Date.Date.AddHours(-12) && p.DateHeure < ctx.Date.Date.AddDays(1).AddHours(12))
                .ToList();
        }
        else
        {
            pointages = pointages
                .Where(p => p.DateHeure >= ctx.Date.Date && p.DateHeure < ctx.Date.Date.AddDays(1))
                .ToList();
        }

        resultat.NombrePointages = pointages.Count;

        // 4. Cas sans pointage brut : absent, sauf si une correction est fournie (entrée manuelle justifiée)
        if (pointages.Count == 0 && !ctx.EntreeCorrigee.HasValue && !ctx.SortieCorrigee.HasValue)
        {
            resultat.Statut = StatutPresence.Absent;
            return resultat;
        }

        // 5. Appariement entrée / sortie (règle 4)
        //    On apparie chronologiquement : (1er, 2e), (3e, 4e)...
        //    Si le nombre de pointages est impair, le dernier est une entrée sans sortie.
        var sorties = new List<DateTime>();

        if (pointages.Count >= 2)
        {
            for (int i = 1; i + 1 < pointages.Count; i += 2)
            {
                sorties.Add(pointages[i + 1].DateHeure);
            }

            // Le dernier pointage n'est appariable que si le nombre total est pair
            if (pointages.Count % 2 == 0)
            {
                sorties.Add(pointages[pointages.Count - 1].DateHeure);
            }
        }

        resultat.Entree = pointages.Count > 0 ? pointages[0].DateHeure : null;
        resultat.Sortie = pointages.Count > 0 ? (sorties.Count > 0 ? sorties[sorties.Count - 1] : null) : null;

        // 5bis. Application des corrections manuelles AVANT les alertes (elles remplacent les valeurs brutes)
        bool correctionAppliquee = false;
        if (ctx.EntreeCorrigee.HasValue) { resultat.Entree = ctx.EntreeCorrigee; correctionAppliquee = true; }
        if (ctx.SortieCorrigee.HasValue) { resultat.Sortie = ctx.SortieCorrigee; correctionAppliquee = true; }

        if (pointages.Count == 1)
        {
            resultat.Alerte = $"Pointage incomplet : entrée à {resultat.Entree:HH:mm} sans sortie enregistrée.";
        }
        else if (pointages.Count % 2 == 1 && pointages.Count > 0)
        {
            resultat.Alerte = $"Appariement incertain : {pointages.Count} pointages, dernière entrée à {pointages[pointages.Count - 1].DateHeure:HH:mm} sans sortie.";
        }
        else if (pointages.Count == 0 && (correctionAppliquee || resultat.Entree.HasValue))
        {
            resultat.Alerte = "Saisie manuelle (aucun pointage brut enregistré sur l'appareil).";
        }

        if (correctionAppliquee)
        {
            resultat.Alerte = (resultat.Alerte != null ? resultat.Alerte + " | " : string.Empty) + "Heure(s) corrigée(s)";
        }

        // 6. Retard (règle 1)
        int tolerance = ctx.Horaire?.ToleranceRetardMin ?? 0;
        if (ctx.Horaire == null || ctx.Horaire.ToleranceRetardMin == 0)
            tolerance = ctx.ToleranceRetardMin;

        if (ctx.Horaire != null && resultat.Entree.HasValue)
        {
            var debut = ctx.Date.Date.Add(ctx.Horaire.HeureDebut);
            double retard = (resultat.Entree.Value - debut.AddMinutes(tolerance)).TotalMinutes;
            resultat.RetardMin = retard > 0 ? (int)Math.Round(retard) : 0;
        }

        // 7. Départ anticipé (règle 2) : uniquement si une sortie est connue
        if (ctx.Horaire != null && resultat.Sortie.HasValue)
        {
            var fin = ctx.Date.Date.Add(ctx.Horaire.HeureFin);
            double ecart = (fin - resultat.Sortie.Value).TotalMinutes;
            resultat.DepartAnticipeMin = ecart > 0 ? (int)Math.Round(ecart) : 0;
        }

        // 8. Heures travaillées (règle 3) : uniquement si l'entrée et la sortie sont connues
        if (resultat.Entree.HasValue && resultat.Sortie.HasValue)
        {
            double minutesTravailees = (resultat.Sortie.Value - resultat.Entree.Value).TotalMinutes;
            if (minutesTravailees < 0) minutesTravailees = 0;

            double pauseMinutes = 0;
            if (ctx.Horaire?.PauseDebut != null && ctx.Horaire.PauseFin != null)
            {
                var pauseDebut = ctx.Date.Date.Add(ctx.Horaire.PauseDebut.Value);
                var pauseFin = ctx.Date.Date.Add(ctx.Horaire.PauseFin.Value);

                bool pauseBadgee = pointages.Any(p => p.DateHeure >= pauseDebut && p.DateHeure <= pauseFin);

                if (!pauseBadgee)
                {
                    // La pause théorique de l'horaire est déduite
                    pauseMinutes = (pauseFin - pauseDebut).TotalMinutes;
                }
            }

            resultat.HeuresTravaillees = Math.Max(0, minutesTravailees - pauseMinutes) / 60.0;
        }

        // 9. Heures supplémentaires (règle 7)
        int? planifie = ctx.DureePlanifieeMin ?? CalculerDureePlanifiee(ctx.Horaire);
        if (planifie.HasValue && planifie.Value > 0 && resultat.HeuresTravaillees > 0)
        {
            double sup = resultat.HeuresTravaillees * 60 - planifie.Value;
            resultat.HeuresSup = sup > 0 ? Math.Round(sup / 60.0, 2) : 0;
        }

        // 10. Statut final
        resultat.Statut = resultat.RetardMin > 0 ? StatutPresence.Retard : StatutPresence.Present;

        // Alerte retard important
        if (resultat.RetardMin >= ctx.SeuilAlerteRetardMin && resultat.Alerte == null)
        {
            resultat.Alerte = $"Retard important de {FormatDuree(resultat.RetardMin)}.";
        }

        return resultat;
    }

    /// <summary>
    /// Supprime les pointages en doublon (règle 5 : moins de N minutes d'écart).
    /// </summary>
    public static List<PointageHorodate> AppliquerAntiRebond(IEnumerable<PointageHorodate> pointages, int minutes)
    {
        var triés = pointages.OrderBy(p => p.DateHeure).ToList();
        if (minutes <= 0 || triés.Count <= 1) return triés;

        var resultat = new List<PointageHorodate> { triés[0] };

        for (int i = 1; i < triés.Count; i++)
        {
            if ((triés[i].DateHeure - resultat[resultat.Count - 1].DateHeure).TotalMinutes >= minutes)
            {
                resultat.Add(triés[i]);
            }
        }

        return resultat;
    }

    /// <summary>Formate des minutes en « 2 h 37 min » (ou « 45 min » si < 1 h).</summary>
    public static string FormatDuree(int totalMinutes)
    {
        int h = totalMinutes / 60, m = totalMinutes % 60;
        return h > 0 ? $"{h} h {m:00} min" : $"{m} min";
    }

    /// <summary>Durée de travail planifiée en minutes (hors pause).</summary>
    public static int? CalculerDureePlanifiee(Horaire? horaire)
    {
        if (horaire == null) return null;

        double total = (horaire.HeureFin - horaire.HeureDebut).TotalMinutes;
        if (total < 0) total += 24 * 60; // poste de nuit

        if (horaire.PauseDebut != null && horaire.PauseFin != null)
        {
            double pause = (horaire.PauseFin.Value - horaire.PauseDebut.Value).TotalMinutes;
            if (pause > 0 && pause < total) total -= pause;
        }

        return (int)Math.Round(total);
    }

    /// <summary>
    /// Détermine si une date tombe un jour de repos selon la semaine de travail
    /// (5 j, 5.5 j ou 6 j — §11 du cahier des charges).
    /// </summary>
    public static bool EstJourDeRepos(DateTime date, double joursTravailSemaine)
    {
        int jour = (int)date.DayOfWeek; // 0 = dimanche

        return joursTravailSemaine switch
        {
            <= 5 => jour == 0 || jour == 6,                    // 5 jours : dimanche + samedi
            < 6 => jour == 0,                                  // 5.5 jours : dimanche seul
            _ => jour == 0                                     // 6 jours : dimanche seul
        };
    }
}
