using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Data.Context;
using KIGest.Data.Services;
using KIGest.Device.Services;
using KIGest.App.Services;

namespace KIGest.App.Views;

/// <summary>Ligne affichée dans « Derniers pointages ».</summary>
public class DernierPointageRow
{
    public DateTime DateHeure { get; set; }
    public string Matricule { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string ModeAffiche { get; set; } = string.Empty;

    /// <summary>« Entrée » ou « Sortie » (déduit par appariement chronologique puis, à défaut, du sens de la pointeuse).</summary>
    public string SensAffiche { get; set; } = "Entrée";

    /// <summary>Vrai si le pointage est une sortie.</summary>
    public bool EstSortie => SensAffiche == "Sortie";

    /// <summary>« OUI (−XXX min) » si sortie avant l'heure de fin de l'horaire du jour, sinon « — ».</summary>
    public string PartiTotAffichee { get; set; } = "—";
}

public partial class DashboardView : Page
{
    private readonly KiGestDbContext _ctx;
    private readonly ParametresService _parametres;

    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _autoSyncTimer;
    private bool _syncEnCours;
    private DateTime _derniereSync = DateTime.MinValue;

    // Converter local pour le mode de vérification
    private readonly CodeZKTecoConverter _codeConverter = new();

    public DashboardView(KiGestDbContext ctx, ParametresService parametres)
    {
        _ctx = ctx;
        _parametres = parametres;
        InitializeComponent();
        Charger();

        // Rafraîchissement visuel régulier depuis la base (10 s)
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timer.Tick += (_, _) => RafraichirDerniersPointages();
        _timer.Start();

        // Synchronisation automatique EN TEMPS RÉEL :
        // lecture incrémentale des nouveaux journaux toutes les 3 secondes
        // (dès qu'un utilisateur badge, le pointage apparaît sur le tableau de bord).
        _autoSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _autoSyncTimer.Tick += async (_, _) => await SynchroniserAsync(depuisTimer: true);

        // La case est cochée par défaut dans le XAML : on démarre tout de suite.
        if (ChkAutoSync.IsChecked == true)
            _autoSyncTimer.Start();

        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _autoSyncTimer.Stop();
        };
    }

    private void Charger()
    {
        try
        {
            DateTime today = DateTime.Today;

            KpiPointages.Text = _ctx.PointagesBruts.Count(p => p.DateHeure >= today && p.DateHeure < today.AddDays(1)).ToString();
            KpiEmployes.Text = _ctx.Employes.Count(e => e.Actif).ToString();
            KpiPointeuses.Text = _ctx.Pointeuses.Count(p => p.Actif).ToString();
            KpiParametres.Text = _ctx.Parametres.Count().ToString();

            GrdPointeuses.ItemsSource = null;
            GrdPointeuses.ItemsSource = _ctx.Pointeuses
                .AsNoTracking()
                .OrderBy(p => p.Nom)
                .ToList();

            RafraichirDerniersPointages();
        }
        catch (Exception)
        {
            // Le tableau de bord ne doit pas bloquer l'application si la base est inaccessible
        }
    }

