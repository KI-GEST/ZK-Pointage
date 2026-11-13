using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Data.Context;

namespace KIGest.App.Views;

public partial class HorairesView : Page
{
    /// <summary>Ligne unifiée affichée dans la grille des affectations.</summary>
    public class AffectationRow
    {
        public string TypeAffiche { get; set; } = string.Empty;   // Service / Employé
        public string CibleAffiche { get; set; } = string.Empty;
        public string HoraireLibelle { get; set; } = string.Empty;
        public DateTime DateDebut { get; set; }
        public string DateFinAffiche { get; set; } = string.Empty;
        public int Id { get; set; }
        public bool EstService { get; set; }
    }

    public class EmployeItem
    {
        public int Id { get; set; }
        public string Matricule { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Affichage => $"{Matricule} — {Prenom} {Nom}".Trim();
    }

    private readonly KiGestDbContext _ctx;
    private Horaire? _horaireCourant;
    private AffectationRow? _affectationCourante;

    public HorairesView(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        CboType.ItemsSource = new[] { "Toutes", "Service", "Employé" };
        CboType.SelectedIndex = 0;

        ChargerHoraires();
        ChargerFiltresServices();
        ChargerFiltresEmployes();
        ChargerAffectations();
    }

    // ==================== Horaires ====================

    private void ChargerHoraires()
    {
        MajListeHoraires(_ctx.Horaires.AsNoTracking().OrderBy(h => h.Libelle).ToList());
    }

    private void MajListeHoraires(List<Horaire> horaires)
    {
        GrdHoraires.ItemsSource = null;
        GrdHoraires.ItemsSource = horaires.Select(h => new HoraireRow
        {
            Id = h.Id,
            Libelle = h.Libelle,
            HeureDebut = h.HeureDebut,
            HeureFin = h.HeureFin,
            PauseAffiche = h.PauseDebut.HasValue && h.PauseFin.HasValue
                ? $"{h.PauseDebut:hh\\:mm} - {h.PauseFin:hh\\:mm}"
                : "-",
            ToleranceRetardMin = h.ToleranceRetardMin,
            NuitOuiNon = h.NuitOuiNon,
            PauseDebut = h.PauseDebut,
            PauseFin = h.PauseFin
        }).ToList();
    }

    public class HoraireRow
    {
        public int Id { get; set; }
        public string Libelle { get; set; } = string.Empty;
        public TimeSpan HeureDebut { get; set; }
        public TimeSpan HeureFin { get; set; }
        public string PauseAffiche { get; set; } = string.Empty;
        public int ToleranceRetardMin { get; set; }
        public bool NuitOuiNon { get; set; }
        public TimeSpan? PauseDebut { get; set; }
        public TimeSpan? PauseFin { get; set; }
    }

    private void GrdHoraires_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GrdHoraires.SelectedItem is not HoraireRow row)
        {
            _horaireCourant = null;
            BtnHorairesSupprimer.IsEnabled = false;
            BtnHorairesEnregistrer.Content = "Ajouter l'horaire";
            return;
        }

        _horaireCourant = _ctx.Horaires.FirstOrDefault(h => h.Id == row.Id);

