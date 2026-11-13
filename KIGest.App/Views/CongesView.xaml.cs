using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Data.Context;

namespace KIGest.App.Views;

public partial class CongesView : Page
{
    private class EmployeItem
    {
        public int Id { get; set; }
        public string Affichage { get; set; } = string.Empty;
    }

    private class CongeRow
    {
        public int Id { get; set; }
        public string EmployeAffiche { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }
        public string Statut { get; set; } = string.Empty;
        public string? Motif { get; set; }
    }

    private class FerieRow
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Jour { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
    }

    private readonly KiGestDbContext _ctx;

    public CongesView(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
        ChargerEmployes();
        ChargerConges();
        ChargerFeries();
    }

    // ==================== Congés ====================

    private void ChargerEmployes()
    {
        CboEmploye.ItemsSource = _ctx.Employes.AsNoTracking()
            .Where(e => e.Actif)
            .OrderBy(e => e.Nom).ThenBy(e => e.Prenom)
            .Select(e => new EmployeItem { Id = e.Id, Affichage = $"{e.Matricule} — {e.Nom} {e.Prenom}" })
            .ToList();
    }

    private void ChargerConges()
    {
        var employes = _ctx.Employes.AsNoTracking().ToDictionary(e => e.Id);
        var rows = _ctx.Conges.AsNoTracking()
            .OrderByDescending(c => c.DateDebut)
            .ToList()
            .Select(c => new CongeRow
            {
                Id = c.Id,
                EmployeAffiche = employes.TryGetValue(c.EmployeId, out var emp) && emp != null
                    ? $"{emp.Matricule} — {emp.Nom} {emp.Prenom}"
                    : $"(employé #{c.EmployeId})",
                Type = c.Type,
                DateDebut = c.DateDebut,
                DateFin = c.DateFin,
                Statut = c.Statut,
                Motif = c.Motif
            })
            .ToList();

        GrdConges.ItemsSource = rows;
    }

    private Conge? CongeSelectionne => (GrdConges.SelectedItem as CongeRow) is { } row
        ? _ctx.Conges.FirstOrDefault(c => c.Id == row.Id)
        : null;

