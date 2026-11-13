using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Data.Context;

namespace KIGest.App.Views;

public partial class EmployesView : Page
{
    private readonly KiGestDbContext _ctx;
    private Employe? _courant;

    public EmployesView(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        var pointeuses = _ctx.Pointeuses.OrderBy(p => p.Nom).ToList();
        CboPointeuse.ItemsSource = pointeuses;
        CboPointeuse.SelectedIndex = -1;

        CboActif.ItemsSource = new[] { "Tous", "Actifs", "Inactifs" };
        CboActif.SelectedIndex = 0;

        Charger();
    }

    private void Charger()
    {
        try
        {
            string recherche = TxtRecherche.Text.Trim();

            var query = _ctx.Employes
                .Include(e => e.Service)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(recherche))
            {
                query = query.Where(e =>
                    e.Nom.Contains(recherche) ||
                    e.Prenom.Contains(recherche) ||
                    e.Matricule.Contains(recherche) ||
                    e.BadgeNumero.Contains(recherche) ||
                    (e.Service != null && e.Service.Libelle.Contains(recherche)));
            }

            // Filtre par pointeuse : employés dont le badge a été vu sur cet appareil
            if (CboPointeuse.SelectedItem is Pointeuse pointeuse)
            {
                var badges = _ctx.PointagesBruts
                    .AsNoTracking()
                    .Where(p => p.PointeuseId == pointeuse.Id)
                    .Select(p => p.BadgeNumero)
                    .Distinct()
                    .ToList();

                query = query.Where(e => badges.Contains(e.BadgeNumero));
            }

            // Filtre par statut
            switch (CboActif.SelectedItem?.ToString())
            {
                case "Actifs":
                    query = query.Where(e => e.Actif);
                    break;
                case "Inactifs":
                    query = query.Where(e => !e.Actif);
                    break;
            }

            var liste = query
                .OrderBy(e => e.Nom)
                .ThenBy(e => e.Prenom)
                .ToList();

            GrdEmployes.ItemsSource = null;
            GrdEmployes.ItemsSource = liste;

            int total = _ctx.Employes.Count();
            int actifs = _ctx.Employes.Count(e => e.Actif);
            TxtTotal.Text = $"{liste.Count} affiché(s) — {actifs} actif(s) sur {total} employé(s)";
        }
        catch (Exception ex)
        {
            TxtTotal.Text = "Erreur de chargement : " + ex.Message;
        }
    }

    private void CboPointeuse_SelectionChanged(object sender, SelectionChangedEventArgs e) => Charger();

    private void CboActif_SelectionChanged(object sender, SelectionChangedEventArgs e) => Charger();

    private void TxtRecherche_TextChanged(object sender, TextChangedEventArgs e) => Charger();

    private void BtnActualiser_Click(object sender, RoutedEventArgs e) => Charger();

    private void BtnReinitialiser_Click(object sender, RoutedEventArgs e)
    {
        TxtRecherche.Text = "";
        CboPointeuse.SelectedIndex = -1;
        CboActif.SelectedIndex = 0;
        Charger();
    }

    private void GrdEmployes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _courant = GrdEmployes.SelectedItem as Employe;
    }

    private void BtnNouveau_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EmployeDialog(_ctx, null)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            Charger();
        }
    }

    private void BtnModifier_Click(object sender, RoutedEventArgs e)
    {
        if (_courant == null)
        {
            MessageBox.Show("Sélectionnez d'abord un employé dans la liste.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // On recharge l'entité suivie pour l'édition
        var employe = _ctx.Employes.FirstOrDefault(e => e.Id == _courant.Id);
        if (employe == null) return;

        var dialog = new EmployeDialog(_ctx, employe)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            Charger();
        }
    }

    private void BtnSupprimer_Click(object sender, RoutedEventArgs e)
    {
        if (_courant == null)
        {
            MessageBox.Show("Sélectionnez d'abord un employé dans la liste.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Supprimer « {_courant.Nom} {_courant.Prenom} » ?\nLes pointages associés seront conservés.",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            var employe = _ctx.Employes.FirstOrDefault(e => e.Id == _courant.Id);
            if (employe == null) return;

            _ctx.Employes.Remove(employe);
            _ctx.SaveChanges();
            Charger();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur : " + ex.Message, "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnBasculerActif_Click(object sender, RoutedEventArgs e)
    {
        if (_courant == null)
        {
            MessageBox.Show("Sélectionnez d'abord un employé dans la liste.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var employe = _ctx.Employes.FirstOrDefault(e => e.Id == _courant.Id);
            if (employe == null) return;

            employe.Actif = !employe.Actif;
            _ctx.SaveChanges();
            Charger();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur : " + ex.Message, "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnServices_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ServicesDialog(_ctx)
        {
            Owner = Window.GetWindow(this)
        };

        dialog.ShowDialog();
        Charger();
    }

    private void BtnImporterPointeuse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ImportEmployesDialog(_ctx)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            Charger();
        }
    }

    private void BtnExporterVersPointeuse_Click(object sender, RoutedEventArgs e)
    {
        var pointeuse = _ctx.Pointeuses.FirstOrDefault(p => p.Actif);
        if (pointeuse == null)
        {
            MessageBox.Show("Aucune pointeuse active configurée.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var employes = _ctx.Employes.Where(e => e.Actif).ToList();
        if (employes.Count == 0)
        {
            MessageBox.Show("Aucun employé à exporter.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Exporter {employes.Count} employé(s) vers {pointeuse.Nom} ({pointeuse.AdresseIP}:{pointeuse.Port}) ?\n\nLes utilisateurs existants sur la pointeuse seront écrasés.",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var device = new KIGest.Device.Services.ZkDevice();
            if (!device.Connect(pointeuse.AdresseIP, pointeuse.Port, pointeuse.CleCom))
            {
                MessageBox.Show("Connexion à la pointeuse échouée.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int ok = 0;
            foreach (var emp in employes)
            {
                try
                {
                    device.SetUserInfo(pointeuse.NumeroMachine, emp.BadgeNumero, $"{emp.Nom} {emp.Prenom}", "", 0, true);
                    ok++;
                }
                catch { }
            }

            device.Disconnect();

            pointeuse.DerniereSynchro = DateTime.Now;
            _ctx.SaveChanges();

            MessageBox.Show($"{ok}/{employes.Count} employé(s) exporté(s) vers la pointeuse.",
                "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur : " + ex.Message, "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