        if (_horaireCourant != null)
        {
            TxtLibelle.Text = _horaireCourant.Libelle;
            TxtHeureDebut.Text = _horaireCourant.HeureDebut.ToString(@"hh\:mm");
            TxtHeureFin.Text = _horaireCourant.HeureFin.ToString(@"hh\:mm");
            TxtPauseDebut.Text = _horaireCourant.PauseDebut?.ToString(@"hh\:mm") ?? "";
            TxtPauseFin.Text = _horaireCourant.PauseFin?.ToString(@"hh\:mm") ?? "";
            TxtTolerance.Text = _horaireCourant.ToleranceRetardMin.ToString();
            ChkNuit.IsChecked = _horaireCourant.NuitOuiNon;
            BtnHorairesSupprimer.IsEnabled = true;
            BtnHorairesEnregistrer.Content = "Modifier l'horaire";
        }
    }

    private void BtnHorairesNouveau_Click(object sender, RoutedEventArgs e)
    {
        _horaireCourant = null;
        BtnHorairesEnregistrer.Content = "Ajouter l'horaire";
        TxtLibelle.Text = "";
        TxtHeureDebut.Text = "08:00";
        TxtHeureFin.Text = "17:00";
        TxtPauseDebut.Text = "12:00";
        TxtPauseFin.Text = "13:00";
        TxtTolerance.Text = "10";
        ChkNuit.IsChecked = false;
        BtnHorairesSupprimer.IsEnabled = false;
        TxtLibelle.Focus();
    }

    private void BtnHorairesEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtLibelle.Text))
        {
            MessageBox.Show("Le libellé est obligatoire.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TimeSpan.TryParse(TxtHeureDebut.Text.Trim(), out var debut))
        {
            MessageBox.Show("Heure début invalide (format HH:mm).", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TimeSpan.TryParse(TxtHeureFin.Text.Trim(), out var fin))
        {
            MessageBox.Show("Heure fin invalide (format HH:mm).", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TimeSpan? pauseDebut = null, pauseFin = null;
        if (!string.IsNullOrWhiteSpace(TxtPauseDebut.Text))
        {
            if (!TimeSpan.TryParse(TxtPauseDebut.Text.Trim(), out var pd))
            {
                MessageBox.Show("Pause début invalide (format HH:mm).", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            pauseDebut = pd;
        }
        if (!string.IsNullOrWhiteSpace(TxtPauseFin.Text))
        {
            if (!TimeSpan.TryParse(TxtPauseFin.Text.Trim(), out var pf))
            {
                MessageBox.Show("Pause fin invalide (format HH:mm).", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            pauseFin = pf;
        }

        int.TryParse(TxtTolerance.Text.Trim(), out int tolerance);

        var etaitModif = _horaireCourant != null;
        var horaire = _horaireCourant ?? new Horaire();

        horaire.Libelle = TxtLibelle.Text.Trim();
        horaire.HeureDebut = debut;
        horaire.HeureFin = fin;
        horaire.PauseDebut = pauseDebut;
        horaire.PauseFin = pauseFin;
        horaire.ToleranceRetardMin = tolerance;
        horaire.NuitOuiNon = ChkNuit.IsChecked == true;

        if (!etaitModif)
            _ctx.Horaires.Add(horaire);

        _ctx.SaveChanges();
        TxtStatut.Text = etaitModif
            ? $"Horaire « {horaire.Libelle} » modifié."
            : $"Horaire « {horaire.Libelle} » ajouté.";

        ChargerHoraires();
        ChargerFiltresEmployes();
    }

    private void BtnHorairesSupprimer_Click(object sender, RoutedEventArgs e)
    {
        if (_horaireCourant == null) return;

        if (MessageBox.Show($"Supprimer l'horaire « {_horaireCourant.Libelle} » ?\nLes affectations associées seront aussi supprimées.",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var id = _horaireCourant.Id;

        _ctx.AffectationsHoraires.RemoveRange(
            _ctx.AffectationsHoraires.Where(a => a.HoraireId == id));
        _ctx.AffectationsServiceHoraires.RemoveRange(
            _ctx.AffectationsServiceHoraires.Where(a => a.HoraireId == id));
        _ctx.Horaires.Remove(_horaireCourant);

        _ctx.SaveChanges();
        _horaireCourant = null;
        BtnHorairesSupprimer.IsEnabled = false;
        ChargerHoraires();
        ChargerAffectations();
        TxtStatut.Text = "Horaire supprimé.";
    }

    // ==================== Filtres ====================

    private void ChargerFiltresServices()
    {
        CboService.ItemsSource = _ctx.Services.AsNoTracking().OrderBy(s => s.Libelle).ToList();
        CboService.DisplayMemberPath = "Libelle";
        CboService.SelectedIndex = -1;
    }

    private void ChargerFiltresEmployes()
    {
        CboEmploye.ItemsSource = _ctx.Employes
            .AsNoTracking()
            .OrderBy(e => e.Nom)
            .Select(e => new EmployeItem { Id = e.Id, Matricule = e.Matricule, Nom = e.Nom, Prenom = e.Prenom })
            .ToList();
        CboEmploye.DisplayMemberPath = "Affichage";
        CboEmploye.SelectedIndex = -1;
    }

    private void CboType_SelectionChanged(object sender, SelectionChangedEventArgs e) => ChargerAffectations();
    private void CboService_SelectionChanged(object sender, SelectionChangedEventArgs e) => ChargerAffectations();
    private void CboEmploye_SelectionChanged(object sender, SelectionChangedEventArgs e) => ChargerAffectations();

    // ==================== Affectations ====================

    private void ChargerAffectations()
    {
        var rows = new List<AffectationRow>();

        string type = CboType.SelectedItem?.ToString() ?? "Toutes";

        if (type != "Employé")
        {
            var services = _ctx.Services.AsNoTracking().ToList();
            var horaires = _ctx.Horaires.AsNoTracking().ToList();
            var serviceFiltre = CboService.SelectedItem as Service;
            var affectationsService = _ctx.AffectationsServiceHoraires
                .AsNoTracking()
                .Where(a => serviceFiltre == null || a.ServiceId == serviceFiltre.Id)
                .ToList();

            foreach (var a in affectationsService)
            {
                var service = services.FirstOrDefault(s => s.Id == a.ServiceId);
                var horaire = horaires.FirstOrDefault(h => h.Id == a.HoraireId);
                rows.Add(new AffectationRow
                {
                    TypeAffiche = "Service",
                    CibleAffiche = service?.Libelle ?? "?",
                    HoraireLibelle = horaire?.Libelle ?? "?",
                    DateDebut = a.DateDebut,
                    DateFinAffiche = a.DateFin?.ToString("dd/MM/yyyy") ?? "En cours",
                    Id = a.Id,
                    EstService = true
                });
            }
        }

        if (type != "Service")
        {
            var employes = _ctx.Employes.AsNoTracking().ToList();
            var horaires = _ctx.Horaires.AsNoTracking().ToList();
            var employeFiltre = CboEmploye.SelectedItem as EmployeItem;
            var affectationsEmploye = _ctx.AffectationsHoraires
                .AsNoTracking()
                .Where(a => employeFiltre == null || a.EmployeId == employeFiltre.Id)
                .ToList();

            foreach (var a in affectationsEmploye)
            {
                var employe = employes.FirstOrDefault(e => e.Id == a.EmployeId);
                var horaire = horaires.FirstOrDefault(h => h.Id == a.HoraireId);
                rows.Add(new AffectationRow
                {
                    TypeAffiche = "Employé",
                    CibleAffiche = employe != null ? $"{employe.Matricule} — {employe.Prenom} {employe.Nom}" : "?",
                    HoraireLibelle = horaire?.Libelle ?? "?",
                    DateDebut = a.DateDebut,
                    DateFinAffiche = a.DateFin?.ToString("dd/MM/yyyy") ?? "En cours",
                    Id = a.Id,
                    EstService = false
                });
            }
        }

        GrdAffectations.ItemsSource = null;
        GrdAffectations.ItemsSource = rows
            .OrderBy(r => r.CibleAffiche)
            .ThenByDescending(r => r.DateDebut)
            .ToList();
    }

    private void GrdAffectations_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _affectationCourante = GrdAffectations.SelectedItem as AffectationRow;
        BtnAffectationSupprimer.IsEnabled = _affectationCourante != null;
        BtnAffectationModifier.IsEnabled = _affectationCourante != null;
    }

    private void BtnAffectationModifier_Click(object sender, RoutedEventArgs e)
    {
        if (_affectationCourante == null) return;

        var horaires = _ctx.Horaires.AsNoTracking().ToList();

        int? horaireActuelId = null;
        DateTime dateDebut = DateTime.Today;
        DateTime? dateFin = null;

        if (_affectationCourante.EstService)
        {
            var a = _ctx.AffectationsServiceHoraires.FirstOrDefault(x => x.Id == _affectationCourante.Id);
            if (a != null)
            {
                horaireActuelId = a.HoraireId;
                dateDebut = a.DateDebut;
                dateFin = a.DateFin;
            }
        }
        else
        {
            var a = _ctx.AffectationsHoraires.FirstOrDefault(x => x.Id == _affectationCourante.Id);
            if (a != null)
            {
                horaireActuelId = a.HoraireId;
                dateDebut = a.DateDebut;
                dateFin = a.DateFin;
            }
        }

        var dialog = new AffectationHoraireDialog(_affectationCourante.CibleAffiche, horaires,
            horaireActuelId: horaireActuelId, dateDebut: dateDebut, dateFin: dateFin)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true) return;

        if (dialog.HoraireId is null || dialog.DateDebut is null)
        {
            MessageBox.Show("Horaire et date de début sont obligatoires.", "KI-GEST Pointage",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_affectationCourante.EstService)
        {
            var a = _ctx.AffectationsServiceHoraires.FirstOrDefault(x => x.Id == _affectationCourante.Id);
            if (a != null)
            {
                a.HoraireId = dialog.HoraireId.Value;
                a.DateDebut = dialog.DateDebut.Value;
                a.DateFin = dialog.DateFin;
            }
        }
        else
        {
            var a = _ctx.AffectationsHoraires.FirstOrDefault(x => x.Id == _affectationCourante.Id);
            if (a != null)
            {
                a.HoraireId = dialog.HoraireId.Value;
                a.DateDebut = dialog.DateDebut.Value;
                a.DateFin = dialog.DateFin;
            }
        }

        _ctx.SaveChanges();
        _affectationCourante = null;
        BtnAffectationModifier.IsEnabled = false;
        BtnAffectationSupprimer.IsEnabled = false;
        ChargerAffectations();
    }

    private void BtnAffectationSupprimer_Click(object sender, RoutedEventArgs e)
    {
        if (_affectationCourante == null) return;

        if (MessageBox.Show("Supprimer cette affectation ?", "KI-GEST Pointage",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (_affectationCourante.EstService)
        {
            var a = _ctx.AffectationsServiceHoraires.FirstOrDefault(x => x.Id == _affectationCourante.Id);
            if (a != null) _ctx.AffectationsServiceHoraires.Remove(a);
        }
        else
        {
            var a = _ctx.AffectationsHoraires.FirstOrDefault(x => x.Id == _affectationCourante.Id);
            if (a != null) _ctx.AffectationsHoraires.Remove(a);
        }

        _ctx.SaveChanges();
        _affectationCourante = null;
        BtnAffectationSupprimer.IsEnabled = false;
        ChargerAffectations();
    }

    private void BtnAjouterService_Click(object sender, RoutedEventArgs e)
    {
        if (CboService.SelectedItem is not Service service)
        {
            MessageBox.Show("Sélectionnez d'abord un service dans le filtre.", "KI-GEST Pointage",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AjouterAffectation(service.Id, service.Libelle, true);
    }

    private void BtnAjouterEmploye_Click(object sender, RoutedEventArgs e)
    {
        if (CboEmploye.SelectedItem is not EmployeItem employe)
        {
            MessageBox.Show("Sélectionnez d'abord un employé dans le filtre.", "KI-GEST Pointage",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AjouterAffectation(employe.Id, $"{employe.Matricule} — {employe.Prenom} {employe.Nom}", false);
    }

    private void AjouterAffectation(int cibleId, string cibleNom, bool parService)
    {
        if (_ctx.Horaires.Count() == 0)
        {
            MessageBox.Show("Créez d'abord un horaire dans l'onglet « Horaires ».", "KI-GEST Pointage",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new AffectationHoraireDialog(cibleNom, _ctx.Horaires.AsNoTracking().ToList())
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true) return;

        if (dialog.HoraireId is null || dialog.DateDebut is null)
        {
            MessageBox.Show("Horaire et date de début sont obligatoires.", "KI-GEST Pointage",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (parService)
        {
            _ctx.AffectationsServiceHoraires.Add(new AffectationServiceHoraire
            {
                ServiceId = cibleId,
                HoraireId = dialog.HoraireId.Value,
                DateDebut = dialog.DateDebut.Value,
                DateFin = dialog.DateFin
            });
        }
        else
        {
            _ctx.AffectationsHoraires.Add(new AffectationHoraire
            {
                EmployeId = cibleId,
                HoraireId = dialog.HoraireId.Value,
                DateDebut = dialog.DateDebut.Value,
                DateFin = dialog.DateFin
            });
        }

        _ctx.SaveChanges();
        ChargerAffectations();
    }
}
