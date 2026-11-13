using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Data.Context;

namespace KIGest.App.Views;

public partial class ServicesDialog : Window
{
    private readonly KiGestDbContext _ctx;
    private ServiceRow? _courant;

    public ServicesDialog(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
        Charger();
    }

    private void Charger()
    {
        var liste = _ctx.Services
            .AsNoTracking()
            .Select(s => new ServiceRow
            {
                Id = s.Id,
                Libelle = s.Libelle,
                NombreEmployes = _ctx.Employes.Count(e => e.ServiceId == s.Id)
            })
            .OrderBy(s => s.Libelle)
            .ToList();

        GrdServices.ItemsSource = null;
        GrdServices.ItemsSource = liste;
    }

    public class ServiceRow
    {
        public int Id { get; set; }
        public string Libelle { get; set; } = string.Empty;
        public int NombreEmployes { get; set; }
    }

    private void GrdServices_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _courant = GrdServices.SelectedItem as ServiceRow;
        if (_courant != null) TxtLibelle.Text = _courant.Libelle;
    }

    private void BtnAjouter_Click(object sender, RoutedEventArgs e)
    {
        string libelle = TxtLibelle.Text.Trim();
        if (string.IsNullOrWhiteSpace(libelle))
        {
            MessageBox.Show("Saisissez le libellé du service.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_ctx.Services.Any(s => s.Libelle == libelle))
        {
            MessageBox.Show($"Le service « {libelle} » existe déjà.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _ctx.Services.Add(new Service { Libelle = libelle });
        _ctx.SaveChanges();
        TxtLibelle.Text = "";
        Charger();
    }

    private void BtnRenommer_Click(object sender, RoutedEventArgs e)
    {
        if (_courant == null)
        {
            MessageBox.Show("Sélectionnez un service.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string libelle = TxtLibelle.Text.Trim();
        if (string.IsNullOrWhiteSpace(libelle))
        {
            MessageBox.Show("Saisissez le libellé du service.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var service = _ctx.Services.FirstOrDefault(s => s.Id == _courant.Id);
        if (service == null) return;

        if (_ctx.Services.Any(s => s.Libelle == libelle && s.Id != service.Id))
        {
            MessageBox.Show($"Le service « {libelle} » existe déjà.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        service.Libelle = libelle;
        _ctx.SaveChanges();
        Charger();
    }

    private void BtnSupprimer_Click(object sender, RoutedEventArgs e)
    {
        if (_courant == null)
        {
            MessageBox.Show("Sélectionnez un service.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int effectif = _ctx.Employes.Count(e => e.ServiceId == _courant.Id);
        if (effectif > 0)
        {
            MessageBox.Show($"Impossible de supprimer : {effectif} employé(s) sont affectés à ce service.",
                "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var service = _ctx.Services.FirstOrDefault(s => s.Id == _courant.Id);
        if (service == null) return;

        _ctx.Services.Remove(service);
        _ctx.SaveChanges();
        TxtLibelle.Text = "";
        Charger();
    }

    private void BtnFermer_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
