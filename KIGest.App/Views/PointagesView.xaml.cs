using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Data.Context;
using KIGest.Device.Services;

namespace KIGest.App.Views;

public partial class PointagesView : Page
{
    /// <summary>Ligne affichée dans le tableau des pointages.</summary>
    public class PointageRow
    {
        public string BadgeNumero { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Matricule { get; set; } = string.Empty;
        public DateTime DateHeure { get; set; }
        public int ModeVerification { get; set; }
        public int SensES { get; set; }
        public string PointeuseNom { get; set; } = string.Empty;
        public bool Importe { get; set; }

        /// <summary>Indicateur de correction (entrée/sortie corrigée pour ce jour-là).</summary>
        public string Corrigee => _correctionsUtilisees.Contains((BadgeNumero, DateHeure.Date)) ? "✎" : "";
    }

    private readonly KiGestDbContext _ctx;
    private readonly List<AttLog> _logs = new();
    private ZkDevice? _device;
    private Pointeuse? _pointeuseConnectee;
    private bool _operationEnCours;

    /// <summary>Couples (badge, date) pour lesquels une correction existe (filtré sur les dates affichées).</summary>
    private static readonly HashSet<(string Badge, DateTime Date)> _correctionsUtilisees = new();

    public PointagesView(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
        ChargerPointeuses();
        MettreAJourEtat();
    }

    // ==================== Bandeau de chargement (design animé) ====================

    private Storyboard? _animations;

