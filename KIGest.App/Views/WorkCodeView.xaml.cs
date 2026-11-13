using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using KIGest.Data.Context;
using KIGest.Device.Services;

namespace KIGest.App.Views;

public partial class WorkCodeView : Page
{
    private readonly KiGestDbContext _ctx;

    public WorkCodeView(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
        Charger();
    }

    private void Charger()
    {
        GrdWorkCodes.ItemsSource = null;
        GrdWorkCodes.ItemsSource = _ctx.WorkCodes.AsNoTracking().OrderBy(w => w.Code).ToList();
    }

    private void GrdWorkCodes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var w = GrdWorkCodes.SelectedItem as Core.Entities.WorkCode;
        BtnSupprimer.IsEnabled = w != null;
        if (w != null)
        {
            TxtCode.Text = w.Code;
            TxtLibelle.Text = w.Libelle;
            ChkActif.IsChecked = w.Actif;
        }
    }

    private void BtnNouveau_Click(object sender, RoutedEventArgs e)
    {
        TxtCode.Text = "";
        TxtLibelle.Text = "";
        ChkActif.IsChecked = true;
        TxtCode.Focus();
    }

    private void BtnEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtCode.Text.Trim();
        string libelle = TxtLibelle.Text.Trim();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(libelle))
        {
            MessageBox.Show("Code et libellé obligatoires.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_ctx.WorkCodes.Any(w => w.Code == code))
        {
            MessageBox.Show($"Le work code « {code} » existe déjà.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _ctx.WorkCodes.Add(new Core.Entities.WorkCode { Code = code, Libelle = libelle, Actif = ChkActif.IsChecked == true });
        _ctx.SaveChanges();
        Charger();
        BtnNouveau_Click(sender, e);
    }

    private void BtnSupprimer_Click(object sender, RoutedEventArgs e)
    {
        if (GrdWorkCodes.SelectedItem is not Core.Entities.WorkCode w) return;

        if (MessageBox.Show($"Supprimer le work code « {w.Code} — {w.Libelle} » ?",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _ctx.WorkCodes.Remove(w);
        _ctx.SaveChanges();
        Charger();
    }

    private void BtnSynchroniser_Click(object sender, RoutedEventArgs e)
    {
        var pointeuse = _ctx.Pointeuses.FirstOrDefault(p => p.Actif);
        if (pointeuse == null)
        {
            MessageBox.Show("Aucune pointeuse active.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            using var device = new ZkDevice();
            if (!device.Connect(pointeuse.AdresseIP, pointeuse.Port, pointeuse.CleCom))
            {
                MessageBox.Show("Connexion échouée.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var actifs = _ctx.WorkCodes.Where(w => w.Actif).OrderBy(w => w.Code).ToList();
            if (actifs.Count == 0)
            {
                MessageBox.Show("Aucun work code actif.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sb = new System.Text.StringBuilder();
            foreach (var w in actifs)
            {
                // Format batch : "WORKC CODE_NAME|WORKC CODE_NAME|..."
                sb.Append((w.Code + "|" + w.Libelle + "\n"));
            }
            device.PushWorkCodeBatch(pointeuse.NumeroMachine, sb.ToString());

            device.Disconnect();
            MessageBox.Show($"{actifs.Count} work code(s) synchronisé(s) vers {pointeuse.Nom}.",
                "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur de synchronisation : " + ex.Message, "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnEffacer_Click(object sender, RoutedEventArgs e)
    {
        TxtCode.Text = "";
        TxtLibelle.Text = "";
        ChkActif.IsChecked = true;
    }
}