    private void GrdConges_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool sel = GrdConges.SelectedItem != null;
        BtnApprouver.IsEnabled = sel;
        BtnRefuser.IsEnabled = sel;
        BtnSupprimer.IsEnabled = sel;
    }

    private void BtnEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        if (CboEmploye.SelectedItem is not EmployeItem emp)
        {
            MessageBox.Show("Sélectionnez un employé.");
            return;
        }

        var debut = DtpDebut.SelectedDate;
        var fin = DtpFin.SelectedDate;
        if (debut == null || fin == null)
        {
            MessageBox.Show("Veuillez choisir la date de début et de fin.");
            return;
        }
        if (fin.Value.Date < debut.Value.Date)
        {
            MessageBox.Show("La date de fin doit être postérieure ou égale à la date de début.");
            return;
        }

        string type = ((ComboBoxItem)CboType.SelectedItem)?.Content?.ToString() ?? "Congé annuel";

        var conge = new Conge
        {
            EmployeId = emp.Id,
            Type = type,
            DateDebut = debut.Value.Date,
            DateFin = fin.Value.Date,
            Statut = "EnAttente",
            Motif = TxtMotif.Text.Trim()
        };

        _ctx.Conges.Add(conge);
        _ctx.Journaux.Add(new Journal
        {
            DateHeure = DateTime.Now,
            Action = "CONGE_DEMANDE",
            Details = $"Demande de {type} du {debut:dd/MM/yyyy} au {fin:dd/MM/yyyy} pour {emp.Affichage}"
        });
        _ctx.SaveChanges();

        DtpDebut.SelectedDate = null;
        DtpFin.SelectedDate = null;
        TxtMotif.Text = "";
        ChargerConges();
        MessageBox.Show("Demande enregistrée (statut : En attente).", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnApprouver_Click(object sender, RoutedEventArgs e) => ChangerStatut("Approuve");
    private void BtnRefuser_Click(object sender, RoutedEventArgs e) => ChangerStatut("Refuse");

    private void ChangerStatut(string nouveauStatut)
    {
        if (GrdConges.SelectedItem is not CongeRow row) return;
        var conge = _ctx.Conges.FirstOrDefault(c => c.Id == row.Id);
        if (conge == null) return;

        string ancienStatut = conge.Statut;
        conge.Statut = nouveauStatut;
        _ctx.Journaux.Add(new Journal
        {
            DateHeure = DateTime.Now,
            Action = nouveauStatut == "Approuve" ? "CONGE_APPROUVE" : "CONGE_REFUSE",
            Details = $"Congé #{conge.Id} ({conge.Type}, du {conge.DateDebut:dd/MM/yyyy} au {conge.DateFin:dd/MM/yyyy}) : {ancienStatut} → {nouveauStatut}"
        });
        _ctx.SaveChanges();
        ChargerConges();
    }

    private void BtnSupprimer_Click(object sender, RoutedEventArgs e)
    {
        if (GrdConges.SelectedItem is not CongeRow row) return;
        if (MessageBox.Show($"Supprimer la demande de congé ({row.Type}, du {row.DateDebut:dd/MM/yyyy} au {row.DateFin:dd/MM/yyyy}) ?",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var conge = _ctx.Conges.FirstOrDefault(c => c.Id == row.Id);
        if (conge != null)
        {
            _ctx.Journaux.Add(new Journal { DateHeure = DateTime.Now, Action = "CONGE_SUPPRIME", Details = $"Suppression du congé #{conge.Id}" });
            _ctx.Conges.Remove(conge);
            _ctx.SaveChanges();
            ChargerConges();
        }
    }

    // ==================== Jours fériés ====================

    private void ChargerFeries()
    {
        int annee = DateTime.Today.Year;
        GrdFeries.ItemsSource = _ctx.JoursFeries.AsNoTracking()
            .Where(f => f.Date.Year == annee)
            .OrderBy(f => f.Date)
            .ToList()
            .Select(f => new FerieRow
            {
                Id = f.Id,
                Date = f.Date,
                Jour = f.Date.ToString("dddd", CultureInfo.GetCultureInfo("fr-FR")),
                Libelle = f.Libelle
            })
            .ToList();
    }

    private void GrdFeries_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnSupprimerFerie.IsEnabled = GrdFeries.SelectedItem != null;
    }

    private void BtnAjouterFerie_Click(object sender, RoutedEventArgs e)
    {
        var date = DtpFerie.SelectedDate;
        string libelle = TxtLibelleFerie.Text.Trim();
        if (date == null || string.IsNullOrWhiteSpace(libelle))
        {
            MessageBox.Show("Date et libellé obligatoires.");
            return;
        }
        if (_ctx.JoursFeries.Any(f => f.Date == date.Value.Date))
        {
            MessageBox.Show("Un férié existe déjà à cette date.");
            return;
        }
        _ctx.JoursFeries.Add(new JourFerie { Date = date.Value.Date, Libelle = libelle });
        _ctx.SaveChanges();
        TxtLibelleFerie.Text = "";
        ChargerFeries();
    }

    private void BtnSupprimerFerie_Click(object sender, RoutedEventArgs e)
    {
        if (GrdFeries.SelectedItem is not FerieRow row) return;
        if (MessageBox.Show($"Supprimer le férié du {row.Date:dd/MM/yyyy} ({row.Libelle}) ?",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var ferie = _ctx.JoursFeries.FirstOrDefault(f => f.Id == row.Id);
        if (ferie != null)
        {
            _ctx.JoursFeries.Remove(ferie);
            _ctx.SaveChanges();
            ChargerFeries();
        }
    }

    private void BtnSeedTunisie_Click(object sender, RoutedEventArgs e)
    {
        int annee = DateTime.Today.Year;
        var feriesNationaux = new List<(DateTime Date, string Libelle)>
        {
            new(new DateTime(annee, 1, 1),  "Jour de l'An"),
            new(new DateTime(annee, 3, 20), "Fête de l'Indépendance"),
            new(new DateTime(annee, 4, 9),  "Journée des Martyrs"),
            new(new DateTime(annee, 5, 1),  "Fête du Travail"),
            new(new DateTime(annee, 7, 25), "Fête de la République"),
            new(new DateTime(annee, 8, 13), "Fête de la Femme"),
            new(new DateTime(annee, 12, 17),"Fête de la Révolution et de la Dignité"),
        };

        int ajoutes = 0;
        foreach (var (date, libelle) in feriesNationaux)
        {
            if (_ctx.JoursFeries.Any(f => f.Date == date)) continue;
            _ctx.JoursFeries.Add(new JourFerie { Date = date, Libelle = libelle });
            ajoutes++;
        }
        _ctx.SaveChanges();
        ChargerFeries();
        MessageBox.Show($"{ajoutes} férié(s) tunisien(s) ajouté(s).\nLes fêtes religieuses (Aïd, Mouled) sont à saisir chaque année (dates variables).",
            "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
