using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using KIGest.Data.Context;
using KIGest.Device.Services;

namespace KIGest.App.Views;

public partial class ImportEmployesDialog : Window
{
    /// <summary>Ligne d'import d'un employé lu sur la pointeuse.</summary>
    public class ImportRow
    {
        public bool Selectionner { get; set; }
        public string BadgeNumero { get; set; } = string.Empty;
        public string NomAppareil { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Matricule { get; set; } = string.Empty;
        public string Statut { get; set; } = "";
        public bool Existant { get; set; }
        public int EmployeId { get; set; }
    }

    private readonly KiGestDbContext _ctx;
    private List<ImportRow> _lignes = new();

    public ImportEmployesDialog(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        var pointeuses = _ctx.Pointeuses.OrderBy(p => p.Nom).ToList();
        CboPointeuse.ItemsSource = pointeuses;
        if (pointeuses.Count > 0) CboPointeuse.SelectedIndex = 0;

        CboDecoupage.ItemsSource = new[] { "NOM Prénom", "Prénom NOM", "Ne pas découper" };
        CboDecoupage.SelectedIndex = 0;
    }

    /// <summary>Trace les appels SDK dans le fichier de log de l'application.</summary>
    private void Log(string message)
    {
        try
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zk_debug.log");
            System.IO.File.AppendAllText(path,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - [ImportEmployes] " + message + Environment.NewLine);
        }
        catch { }
    }

    private void BtnLire_Click(object sender, RoutedEventArgs e)
    {
        if (CboPointeuse.SelectedItem is not Core.Entities.Pointeuse pointeuse)
        {
            TxtStatut.Text = "Sélectionnez une pointeuse.";
            return;
        }

        try
        {
            BtnLire.IsEnabled = false;
            TxtStatut.Text = $"Lecture sur {pointeuse.Nom}...";

            using var device = new ZkDevice();
            if (!device.Connect(pointeuse.AdresseIP, pointeuse.Port, pointeuse.CleCom))
            {
                TxtStatut.Text = $"Échec de connexion à {pointeuse.AdresseIP}:{pointeuse.Port}";
                return;
            }

            var users = device.ReadAllUsers(pointeuse.NumeroMachine, msg => Log(msg));

            if (users.Count == 0)
            {
                TxtStatut.Text = "Aucun utilisateur trouvé sur la pointeuse.";
                Log("Aucun utilisateur retourné par la pointeuse.");
                _lignes.Clear();
                GrdUtilisateurs.ItemsSource = null;
                BtnImporter.IsEnabled = false;
                return;
            }

            var badgesEnBase = _ctx.Employes
                .Where(e => users.Select(u => u.UserId).Contains(e.BadgeNumero))
                .ToList();

            _lignes = users.Select(u =>
            {
                var existant = badgesEnBase.FirstOrDefault(e => e.BadgeNumero == u.UserId);
                (string nom, string prenom) = ScinderNom(u.Name);

                return new ImportRow
                {
                    Selectionner = existant == null,
                    BadgeNumero = u.UserId,
                    NomAppareil = u.Name,
                    Nom = existant?.Nom ?? nom,
                    Prenom = existant?.Prenom ?? prenom,
                    Matricule = existant?.Matricule ?? u.UserId,
                    Statut = existant != null ? "Oui" : "Non",
                    Existant = existant != null,
                    EmployeId = existant?.Id ?? 0
                };
            })
            .OrderBy(l => l.Nom).ThenBy(l => l.Prenom)
            .ToList();

            GrdUtilisateurs.ItemsSource = null;
            GrdUtilisateurs.ItemsSource = _lignes;
            BtnImporter.IsEnabled = _lignes.Any(l => l.Selectionner);

            TxtStatut.Text = $"{users.Count} utilisateur(s) lu(s) — {_lignes.Count(l => l.Existant)} déjà en base";
        }
        catch (Exception ex)
        {
            TxtStatut.Text = "Erreur : " + ex.Message;
        }
        finally
        {
            BtnLire.IsEnabled = true;
        }
    }