    private void RafraichirDerniersPointages()
    {
        try
        {
            var rows = _ctx.PointagesBruts
                .AsNoTracking()
                .OrderByDescending(p => p.DateHeure)
                .Take(15)
                .ToList();
            if (rows.Count == 0)
            {
                GrdPointages.ItemsSource = null;
                VoyantSync.Fill = (SolidColorBrush)FindResource("Brush.Status.Rest");
                TxtSync.Text = "Aucun pointage en base pour l'instant.";
                return;
            }

            // Appariement entrée/sortie (règle 4) : on charge un peu d'historique
            // pour déterminer le sens de chaque pointage affiché.
            var debutAppariement = rows.Min(r => r.DateHeure).Date.AddDays(-1);
            var badgesAll = rows.Select(r => r.BadgeNumero).Distinct().ToList();

            var historique = _ctx.PointagesBruts
                .AsNoTracking()
                .Where(p => badgesAll.Contains(p.BadgeNumero) && p.DateHeure >= debutAppariement)
                .OrderBy(p => p.BadgeNumero)
                .ThenBy(p => p.DateHeure)
                .ToList();

            var sensParPointage = new Dictionary<int, string>();
            foreach (var groupe in historique
                         .GroupBy(p => (p.BadgeNumero, p.DateHeure.Date)))
            {
                int index = 0;
                foreach (var pointage in groupe.OrderBy(p => p.DateHeure))
                {
                    // 0 = entrée, 1 = sortie, 2 = entrée (pause), 3 = sortie (pause)…
                    sensParPointage[pointage.Id] = index % 2 == 0 ? "Entrée" : "Sortie";
                    index++;
                }
            }

            var badges = rows.Select(r => r.BadgeNumero).Distinct().ToList();
            var employes = _ctx.Employes
                .AsNoTracking()
                .Where(e => badges.Contains(e.BadgeNumero))
                .ToDictionary(e => e.BadgeNumero);

            // Règle : sorti avant l'heure de fin de l'horaire du jour → « OUI »
            var debutPeriode = rows.Min(r => r.DateHeure).Date;
            var finPeriode = rows.Max(r => r.DateHeure).Date.AddDays(1);
            var horaires = _ctx.Horaires.AsNoTracking().ToList();
            var affectations = _ctx.AffectationsHoraires.AsNoTracking()
                .Where(a => a.DateDebut < finPeriode && (a.DateFin == null || a.DateFin >= debutPeriode))
                .ToList();
            var affectationsService = _ctx.AffectationsServiceHoraires.AsNoTracking()
                .Where(a => a.DateDebut < finPeriode && (a.DateFin == null || a.DateFin >= debutPeriode))
                .ToList();
            string horaireParDefautLibelle = _parametres.Get("Pointage.HoraireParDefaut", "");
            var horaireParDefaut = !string.IsNullOrWhiteSpace(horaireParDefautLibelle)
                ? horaires.FirstOrDefault(h => h.Libelle == horaireParDefautLibelle)
                : null;

            Core.Entities.Horaire? HoraireDuJour(Core.Entities.Employe employe, DateTime date)
            {
                var aff = affectations.FirstOrDefault(a => a.EmployeId == employe.Id && a.DateDebut <= date && (a.DateFin == null || a.DateFin >= date));
                if (aff != null) return horaires.FirstOrDefault(h => h.Id == aff.HoraireId);
                if (employe.ServiceId.HasValue)
                {
                    var affSrv = affectationsService
                        .Where(a => a.ServiceId == employe.ServiceId.Value && a.DateDebut <= date && (a.DateFin == null || a.DateFin >= date))
                        .OrderByDescending(a => a.DateDebut)
                        .FirstOrDefault();
                    if (affSrv != null) return horaires.FirstOrDefault(h => h.Id == affSrv.HoraireId);
                }
                return horaireParDefaut;
            }

            var liste = rows.Select(r =>
            {
                employes.TryGetValue(r.BadgeNumero, out var emp);

                bool sensConnu = sensParPointage.TryGetValue(r.Id, out var sens);
                string sensAffiche = sensConnu
                    ? sens
                    : (r.SensES == 1 ? "Sortie" : "Entrée");

                string partiTotText = "—";
                if (sensAffiche == "Sortie" && emp != null)
                {
                    var horaire = HoraireDuJour(emp, r.DateHeure.Date);
                    if (horaire != null)
                    {
                        double ecart = (r.DateHeure.Date.Add(horaire.HeureFin) - r.DateHeure).TotalMinutes;
                        if (ecart > 0)
                        {
                            int totalMinutes = (int)Math.Round(ecart);
                            int h = totalMinutes / 60, m = totalMinutes % 60;
                            partiTotText = h > 0 ? $"OUI (−{h} h {m:00} min)" : $"OUI (−{m} min)";
                        }
                    }
                }

                return new DernierPointageRow
                {
                    DateHeure = r.DateHeure,
                    Matricule = string.IsNullOrWhiteSpace(emp?.Matricule) ? r.BadgeNumero : emp!.Matricule,
                    Nom = string.IsNullOrWhiteSpace(emp?.Nom) ? emp?.BadgeNumero ?? "(inconnu)" : emp!.Nom,
                    Prenom = emp?.Prenom ?? "",
                    ModeAffiche = _codeConverter.Convert(r.ModeVerification, typeof(string), "Mode", System.Globalization.CultureInfo.InvariantCulture)?.ToString() ?? $"Mode {r.ModeVerification}",
                    SensAffiche = sensAffiche,
                    PartiTotAffichee = partiTotText
                };
            }).ToList();

            GrdPointages.ItemsSource = null;
            GrdPointages.ItemsSource = liste;

            VoyantSync.Fill = (SolidColorBrush)FindResource("Brush.Status.Present");
            TxtSync.Text = _derniereSync == DateTime.MinValue
                ? "Dernière synchronisation : jamais"
                : $"Dernière synchronisation : {_derniereSync:dd/MM/yyyy HH:mm:ss}";
        }
        catch (Exception ex)
        {
            TxtSync.Text = "Erreur de rafraîchissement : " + ex.Message;
        }
        finally
        {
            ChargerKpis();
        }
    }

    // ==================== Synchronisation ====================

    private void ChkAutoSync_Changed(object sender, RoutedEventArgs e)
    {
        // Déclenché pendant InitializeComponent (IsChecked="True" en XAML) :
        // les objets ne sont pas tous prêts — le ctor démarre le timer explicitement.
        if (_autoSyncTimer == null || TxtSync == null) return;
        bool actif = ChkAutoSync.IsChecked == true;

        if (actif)
        {
            _autoSyncTimer.Start();
            TxtSync.Text = "Synchronisation automatique activée (temps réel : lecture toutes les 3 secondes).";
        }
        else
        {
            _autoSyncTimer.Stop();
            TxtSync.Text = "Synchronisation automatique désactivée. Rafraîchissement visuel (10 s) toujours actif.";
        }
    }