    private void DemarrerAnimationsChargement()
    {
        ArreterAnimationsChargement();

        if (!SystemParameters.ClientAreaAnimation) return;

        var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

        // 1. Rotation du spinner
        var rotate = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1.3)))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = new Duration(TimeSpan.FromSeconds(1.3))
        };
        Storyboard.SetTarget(rotate, ArcSpinnerRotate);
        Storyboard.SetTargetProperty(rotate, new PropertyPath(RotateTransform.AngleProperty));
        storyboard.Children.Add(rotate);

        // 2. Reflet glissant sur la barre
        var reflet = new DoubleAnimation(-110, 470, new Duration(TimeSpan.FromSeconds(1.8)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = new Duration(TimeSpan.FromSeconds(1.8))
        };
        Storyboard.SetTarget(reflet, BarreProgressionRefletTranslate);
        Storyboard.SetTargetProperty(reflet, new PropertyPath(TranslateTransform.XProperty));
        storyboard.Children.Add(reflet);

        // 3. Points pulsants en cascade
        AjouterPulsation(storyboard, Dot1, Dot1Scale, 0.0);
        AjouterPulsation(storyboard, Dot2, Dot2Scale, 0.25);
        AjouterPulsation(storyboard, Dot3, Dot3Scale, 0.5);

        storyboard.Begin();
        _animations = storyboard;
    }

    private static void AjouterPulsation(Storyboard storyboard, Ellipse dot, ScaleTransform scale, double decalage)
    {
        var anim = new DoubleAnimation(0.25, 1.0, new Duration(TimeSpan.FromSeconds(0.9)))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromSeconds(decalage),
            Duration = new Duration(TimeSpan.FromSeconds(0.9))
        };
        Storyboard.SetTarget(anim, dot);
        Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(anim);

        var scaleAnim = new DoubleAnimation(0.72, 1.18, new Duration(TimeSpan.FromSeconds(0.9)))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromSeconds(decalage),
            Duration = new Duration(TimeSpan.FromSeconds(0.9))
        };
        Storyboard.SetTarget(scaleAnim, scale);
        Storyboard.SetTargetProperty(scaleAnim, new PropertyPath(ScaleTransform.ScaleXProperty));
        storyboard.Children.Add(scaleAnim);

        // Les deux axes restent synchronisés forment une belle pulsation
        var scaleY = new DoubleAnimation(0.72, 1.18, new Duration(TimeSpan.FromSeconds(0.9)))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromSeconds(decalage),
            Duration = new Duration(TimeSpan.FromSeconds(0.9))
        };
        Storyboard.SetTarget(scaleY, scale);
        Storyboard.SetTargetProperty(scaleY, new PropertyPath(ScaleTransform.ScaleYProperty));
        storyboard.Children.Add(scaleY);
    }

    private void ArreterAnimationsChargement()
    {
        _animations?.Remove(this);
        _animations = null;
    }

    private void AfficherChargement(string titre, string detail, bool indetermine = true)
    {
        TxtChargementTitre.Text = titre;
        TxtChargementDetail.Text = detail;
        TxtChargementSousTitre.Text = indetermine
            ? "La pointeuse est temporairement verrouillée — ne la débranchez pas."
            : "Enregistrement des pointages dans la base POINTAGE.";
        TxtProgression.Text = "";
        BarreProgressionSegment.Width = 0;
        BarreProgressionSegment.Opacity = 1;
        OverlayChargement.Visibility = Visibility.Visible;
        _operationEnCours = true;
        VerrouillerInterface(true);
        DemarrerAnimationsChargement();
    }

    private void MasquerChargement()
    {
        ArreterAnimationsChargement();
        OverlayChargement.Visibility = Visibility.Collapsed;
        _operationEnCours = false;
        VerrouillerInterface(false);
    }

    private void MettreAJourProgression(int pourcentage, string detail)
    {
        double pct = Math.Clamp(pourcentage, 0, 100) / 100.0;
        double largeurTotale = BarreTrack.ActualWidth > 0 ? BarreTrack.ActualWidth : 410;
        BarreProgressionSegment.Width = largeurTotale * pct;
        TxtProgression.Text = $"{pourcentage} %";
        if (!string.IsNullOrEmpty(detail)) TxtChargementDetail.Text = detail;
    }

    private void VerrouillerInterface(bool verrou)
    {
        CboPointeuse.IsEnabled = !verrou;
        BtnConnecter.IsEnabled = !verrou && _device == null;
        BtnDeconnecter.IsEnabled = !verrou && _device != null;
        BtnLire.IsEnabled = !verrou && _device != null;
        BtnImporter.IsEnabled = !verrou && _device != null && _logs.Count > 0;
        CboPointeuseFiltre.IsEnabled = !verrou;
        DtpDu.IsEnabled = !verrou;
        DtpAu.IsEnabled = !verrou;
        TxtBadgeFiltre.IsEnabled = !verrou;
        TxtEmployeFiltre.IsEnabled = !verrou;
    }

    /// <summary>Exécute une action COM dans un thread STA dédié (sans figer l'interface).</summary>
    private static Task RunStaAsync(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();

        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }

    // ==================== Données ====================

    private void ChargerPointeuses()
    {
        var liste = _ctx.Pointeuses.OrderBy(p => p.Nom).ToList();

        CboPointeuse.ItemsSource = liste;
        CboPointeuse.SelectedIndex = liste.Count > 0 ? 0 : -1;

        CboPointeuseFiltre.ItemsSource = liste;
        CboPointeuseFiltre.SelectedIndex = -1;
    }

    private Pointeuse? PointeuseCourante => CboPointeuse.SelectedItem as Pointeuse;

    private void AppliquerFiltres()
    {
        try
        {
            var query = _ctx.PointagesBruts.AsNoTracking();

            if (DtpDu.SelectedDate.HasValue)
                query = query.Where(p => p.DateHeure >= DtpDu.SelectedDate.Value.Date);

            if (DtpAu.SelectedDate.HasValue)
                query = query.Where(p => p.DateHeure < DtpAu.SelectedDate.Value.Date.AddDays(1));

            string badge = TxtBadgeFiltre.Text.Trim();
            if (!string.IsNullOrWhiteSpace(badge))
                query = query.Where(p => p.BadgeNumero.Contains(badge));

            if (CboPointeuseFiltre.SelectedItem is Pointeuse pf)
                query = query.Where(p => p.PointeuseId == pf.Id);

            string employe = TxtEmployeFiltre.Text.Trim();
            if (!string.IsNullOrWhiteSpace(employe))
            {
                var badgesFiltres = _ctx.Employes
                    .Where(e => e.Nom.Contains(employe)
                             || e.Prenom.Contains(employe)
                             || e.Matricule.Contains(employe)
                             || e.BadgeNumero.Contains(employe))
                    .Select(e => e.BadgeNumero)
                    .ToList();

                query = query.Where(p => badgesFiltres.Contains(p.BadgeNumero));
            }

            var rows = query
                .OrderByDescending(p => p.DateHeure)
                .Take(2000)
                .ToList();

            var badges = rows.Select(r => r.BadgeNumero).Distinct().ToList();
            var employes = _ctx.Employes
                .Where(e => badges.Contains(e.BadgeNumero))
                .ToDictionary(e => e.BadgeNumero);

            var idsPointeuses = rows.Select(r => r.PointeuseId).Distinct().ToList();
            var pointeuses = _ctx.Pointeuses
                .Where(p => idsPointeuses.Contains(p.Id))
                .ToDictionary(p => p.Id, p => p.Nom);

            // Badges corrigés sur la plage affichée (pour le marqueur « ✎ »)
            if (rows.Count > 0)
            {
                var debut = rows.Min(r => r.DateHeure).Date;
                var fin = rows.Max(r => r.DateHeure).Date.AddDays(1);
                var employeIds = employes.Values.Select(e => e.Id).Distinct().ToList();

                var corrections = _ctx.CorrectionsPointages
                    .AsNoTracking()
                    .Where(c => employeIds.Contains(c.EmployeId) && c.Date >= debut && c.Date < fin)
                    .ToList();

                var employeIdVersBadge = employes.ToDictionary(kv => kv.Value.Id, kv => kv.Key);

                _correctionsUtilisees.Clear();
                foreach (var correction in corrections)
                {
                    if (employeIdVersBadge.TryGetValue(correction.EmployeId, out var bdg))
                        _correctionsUtilisees.Add((bdg, correction.Date));
                }
            }
            else
            {
                _correctionsUtilisees.Clear();
            }

            var resultat = new List<PointageRow>();
            foreach (var r in rows)
            {
                employes.TryGetValue(r.BadgeNumero, out var emp);

                resultat.Add(new PointageRow
                {
                    BadgeNumero = r.BadgeNumero,
                    Nom = emp?.Nom ?? "(inconnu)",
                    Prenom = emp?.Prenom ?? "",
                    Matricule = string.IsNullOrWhiteSpace(emp?.Matricule) ? r.BadgeNumero : emp!.Matricule,
                    DateHeure = r.DateHeure,
                    ModeVerification = r.ModeVerification,
                    SensES = r.SensES,
                    PointeuseNom = pointeuses.TryGetValue(r.PointeuseId, out var pn) ? pn : "-",
                    Importe = r.Importe
                });
            }

            GrdPointages.ItemsSource = null;
            GrdPointages.ItemsSource = resultat;
            TxtTotal.Text = $"{resultat.Count} pointage(s) affiché(s) — total en base : {_ctx.PointagesBruts.Count()}";
        }
        catch (Exception ex)
        {
            TxtImportStatus.Text = "Erreur de filtre : " + ex.Message;
        }
    }

    private void BtnFiltrer_Click(object sender, RoutedEventArgs e) => AppliquerFiltres();

    private void BtnReinitialiser_Click(object sender, RoutedEventArgs e)
    {
        DtpDu.SelectedDate = null;
        DtpAu.SelectedDate = null;
        TxtBadgeFiltre.Text = "";
        TxtEmployeFiltre.Text = "";
        CboPointeuseFiltre.SelectedIndex = -1;
        AppliquerFiltres();
    }

    private void DtpDu_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => AppliquerFiltres();
    private void DtpAu_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => AppliquerFiltres();

    private void TxtBadgeFiltre_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { AppliquerFiltres(); e.Handled = true; }
    }

    private void TxtEmployeFiltre_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { AppliquerFiltres(); e.Handled = true; }
    }

    private void BtnActualiser_Click(object sender, RoutedEventArgs e) => AppliquerFiltres();

    // ==================== Connexion ====================

    private void MettreAJourEtat()
    {
        bool connecte = _device != null && _pointeuseConnectee != null;

        TxtConnexion.Text = connecte
            ? $"Connecté à {_pointeuseConnectee!.Nom} ({_pointeuseConnectee.AdresseIP}:{_pointeuseConnectee.Port})"
            : "Déconnecté";
        TxtConnexion.Foreground = (SolidColorBrush)FindResource(connecte ? "Brush.Status.Present" : "Brush.MutedForeground");

        if (_operationEnCours) return;

        BtnConnecter.IsEnabled = !connecte;
        BtnDeconnecter.IsEnabled = connecte;
        BtnLire.IsEnabled = connecte;
        BtnImporter.IsEnabled = connecte && _logs.Count > 0;
    }

    private void CboPointeuse_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_device != null)
        {
            MessageBox.Show("Déconnectez la pointeuse courante avant d'en sélectionner une autre.",
                "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _logs.Clear();
        BtnImporter.IsEnabled = false;
        MettreAJourEtat();
    }

    private void BtnConnecter_Click(object sender, RoutedEventArgs e)
    {
        var p = PointeuseCourante;
        if (p == null)
        {
            TxtImportStatus.Text = "Sélectionnez d'abord une pointeuse.";
            return;
        }

        try
        {
            BtnConnecter.IsEnabled = false;
            TxtImportStatus.Text = $"Connexion à {p.Nom} ({p.AdresseIP}:{p.Port})...";

            _device?.Dispose();
            _device = new ZkDevice();

            if (!_device.Connect(p.AdresseIP, p.Port, p.CleCom))
            {
                TxtImportStatus.Text = $"Échec de connexion à {p.AdresseIP}:{p.Port}. Vérifiez l'adresse, le port et la clé de communication.";
                _device.Dispose();
                _device = null;
                MettreAJourEtat();
                return;
            }

            _pointeuseConnectee = p;
            TxtImportStatus.Text = $"Connexion établie avec {p.Nom}.";
            MettreAJourEtat();
        }
        catch (Exception ex)
        {
            TxtImportStatus.Text = "Erreur de connexion : " + ex.Message;
            _device?.Dispose();
            _device = null;
            _pointeuseConnectee = null;
            MettreAJourEtat();
        }
    }

    private void BtnDeconnecter_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _device?.Disconnect();
            _device?.Dispose();
        }
        catch { }
        finally
        {
            _device = null;
            _pointeuseConnectee = null;
            _logs.Clear();
            TxtImportStatus.Text = "Déconnecté.";
            MettreAJourEtat();
        }
    }

    // ==================== Lecture avec bandeau ====================

    private async void BtnLire_Click(object sender, RoutedEventArgs e)
    {
        var p = _pointeuseConnectee;
        if (_device == null || p == null)
        {
            TxtImportStatus.Text = "Connectez-vous d'abord à une pointeuse.";
            return;
        }

        AfficherChargement("Lecture des pointages en cours...",
            $"Connexion à {p.Nom} ({p.AdresseIP}:{p.Port}) — la pointeuse est bloquée pendant la lecture.");

        var sw = Stopwatch.StartNew();
        List<AttLog> logs = new();
        string? erreur = null;

        try
        {
            await RunStaAsync(() =>
            {
                try
                {
                    using var device = new ZkDevice();
                    if (!device.Connect(p.AdresseIP, p.Port, p.CleCom))
                    {
                        erreur = $"Échec de connexion à {p.AdresseIP}:{p.Port}.";
                        return;
                    }

                    logs = device.ReadAttendance(p.NumeroMachine).ToList();
                }
                catch (Exception ex)
                {
                    erreur = ex.Message;
                }
            });
        }
        catch (Exception ex)
        {
            erreur = ex.Message;
        }
        finally
        {
            MasquerChargement();
        }

        if (erreur != null)
        {
            TxtImportStatus.Text = "Erreur de lecture : " + erreur;
            return;
        }

        _logs.Clear();
        _logs.AddRange(logs);

        TxtImportStatus.Text = $"{_logs.Count} pointage(s) lu(s) en {sw.Elapsed.TotalSeconds:F1} s. Vous pouvez maintenant importer.";
        MettreAJourEtat();
    }

    // ==================== Import avec bandeau + progression ====================

    private async void BtnImporter_Click(object sender, RoutedEventArgs e)
    {
        var p = _pointeuseConnectee;
        if (_device == null || p == null || _logs.Count == 0)
        {
            TxtImportStatus.Text = "Connectez-vous et lisez les pointages avant d'importer.";
            return;
        }

        AfficherChargement("Importation des pointages en cours...",
            $"{_logs.Count} pointage(s) à traiter vers la base POINTAGE.", indetermine: false);

        var sw = Stopwatch.StartNew();
        int ajoutes = 0;
        int doublons = 0;
        string? erreur = null;

        try
        {
            await Task.Run(() =>
            {
                // Index unique (PointeuseId, BadgeNumero, DateHeure) : recherche rapide des doublons
                var dateMin = _logs.Min(l => l.Time).Date.AddDays(-1);
                var dateMax = _logs.Max(l => l.Time).Date.AddDays(1);

                var existants = new HashSet<(string Badge, DateTime Date)>(
                    _ctx.PointagesBruts
                        .AsNoTracking()
                        .Where(x => x.PointeuseId == p.Id && x.DateHeure >= dateMin && x.DateHeure <= dateMax)
                        .Select(x => new { x.BadgeNumero, x.DateHeure })
                        .ToList()
                        .Select(x => (x.BadgeNumero, x.DateHeure)));

                var lot = new List<PointageBrut>(500);
                int traites = 0;

                foreach (var log in _logs)
                {
                    traites++;

                    if (existants.Contains((log.UserId, log.Time)))
                    {
                        doublons++;
                    }
                    else
                    {
                        lot.Add(new PointageBrut
                        {
                            PointeuseId = p.Id,
                            BadgeNumero = log.UserId,
                            DateHeure = log.Time,
                            ModeVerification = log.VerifyMode,
                            SensES = log.InOutMode,
                            Importe = true
                        });
                        ajoutes++;
                        existants.Add((log.UserId, log.Time));
                    }

                    if (lot.Count >= 500)
                    {
                        _ctx.PointagesBruts.AddRange(lot);
                        _ctx.SaveChanges();
                        lot.Clear();
                    }

                    if (traites % 500 == 0)
                    {
                        int pct = (int)(traites * 100L / _logs.Count);
                        Dispatcher.Invoke(() => MettreAJourProgression(pct, $"{traites} / {_logs.Count} pointage(s) traités"));
                    }
                }

                if (lot.Count > 0)
                {
                    _ctx.PointagesBruts.AddRange(lot);
                    _ctx.SaveChanges();
                }

                p.DerniereSynchro = DateTime.Now;
                _ctx.SaveChanges();
            });
        }
        catch (Exception ex)
        {
            erreur = ex.Message;
        }
        finally
        {
            MasquerChargement();
        }

        if (erreur != null)
        {
            TxtImportStatus.Text = "Erreur d'import : " + erreur;
            return;
        }

        TxtImportStatus.Text = $"{ajoutes} pointage(s) importé(s), {doublons} doublon(s) ignoré(s) — {sw.Elapsed.TotalSeconds:F1} s.";
        AppliquerFiltres();
        MettreAJourEtat();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _device?.Disconnect();
            _device?.Dispose();
        }
        catch { }
        _device = null;
        _pointeuseConnectee = null;
    }
}
