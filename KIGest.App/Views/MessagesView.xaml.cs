using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using KIGest.Data.Context;
using KIGest.Device.Services;

namespace KIGest.App.Views;

public partial class MessagesView : Page
{
    public class EmployeItem
    {
        public int Id { get; set; }
        public string Affichage { get; set; } = string.Empty;
    }

    private readonly KiGestDbContext _ctx;

    public MessagesView(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
        ChargerEmployes();
        ChargerPointeuses();
        ChargerMessages();
    }

    private void ChargerEmployes()
    {
        var list = _ctx.Employes.AsNoTracking()
            .OrderBy(e => e.Nom)
            .Select(e => new EmployeItem
            {
                Id = e.Id,
                Affichage = $"{e.Matricule} — {e.Prenom} {e.Nom}"
            })
            .ToList();

        CboEmploye.ItemsSource = list;
        CboEmploye.DisplayMemberPath = "Affichage";
    }

    private void ChargerPointeuses()
    {
        CboPointeuse.ItemsSource = _ctx.Pointeuses.AsNoTracking().ToList();
        CboPointeuse.DisplayMemberPath = "Nom";
        CboPointeuse.SelectedIndex = CboPointeuse.Items.Count > 0 ? 0 : -1;
    }

    private void ChargerMessages()
    {
        GrdMessages.ItemsSource = null;
        GrdMessages.ItemsSource = _ctx.MessagesAppareil
            .OrderByDescending(m => m.DateCreation)
            .Take(50)
            .AsNoTracking()
            .ToList();
    }

    private void BtnEnvoyer_Click(object sender, RoutedEventArgs e)
    {
        string titre = TxtTitre.Text.Trim();
        string contenu = TxtContenu.Text.Trim();

        if (string.IsNullOrWhiteSpace(titre) || string.IsNullOrWhiteSpace(contenu))
        {
            MessageBox.Show("Titre et contenu obligatoires.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var pointeuse = CboPointeuse.SelectedItem as Core.Entities.Pointeuse;
        if (pointeuse == null)
        {
            MessageBox.Show("Sélectionnez une pointeuse.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        bool pub = ChkPublic.IsChecked == true;
        var employe = CboEmploye.SelectedItem as EmployeItem;
        if (!pub && employe == null)
        {
            MessageBox.Show("Sélectionnez un employé pour cibler le message.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            using var device = new ZkDevice();
            if (!device.Connect(pointeuse.AdresseIP, pointeuse.Port, pointeuse.CleCom))
            {
                MessageBox.Show($"Connexion échouée vers {pointeuse.AdresseIP}.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            device.PushMessage(pointeuse.NumeroMachine, $"{titre} : {contenu}", 60);
            device.Disconnect();

            _ctx.MessagesAppareil.Add(new Core.Entities.MessageAppareil
            {
                Titre = titre,
                Contenu = contenu,
                EstPublic = pub,
                EmployeId = pub ? null : employe?.Id,
                DateCreation = DateTime.Now,
                Actif = true
            });
            _ctx.SaveChanges();
            ChargerMessages();
            BtnEffacer_Click(sender, e);
            MessageBox.Show("Message envoyé.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur d'envoi: " + ex.Message, "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnEffacer_Click(object sender, RoutedEventArgs e)
    {
        TxtTitre.Text = "";
        TxtContenu.Text = "";
        ChkPublic.IsChecked = true;
        CboEmploye.SelectedIndex = -1;
    }
}