    private async void BtnSyncMaintenant_Click(object sender, RoutedEventArgs e)
    {
        await SynchroniserAsync(depuisTimer: false);
    }

    private async Task SynchroniserAsync(bool depuisTimer)
    {
        if (_syncEnCours) return;

        try
        {
            _syncEnCours = true;
            if (depuisTimer)
            {
                // Temps réel : on ne bloque pas l'UI ni la case, pas de voyant orange
            }
            else
            {
                VoyantSync.Fill = (SolidColorBrush)FindResource("Brush.Status.Late");
                BtnSyncMaintenant.IsEnabled = false;
                TxtSync.Text = "Connexion à la pointeuse...";
            }

            var pointeuses = _ctx.Pointeuses.AsNoTracking().Where(p => p.Actif).ToList();
            if (pointeuses.Count == 0)
            {
                TxtSync.Text = "Aucune pointeuse active configurée.";
                return;
            }

            int totalAjouts = 0;

            foreach (var pointeuse in pointeuses)
            {
                try
                {
                    if (!depuisTimer)
                        TxtSync.Text = $"Lecture sur {pointeuse.Nom}...";

                    var logs = new List<AttLog>();
                    string? erreur = null;

                    await Task.Run(() =>
                    {
                        try
                        {
                            using var device = new ZkDevice();
                            if (!device.Connect(pointeuse.AdresseIP, pointeuse.Port, pointeuse.CleCom))
                            {
                                erreur = $"Échec de connexion à {pointeuse.Nom}";
                                return;
                            }
                            // Mode temps réel : lecture incrémentale uniquement (très rapide).
                            // Bouton « Synchroniser maintenant » : lecture complète.
                            logs = depuisTimer
                                ? device.ReadNewLogs(pointeuse.NumeroMachine)
                                : device.ReadAttendance(pointeuse.NumeroMachine).ToList();
                        }
                        catch (Exception ex)
                        {
                            erreur = ex.Message;
                        }
                    });

                    if (erreur != null)
                    {
                        if (!depuisTimer) TxtSync.Text = erreur;
                        continue;
                    }

                    if (logs.Count == 0)
                    {
                        if (!depuisTimer) TxtSync.Text = $"{pointeuse.Nom} : aucun pointage lu.";
                        continue;
                    }

                    // Déduplication contre la base
                    var dateMin = logs.Min(l => l.Time).Date.AddDays(-1);
                    var dateMax = logs.Max(l => l.Time).Date.AddDays(1);

                    var existants = new HashSet<(string Badge, DateTime Date)>(
                        _ctx.PointagesBruts
                            .Where(x => x.PointeuseId == pointeuse.Id && x.DateHeure >= dateMin && x.DateHeure <= dateMax)
                            .Select(x => new { x.BadgeNumero, x.DateHeure })
                            .ToList()
                            .Select(x => (x.BadgeNumero, x.DateHeure)));

                    int ajoutes = 0;
                    foreach (var log in logs)
                    {
                        if (existants.Contains((log.UserId, log.Time))) continue;

                        _ctx.PointagesBruts.Add(new PointageBrut
                        {
                            PointeuseId = pointeuse.Id,
                            BadgeNumero = log.UserId,
                            DateHeure = log.Time,
                            ModeVerification = log.VerifyMode,
                            SensES = log.InOutMode,
                            Importe = true
                        });
                        ajoutes++;
                    }

                    pointeuse.DerniereSynchro = DateTime.Now;
                    _ctx.Pointeuses.Update(pointeuse);
                    _ctx.SaveChanges();
                    totalAjouts += ajoutes;
                }
                catch (Exception ex)
                {
                    TxtSync.Text = $"Erreur sur {pointeuse.Nom} : {ex.Message}";
                }
            }

            if (totalAjouts > 0 || !depuisTimer)
            {
                _derniereSync = DateTime.Now;
                RafraichirDerniersPointages(); // recharge grille + KPI + libellé « Dernière synchronisation »

                TxtSync.Text = totalAjouts > 0
                    ? $"{totalAjouts} nouveau(x) pointage(s) reçu(s) — {DateTime.Now:HH:mm:ss}"
                    : $"Aucun nouveau pointage — {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            TxtSync.Text = "Erreur de synchronisation : " + ex.Message;
        }
        finally
        {
            _syncEnCours = false;
            BtnSyncMaintenant.IsEnabled = true;
            VoyantSync.Fill = (SolidColorBrush)FindResource("Brush.Status.Present");
        }
    }

    private void ChargerKpis()
    {
        try
        {
            DateTime today = DateTime.Today;
            KpiPointages.Text = _ctx.PointagesBruts.Count(p => p.DateHeure >= today && p.DateHeure < today.AddDays(1)).ToString();
        }
        catch { }
    }
}
