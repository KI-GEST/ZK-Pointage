using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Core.Services;
using KIGest.Data.Context;
using KIGest.Data.Services;

namespace KIGest.App.Services;

public record PresencePeriodeResult(
    List<Employe> Employes,
    List<PresenceCalculee> Resultats,
    List<Employe> SansHoraire);

/// <summary>
/// Calcul des présences sur une période (jours × employés), utilisé par les rapports.
/// Même règle que la vue Présences : affectation employé > affectation service > horaire par défaut,
/// congés approuvés uniquement, fériés et jours de repos non pointés.
/// </summary>
public static class PresencePeriode
{
    public static PresencePeriodeResult Calculer(KiGestDbContext ctx, ParametresService parametres,
        DateTime debut, DateTime fin, int? serviceId, string? recherche)
    {
        var employes = ctx.Employes.AsNoTracking().Where(e => e.Actif);

        if (serviceId.HasValue)
            employes = employes.Where(e => e.ServiceId == serviceId.Value);

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            string r = recherche.Trim();
            employes = employes.Where(e => e.Nom.Contains(r) || e.Prenom.Contains(r) || e.Matricule.Contains(r) || e.BadgeNumero.Contains(r));
        }

        var listeEmployes = employes.OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ToList();

        int nbJours = Math.Max(1, (int)(fin.Date - debut.Date).TotalDays);
        var badges = listeEmployes.Select(e => e.BadgeNumero).ToList();

        var pointages = ctx.PointagesBruts.AsNoTracking()
            .Where(p => badges.Contains(p.BadgeNumero) && p.DateHeure >= debut && p.DateHeure < fin)
            .Select(p => new { p.BadgeNumero, p.DateHeure, p.SensES })
            .ToList();

        var parBadge = pointages
            .GroupBy(p => p.BadgeNumero)
            .ToDictionary(g => g.Key, g => g.Select(x => new PointageHorodate(x.DateHeure, x.SensES)).ToList());

        var horaires = ctx.Horaires.AsNoTracking().ToList();

        var affectations = ctx.AffectationsHoraires.AsNoTracking()
            .Where(a => a.DateDebut < fin && (a.DateFin == null || a.DateFin >= debut))
            .ToList();

        var affectationsService = ctx.AffectationsServiceHoraires.AsNoTracking()
            .Where(a => a.DateDebut < fin && (a.DateFin == null || a.DateFin >= debut))
            .ToList();

        var conges = ctx.Conges.AsNoTracking()
            .Where(c => c.DateDebut < fin && c.DateFin >= debut
                     && c.Statut.ToUpper() == "APPROUVE")
            .ToList();

        var feries = ctx.JoursFeries.AsNoTracking()
            .Where(f => f.Date >= debut && f.Date < fin)
            .Select(f => f.Date.Date)
            .ToHashSet();

        double joursTravail = parametres.GetDouble("Presence.JoursTravailSemaine", 5);
        int toleranceDefaut = parametres.GetInt("Pointage.ToleranceRetardMin", 10);
        int antiRebond = parametres.GetInt("Pointage.AntiRebondMin", 2);
        int seuilAlerte = parametres.GetInt("Presence.SeuilAlerteRetardMin", 30);

        string horaireParDefautLibelle = parametres.Get("Pointage.HoraireParDefaut", "");
        var horaireParDefaut = !string.IsNullOrWhiteSpace(horaireParDefautLibelle)
            ? horaires.FirstOrDefault(h => h.Libelle == horaireParDefautLibelle)
            : null;

        var employesIds = listeEmployes.Select(e => e.Id).ToList();
        var corrections = ctx.CorrectionsPointages.AsNoTracking()
            .Where(c => employesIds.Contains(c.EmployeId) && c.Date >= debut && c.Date < fin)
            .ToDictionary(c => (c.EmployeId, c.Date.Date));

        var calculateur = new PresenceCalculator();
        var resultats = new List<PresenceCalculee>();
        var sansHoraire = new List<Employe>();

        foreach (var employe in listeEmployes)
        {
            bool aEmployeAffecte = affectations.Any(a => a.EmployeId == employe.Id
                && a.DateDebut < fin && (a.DateFin == null || a.DateFin >= debut));
            bool aServiceAffecte = employe.ServiceId.HasValue
                && affectationsService.Any(a => a.ServiceId == employe.ServiceId.Value
                    && a.DateDebut < fin && (a.DateFin == null || a.DateFin >= debut));

            if (!aEmployeAffecte && !aServiceAffecte)
            {
                sansHoraire.Add(employe);
                continue;
            }

            var employePointages = parBadge.TryGetValue(employe.BadgeNumero, out var pts)
                ? pts
                : new List<PointageHorodate>();

            for (int jour = 0; jour < nbJours; jour++)
            {
                var date = debut.Date.AddDays(jour);
                var journee = employePointages.Where(p => p.DateHeure.Date == date.Date).ToList();

                var affectation = affectations.FirstOrDefault(a => a.EmployeId == employe.Id
                    && a.DateDebut <= date
                    && (a.DateFin == null || a.DateFin >= date));

                Horaire? horaire = null;
                if (affectation != null)
                {
                    horaire = horaires.FirstOrDefault(h => h.Id == affectation.HoraireId);
                }
                else if (employe.ServiceId.HasValue)
                {
                    var affectationSrv = affectationsService
                        .Where(a => a.ServiceId == employe.ServiceId.Value && a.DateDebut <= date && (a.DateFin == null || a.DateFin >= date))
                        .OrderByDescending(a => a.DateDebut)
                        .FirstOrDefault();

                    if (affectationSrv != null)
                        horaire = horaires.FirstOrDefault(h => h.Id == affectationSrv.HoraireId);
                }

                if (horaire == null && horaireParDefaut != null)
                    horaire = horaireParDefaut;

                bool estConge = conges.Any(c => c.EmployeId == employe.Id
                    && c.DateDebut <= date && c.DateFin >= date);
                bool estFerie = feries.Contains(date.Date);
                bool estRepos = PresenceCalculator.EstJourDeRepos(date, joursTravail);

                corrections.TryGetValue((employe.Id, date.Date), out var correction);

                resultats.Add(calculateur.Calculer(new ContexteCalcul
                {
                    EmployeId = employe.Id,
                    Date = date,
                    Pointages = journee,
                    Horaire = horaire,
                    EstConge = estConge,
                    EstFerie = estFerie,
                    EstJourDeRepos = estRepos,
                    ToleranceRetardMin = toleranceDefaut,
                    AntiRebondMin = antiRebond,
                    SeuilAlerteRetardMin = seuilAlerte,
                    DateReference = DateTime.Today,
                    EntreeCorrigee = correction?.EntreeCorrigee,
                    SortieCorrigee = correction?.SortieCorrigee
                }));
            }
        }

        return new PresencePeriodeResult(listeEmployes, resultats, sansHoraire);
    }

    /// <summary>Libellé français du statut de présence (exports).</summary>
    public static string StatutFr(StatutPresence statut) => statut switch
    {
        StatutPresence.Present => "Présent",
        StatutPresence.Retard => "Retard",
        StatutPresence.Absent => "Absent",
        StatutPresence.Conge => "Congé",
        StatutPresence.Ferie => "Férié",
        StatutPresence.Repos => "Repos",
        StatutPresence.PasDePointage => "À venir",
        _ => "Inconnu"
    };
}