    /// <summary>Sépare un nom d'appareil selon le mode de découpage choisi.</summary>
    private (string nom, string prenom) ScinderNom(string nomAppareil)
    {
        string brut = (nomAppareil ?? "").Trim();

        // Certains appareils ont le numéro de badge comme nom
        if (string.IsNullOrWhiteSpace(brut) || brut == brut.Trim() && int.TryParse(brut, out _))
            return ("", "");

        string mode = CboDecoupage.SelectedItem?.ToString() ?? "NOM Prénom";

        if (mode == "Ne pas découper")
            return (brut, "");

        var parts = brut.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return ("", "");
        if (parts.Length == 1) return (parts[0], "");

        if (mode == "Prénom NOM")
            return (parts[parts.Length - 1], string.Join(" ", parts.Take(parts.Length - 1)));

        return (parts[0], string.Join(" ", parts.Skip(1)));
    }

    private void CboDecoupage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_lignes.Count == 0) return;

        foreach (var ligne in _lignes)
        {
            if (ligne.Existant) continue;
            (string nom, string prenom) = ScinderNom(ligne.NomAppareil);
            ligne.Nom = nom;
            ligne.Prenom = prenom;
        }

        GrdUtilisateurs.ItemsSource = null;
        GrdUtilisateurs.ItemsSource = _lignes;
    }

    private void BtnToutCocher_Click(object sender, RoutedEventArgs e)
    {
        foreach (var l in _lignes) l.Selectionner = true;
        GrdUtilisateurs.ItemsSource = null;
        GrdUtilisateurs.ItemsSource = _lignes;
        BtnImporter.IsEnabled = _lignes.Any(l => l.Selectionner);
    }

    private void BtnToutDecocher_Click(object sender, RoutedEventArgs e)
    {
        foreach (var l in _lignes) l.Selectionner = false;
        GrdUtilisateurs.ItemsSource = null;
        GrdUtilisateurs.ItemsSource = _lignes;
        BtnImporter.IsEnabled = _lignes.Any(l => l.Selectionner);
    }

    private void BtnImporter_Click(object sender, RoutedEventArgs e)
    {
        var aImporter = _lignes.Where(l => l.Selectionner).ToList();
        if (aImporter.Count == 0)
        {
            MessageBox.Show("Sélectionnez au moins un employé à importer.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            int crees = 0;
            int modifies = 0;

            foreach (var ligne in aImporter)
            {
                string nom = ligne.Nom.Trim();
                string prenom = ligne.Prenom.Trim();
                string badge = ligne.BadgeNumero.Trim();

                if (badge.Length == 0) continue;

                var employe = ligne.Existant
                    ? _ctx.Employes.FirstOrDefault(e => e.Id == ligne.EmployeId)
                    : _ctx.Employes.FirstOrDefault(e => e.BadgeNumero == badge);

                if (employe == null)
                {
                    employe = new Core.Entities.Employe
                    {
                        BadgeNumero = badge,
                        Matricule = string.IsNullOrWhiteSpace(ligne.Matricule) ? badge : ligne.Matricule.Trim(),
                        Nom = nom,
                        Prenom = prenom,
                        Actif = true
                    };
                    _ctx.Employes.Add(employe);
                    crees++;
                }
                else
                {
                    employe.Nom = string.IsNullOrWhiteSpace(nom) ? employe.Nom : nom;
                    employe.Prenom = string.IsNullOrWhiteSpace(prenom) ? employe.Prenom : prenom;
                    if (!string.IsNullOrWhiteSpace(ligne.Matricule)) employe.Matricule = ligne.Matricule.Trim();
                    modifies++;
                }
            }

            _ctx.SaveChanges();

            MessageBox.Show($"{crees} employé(s) créé(s), {modifies} mis à jour.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur d'import : " + ex.Message, "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAnnuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
