using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Core.Services;
using KIGest.Data.Context;
using KIGest.Data.Services;

namespace KIGest.App.Views;

public partial class PresencesView : Page
{
    /// <summary>Une ligne = un employé sur une journée, avec le détail du calcul.</summary>
    public class LignePresence : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _entree = "";
        private string _sortie = "";
        private string _heures = "";
        private string _retard = "";
        private string _depart = "";
        private string _heuresSup = "";
        private string _alerte = "";
        private string _partiTot = "—";

        public int EmployeId { get; set; }
        public string Matricule { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int NbJoursMois { get; set; }
        public StatutPresence Statut { get; set; } = StatutPresence.Inconnu;
        public PresenceCalculee? Calcul { get; set; }

        /// <summary>Vrai si une correction manuelle a été appliquée pour cette journée.</summary>
        public bool Corrigee { get; set; }

        /// <summary>Clé de regroupement : matricule + nom.</summary>
        public string GroupeEmploye => $"{Matricule} — {Nom} {Prenom}".Trim();

        public string JourSemaine => Date.ToString("ddd", CultureInfo.GetCultureInfo("fr-FR"));

        public string EntreeAffichee { get => _entree; set { _entree = value; Notifier(); } }
        public string SortieAffichee { get => _sortie; set { _sortie = value; Notifier(); } }
        public string HeuresAffichee { get => _heures; set { _heures = value; Notifier(); } }
        public string RetardAffichee { get => _retard; set { _retard = value; Notifier(); } }
        public string DepartAffichee { get => _depart; set { _depart = value; Notifier(); } }
        public string HeuresSupAffichee { get => _heuresSup; set { _heuresSup = value; Notifier(); } }
        public string PartiTotAffichee { get => _partiTot; set { _partiTot = value; Notifier(); } }
        public string Alerte { get => _alerte; set { _alerte = value; Notifier(); } }

        /// <summary>Libellé de l'horaire utilisé pour ce calcul (affectation employé, service, ou par défaut).</summary>
        public string HoraireAffiche { get; set; } = "—";

        private void Notifier([System.Runtime.CompilerServices.CallerMemberName] string? prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    private readonly KiGestDbContext _ctx;
    private readonly ParametresService _parametres;
    private readonly PresenceCalculator _calculateur = new();

    private List<PresenceCalculee> _resultats = new();
    private List<LignePresence> _lignes = new();
    private Dictionary<int, Employe> _employesParId = new();
    private Dictionary<(int EmployeId, DateTime Date), CorrectionPointage> _correctionsParJour = new();
    private Dictionary<(int EmployeId, DateTime Date), string> _horairesParJour = new();
    private List<Employe> _employesSansHoraire = new();
    private LignePresence? _ligneSelectionnee;
    private int _nbJoursMois = 1;
    private DateTime _debutReference = DateTime.Today;

    public PresencesView(KiGestDbContext ctx, ParametresService parametres)
    {
        _ctx = ctx;
        _parametres = parametres;
        InitializeComponent();

        DtpDate.SelectedDate = DateTime.Today;
        CboVue.ItemsSource = new[] { "À partir du", "Mensuel" };
        CboVue.SelectedIndex = 0;

        for (int m = 1; m <= 12; m++)
            CboMois.Items.Add(new MoisItem(m, new DateTime(DateTime.Today.Year, m, 1).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"))));
        CboMois.SelectedIndex = DateTime.Today.Month - 1;

        var services = _ctx.Services.AsNoTracking().OrderBy(s => s.Libelle).ToList();
        CboService.ItemsSource = services;
        CboService.SelectedIndex = -1;

        CboStatut.ItemsSource = new[] { "Tous", "Présent", "Retard", "Absent", "Congé", "Férié", "Repos", "Pas de pointage" };
        CboStatut.SelectedIndex = 0;

        AppliquerVue();
    }

    public class MoisItem
    {
        public int Mois { get; }
        public string Libelle { get; }
        public MoisItem(int mois, string libelle) { Mois = mois; Libelle = libelle; }
        public override string ToString() => Libelle;
    }

    // ==================== Filtres & vue ====================

    /// <summary>Formate des minutes en « 2 h 10 min ».</summary>
    private static string FormatDuree(int totalMinutes)
    {
        int h = totalMinutes / 60, m = totalMinutes % 60;
        return h > 0 ? $"{h} h {m:00} min" : $"{m} min";
    }


    private void AppliquerVue()
    {
        bool mensuel = CboVue.SelectedItem?.ToString() == "Mensuel";

        DtpDate.Visibility = mensuel ? Visibility.Collapsed : Visibility.Visible;
        LblDate.Visibility = mensuel ? Visibility.Collapsed : Visibility.Visible;
        CboMois.Visibility = mensuel ? Visibility.Visible : Visibility.Collapsed;
        LblMois.Visibility = mensuel ? Visibility.Visible : Visibility.Collapsed;

        TxtTitre.Text = mensuel
            ? $"Détail mensuel — {CboMois.SelectedItem} (une ligne par jour, regroupée par employé)"
            : $"Présences du {((DateTime?)DtpDate.SelectedDate ?? DateTime.Today):dd/MM/yyyy} à aujourd'hui";
    }

    private void CboVue_SelectionChanged(object sender, SelectionChangedEventArgs e) => AppliquerVue();
    private void DtpDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => AppliquerVue();
    private void CboMois_SelectionChanged(object sender, SelectionChangedEventArgs e) => AppliquerVue();
    private void CboService_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    private void TxtRecherche_TextChanged(object sender, TextChangedEventArgs e) { }
    private void CboStatut_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private List<Employe> GetEmployes()
    {
        var query = _ctx.Employes.AsNoTracking();

        if (CboService.SelectedItem is Service service)
            query = query.Where(e => e.ServiceId == service.Id);

        string recherche = TxtRecherche.Text.Trim();
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            query = query.Where(e =>
                e.Nom.Contains(recherche) ||
                e.Prenom.Contains(recherche) ||
                e.Matricule.Contains(recherche) ||
                e.BadgeNumero.Contains(recherche));
        }

        return query.OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ToList();
    }

    // ==================== Calcul ====================

    private void BtnCalculer_Click(object sender, RoutedEventArgs e) => Calculer();

    private void Calculer()
    {
        try
        {
            BtnCalculer.IsEnabled = false;
            TxtStatut.Text = "Calcul en cours...";

            var employes = GetEmployes();
            if (employes.Count == 0)
            {
                TxtStatut.Text = "Aucun employé à traiter.";
                return;
            }

            bool mensuel = CboVue.SelectedItem?.ToString() == "Mensuel";
            DateTime reference = mensuel
                ? new DateTime(DateTime.Today.Year, ((MoisItem)CboMois.SelectedItem).Mois, 1)
                : (DtpDate.SelectedDate ?? DateTime.Today);

            // « À partir du » : depuis la date choisie jusqu'à aujourd'hui (les jours futurs sont exclus)
            int nbJours = mensuel
                ? DateTime.DaysInMonth(reference.Year, reference.Month)
                : Math.Max(1, (DateTime.Today.Date - reference.Date).Days + 1);

            var debut = reference.Date;
            var fin = reference.Date.AddDays(nbJours);

            var badges = employes.Select(e => e.BadgeNumero).ToList();
            var pointages = _ctx.PointagesBruts
                .AsNoTracking()
                .Where(p => badges.Contains(p.BadgeNumero) && p.DateHeure >= debut && p.DateHeure < fin)
                .Select(p => new { p.BadgeNumero, p.DateHeure, p.SensES })
                .ToList();

            var parBadge = pointages
                .GroupBy(p => p.BadgeNumero)
                .ToDictionary(g => g.Key, g => g.Select(x => new PointageHorodate(x.DateHeure, x.SensES)).ToList());

            var horaires = _ctx.Horaires.AsNoTracking().ToList();
            var affectations = _ctx.AffectationsHoraires
                .AsNoTracking()
                .Where(a => a.DateDebut < fin && (a.DateFin == null || a.DateFin >= debut))
                .ToList();

            var conges = _ctx.Conges
                .AsNoTracking()
                .Where(c => c.DateDebut < fin && c.DateFin >= debut)
                .ToList();

            var feries = _ctx.JoursFeries
                .AsNoTracking()
                .Where(f => f.Date >= debut && f.Date < fin)
                .Select(f => f.Date.Date)
                .ToHashSet();

            double joursTravail = _parametres.GetDouble("Presence.JoursTravailSemaine", 5);
            int toleranceDefaut = _parametres.GetInt("Pointage.ToleranceRetardMin", 10);
            int antiRebond = _parametres.GetInt("Pointage.AntiRebondMin", 2);
            int seuilAlerte = _parametres.GetInt("Presence.SeuilAlerteRetardMin", 30);

            // Horaire par défaut depuis les paramètres (si aucune affectation ne correspond)
            string horaireParDefautLibelle = _parametres.Get("Pointage.HoraireParDefaut", "");
            var horaireParDefaut = !string.IsNullOrWhiteSpace(horaireParDefautLibelle)
                ? horaires.FirstOrDefault(h => h.Libelle == horaireParDefautLibelle)
                : null;

            _resultats = new List<PresenceCalculee>();
            _employesParId = employes.ToDictionary(e => e.Id);
            _nbJoursMois = nbJours;
            _debutReference = debut;
            _employesSansHoraire = new List<Employe>();

            // Corrections manuelles enregistrées en base
            var employesIds = employes.Select(e => e.Id).ToList();
            _correctionsParJour = _ctx.CorrectionsPointages
                .AsNoTracking()
                .Where(c => employesIds.Contains(c.EmployeId) && c.Date >= debut && c.Date < fin)
                .ToList()
                .ToDictionary(c => (c.EmployeId, c.Date));

            _horairesParJour.Clear();

            // Affectations par service (fallback d'un horaire employe absent)
            var affectationsService = _ctx.AffectationsServiceHoraires
                .AsNoTracking()
                .Where(a => a.DateDebut < fin && (a.DateFin == null || a.DateFin >= debut))
                .ToList();

            foreach (var employe in employes)
            {
                // L'horaire par défaut ne remplace pas une affectation : un employé doit
                // avoir une affectation individuelle ou de service pour être affiché.
                bool aEmployeAffecte = affectations.Any(a => a.EmployeId == employe.Id
                    && a.DateDebut < fin && (a.DateFin == null || a.DateFin >= debut));
                bool aServiceAffecte = employe.ServiceId.HasValue
                    && affectationsService.Any(a => a.ServiceId == employe.ServiceId.Value
                        && a.DateDebut < fin && (a.DateFin == null || a.DateFin >= debut));

                if (!aEmployeAffecte && !aServiceAffecte)
                {
                    _employesSansHoraire.Add(employe);
                    continue; // aucune affectation → pas d'affichage + avertissement
                }

                var employePointages = parBadge.TryGetValue(employe.BadgeNumero, out var pts)
                    ? pts
                    : new List<PointageHorodate>();

                for (int jour = 0; jour < nbJours; jour++)
                {
                    var date = debut.AddDays(jour);
                    var journee = employePointages.Where(p => p.DateHeure.Date == date.Date).ToList();

                    // Priorité : affectation employé > affectation service > défaut
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
                            .Where(a => a.ServiceId == employe.ServiceId.Value
                                     && a.DateDebut <= date
                                     && (a.DateFin == null || a.DateFin >= date))
                            .OrderByDescending(a => a.DateDebut)
                            .FirstOrDefault();

                        if (affectationSrv != null)
                            horaire = horaires.FirstOrDefault(h => h.Id == affectationSrv.HoraireId);
                    }

                    // Repli sur l'horaire par défaut des paramètres
                    if (horaire == null && horaireParDefaut != null)
                        horaire = horaireParDefaut;

                    // Mémoriser la référence d'horaire utilisée pour l'affichage
                    _horairesParJour[(employe.Id, date.Date)] = horaire?.Libelle ?? "Défaut";

                    bool estConge = conges.Any(c => c.EmployeId == employe.Id
                        && c.DateDebut <= date && c.DateFin >= date
                        && string.Equals(c.Statut, "Approuve", StringComparison.OrdinalIgnoreCase));

                    bool estFerie = feries.Contains(date.Date);
                    bool estRepos = PresenceCalculator.EstJourDeRepos(date, joursTravail);

                    _correctionsParJour.TryGetValue((employe.Id, date.Date), out var correction);

                    _resultats.Add(_calculateur.Calculer(new ContexteCalcul
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

            AfficherResultats(_employesParId, _nbJoursMois);
            BtnEnregistrer.IsEnabled = _resultats.Any(r => r.Statut != StatutPresence.Inconnu);

            if (_employesSansHoraire.Count > 0)
            {
                var noms = string.Join(", ", _employesSansHoraire.Take(10).Select(e => $"{e.Nom} {e.Prenom}"));
                TxtAvertissement.Text = $"{_employesSansHoraire.Count} employé(s) sans affectation horaire — non affichés ici. " +
                                        $"Exemples : {noms}. " +
                                        $"Affectez-leur un horaire dans « Horaires » (par employé ou par service).";
                BarreAvertissement.Visibility = Visibility.Visible;
            }
            else
            {
                BarreAvertissement.Visibility = Visibility.Collapsed;
            }

            TxtStatut.Text = $"{_resultats.Count} journée(s) calculée(s) — {_employesSansHoraire.Count} employé(s) masqué(s) (sans affectation)";
        }
        catch (Exception ex)
        {
            TxtStatut.Text = "Erreur de calcul : " + ex.Message;
        }
        finally
        {
            BtnCalculer.IsEnabled = true;
        }
    }

    private void AfficherResultats(Dictionary<int, Employe> employesParId, int nbJoursMois)
    {
        string filtreStatut = CboStatut.SelectedItem?.ToString() ?? "Tous";

        var statutsMapping = new Dictionary<string, StatutPresence>
        {
            { "Présent", StatutPresence.Present },
            { "Retard", StatutPresence.Retard },
            { "Absent", StatutPresence.Absent },
            { "Congé", StatutPresence.Conge },
            { "Férié", StatutPresence.Ferie },
            { "Repos", StatutPresence.Repos },
            { "Pas de pointage", StatutPresence.PasDePointage }
        };

        _lignes = _resultats
            .Where(r => employesParId.ContainsKey(r.EmployeId))
            .Select(r =>
            {
                employesParId.TryGetValue(r.EmployeId, out var emp);

                return new LignePresence
                {
                    EmployeId = r.EmployeId,
                    Matricule = emp?.Matricule ?? "",
                    Nom = string.IsNullOrWhiteSpace(emp?.Nom) ? emp?.BadgeNumero ?? "" : emp!.Nom,
                    Prenom = emp?.Prenom ?? "",
                    Date = r.Date,
                    NbJoursMois = nbJoursMois,
                    Statut = r.Statut,
                    Calcul = r,
                    Corrigee = _correctionsParJour.ContainsKey((r.EmployeId, r.Date)),
                    EntreeAffichee = r.Entree?.ToString("HH:mm") ?? "—",
                    SortieAffichee = r.Sortie?.ToString("HH:mm") ?? "—",
                    HeuresAffichee = r.Sortie.HasValue
                        ? $"{r.HeuresTravaillees:0.00} h"
                        : (r.Statut == StatutPresence.Present || r.Statut == StatutPresence.Retard ? "en cours" : "—"),
                    RetardAffichee = r.RetardMin > 0 ? $"{r.RetardMin} min" : "—",
                    DepartAffichee = r.DepartAnticipeMin > 0 ? $"{r.DepartAnticipeMin} min" : "—",
                    PartiTotAffichee = r.DepartAnticipeMin > 0 ? "OUI (−" + FormatDuree(r.DepartAnticipeMin) + ")" : "—",
                    HeuresSupAffichee = r.HeuresSup > 0 ? $"{r.HeuresSup:0.00} h" : "—",
                    Alerte = r.Alerte ?? "",
                    HoraireAffiche = _horairesParJour.TryGetValue((r.EmployeId, r.Date), out var horaireLib) ? horaireLib : "—"
                 };
            })
            .Where(l => filtreStatut == "Tous"
                        || (statutsMapping.TryGetValue(filtreStatut, out var statutFiltre) && l.Statut == statutFiltre))
            .OrderBy(l => l.Nom).ThenBy(l => l.Prenom).ThenBy(l => l.Date)
            .ToList();

        // Regroupement par employé
        var vue = new CollectionViewSource { Source = _lignes };
        vue.GroupDescriptions.Add(new PropertyGroupDescription("GroupeEmploye"));
        GrdPresences.ItemsSource = vue.View;
    }

    // ==================== Enregistrement ====================

    private void BtnEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnEnregistrer.IsEnabled = false;

            int crees = 0, modifies = 0;

            foreach (var calcul in _resultats)
            {
                if (calcul.Statut == StatutPresence.Inconnu || calcul.Statut == StatutPresence.PasDePointage)
                    continue;

                var existant = _ctx.PresencesJournalieres
                    .FirstOrDefault(p => p.EmployeId == calcul.EmployeId && p.Date == calcul.Date);

                if (existant == null)
                {
                    _ctx.PresencesJournalieres.Add(new PresenceJournalier
                    {
                        EmployeId = calcul.EmployeId,
                        Date = calcul.Date,
                        Entree = calcul.Entree,
                        Sortie = calcul.Sortie,
                        HeuresTravaillees = calcul.HeuresTravaillees,
                        RetardMin = calcul.RetardMin,
                        DepartAnticipeMin = calcul.DepartAnticipeMin,
                        HeuresSup = calcul.HeuresSup,
                        Statut = calcul.Statut.ToString()
                    });
                    crees++;
                }
                else
                {
                    existant.Entree = calcul.Entree;
                    existant.Sortie = calcul.Sortie;
                    existant.HeuresTravaillees = calcul.HeuresTravaillees;
                    existant.RetardMin = calcul.RetardMin;
                    existant.DepartAnticipeMin = calcul.DepartAnticipeMin;
                    existant.HeuresSup = calcul.HeuresSup;
                    existant.Statut = calcul.Statut.ToString();
                    modifies++;
                }
            }

            _ctx.SaveChanges();
            TxtStatut.Text = $"{crees} présence(s) créée(s), {modifies} mise(s) à jour dans la base.";
        }
        catch (Exception ex)
        {
            TxtStatut.Text = "Erreur d'enregistrement : " + ex.Message;
        }
        finally
        {
            BtnEnregistrer.IsEnabled = _resultats.Any();
        }
    }

    private void BtnExcel_Click(object sender, RoutedEventArgs e)
    {
        TxtStatut.Text = "Export Excel - à venir.";
    }

    // ==================== Correction manuelle d'une journée ====================

    private void GrdPresences_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ligneSelectionnee = GrdPresences.SelectedItem as LignePresence;
        BtnCorriger.IsEnabled = _ligneSelectionnee != null;
    }

    private void BtnCorriger_Click(object sender, RoutedEventArgs e)
    {
        if (_ligneSelectionnee == null)
        {
            MessageBox.Show("Sélectionnez une ligne dans la grille.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ligne = _ligneSelectionnee;
        var correctionExistante = _ctx.CorrectionsPointages
            .FirstOrDefault(c => c.EmployeId == ligne.EmployeId && c.Date == ligne.Date);

        var calcul = ligne.Calcul;
        string? correctionEnCours = correctionExistante != null
            ? $"Correction existante — Entrée: {(correctionExistante.EntreeCorrigee.HasValue ? correctionExistante.EntreeCorrigee.Value.ToString("HH:mm") : "(brute)")}  Sortie: {(correctionExistante.SortieCorrigee.HasValue ? correctionExistante.SortieCorrigee.Value.ToString("HH:mm") : "(brute)")}  Motif: {correctionExistante.Motif}"
            : null;

        var dialog = new CorrigerPresenceDialog(
            $"{ligne.Matricule} — {ligne.Nom} {ligne.Prenom}",
            ligne.Date,
            correctionExistante?.EntreeCorrigee ?? calcul?.Entree,
            correctionExistante?.SortieCorrigee ?? calcul?.Sortie,
            correctionExistante?.Motif)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true) return;

        // Valeurs avant modification (pour le journal)
        string avantEntree = ligne.Calcul?.Entree?.ToString("HH:mm") ?? "(aucune)";
        string avantSortie = ligne.Calcul?.Sortie?.ToString("HH:mm") ?? "(aucune)";
        string brutOriginal = $"entree_brute={avantEntree} sortie_brute={avantSortie}";

        try
        {
            if (dialog.ResetRequested)
            {
                if (correctionExistante != null)
                {
                    _ctx.CorrectionsPointages.Remove(correctionExistante);
                    _ctx.Journaux.Add(new Journal
                    {
                        DateHeure = DateTime.Now,
                        Action = "CORRECTION_POINTAGE_EFFACEE",
                        Details = $"EmployeId={ligne.EmployeId} Date={ligne.Date:dd/MM/yyyy} — rétablissement des pointages bruts"
                    });
                    _ctx.SaveChanges();
                }

                TxtStatut.Text = "Correction effacée. Rétablissement des pointages bruts au prochain calcul.";
                Calculer();
                return;
            }

            // Construire la correction (on ne modifie jamais PointagesBruts)
            var correction = correctionExistante ?? new CorrectionPointage
            {
                EmployeId = ligne.EmployeId,
                Date = ligne.Date,
                Auteur = "Application",
                PointagesBrutsOriginal = brutOriginal
            };

            if (correctionExistante == null)
                _ctx.CorrectionsPointages.Add(correction);

            correction.EntreeCorrigee = dialog.EntreeCorrigee;
            correction.SortieCorrigee = dialog.SortieCorrigee;
            correction.Motif = dialog.Motif;
            correction.Auteur = correctionExistante?.Auteur ?? "Application";
            correction.DateModification = DateTime.Now;
            if (string.IsNullOrWhiteSpace(correction.PointagesBrutsOriginal))
                correction.PointagesBrutsOriginal = brutOriginal;

            // Journal d'audit (§12 cahier des charges)
            _ctx.Journaux.Add(new Journal
            {
                DateHeure = DateTime.Now,
                Action = "CORRECTION_POINTAGE",
                Details = $"EmployeId={ligne.EmployeId} Date={ligne.Date:dd/MM/yyyy} avant: entree={avantEntree} sortie={avantSortie} | apres: entree={(dialog.EntreeCorrigee?.ToString("HH:mm") ?? "(brute)")} sortie={(dialog.SortieCorrigee?.ToString("HH:mm") ?? "(brute)")} | Motif: {dialog.Motif}{(correctionEnCours != null ? " | (correction d'une correction)" : string.Empty)}"
            });

            _ctx.SaveChanges();
            TxtStatut.Text = $"Correction enregistrée pour {ligne.Nom} {ligne.Prenom} ({ligne.Date:dd/MM/yyyy}).";

            // Recalcul immédiat pour appliquer la correction
            Calculer();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur d'enregistrement de la correction : " + ex.Message,
                "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
