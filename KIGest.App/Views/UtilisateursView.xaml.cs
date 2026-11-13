using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using KIGest.App.Services;
using KIGest.Core.Entities;
using KIGest.Data.Context;

namespace KIGest.App.Views;

public partial class UtilisateursView : Page
{
    private class UtilisateurRow
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    private readonly KiGestDbContext _ctx;

    public UtilisateursView(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        if (!Session.EstAdmin)
        {
            MessageBox.Show("Cette page est réservée aux administrateurs.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            IsEnabled = false;
            return;
        }

        Charger();
    }

    private void Charger()
    {
        GrdUtilisateurs.ItemsSource = _ctx.Utilisateurs.AsNoTracking()
            .OrderBy(u => u.Login)
            .Select(u => new UtilisateurRow { Id = u.Id, Login = u.Login, Role = u.Role })
            .ToList();

        GrdUtilisateurs_SelectionChanged(this, null!);
    }

    private Utilisateur? UtilisateurSelectionne
    {
        get
        {
            if (GrdUtilisateurs.SelectedItem is not UtilisateurRow row) return null;
            return _ctx.Utilisateurs.FirstOrDefault(u => u.Id == row.Id);
        }
    }

    private int NombreAdmins => _ctx.Utilisateurs.Count(u => u.Role == "Admin");

    private void GrdUtilisateurs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var u = UtilisateurSelectionne;
        bool sel = u != null;

        BtnPromouvoir.IsEnabled = sel && u!.Role != "Admin";
        BtnRetrograder.IsEnabled = sel && u!.Role == "Admin" && NombreAdmins > 1;
        BtnRenitialiserMdp.IsEnabled = sel;
        BtnSupprimer.IsEnabled = sel && ((u!.Role != "Admin") || NombreAdmins > 1);
    }

    private void BtnPromouvoir_Click(object sender, RoutedEventArgs e) => ChangerRole("Admin");

    private void BtnRetrograder_Click(object sender, RoutedEventArgs e) => ChangerRole("Utilisateur");

    private void ChangerRole(string nouveauRole)
    {
        var u = UtilisateurSelectionne;
        if (u == null) return;

        string ancienRole = u.Role;
        if (ancienRole == nouveauRole) return;

        // Empêcher de dégrader le dernier Admin
        if (ancienRole == "Admin" && nouveauRole != "Admin" && NombreAdmins <= 1)
        {
            MessageBox.Show("Impossible de dégrader le dernier compte Administrateur.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        u.Role = nouveauRole;
        Journaliser(nouveauRole == "Admin" ? "UTILISATEUR_PROMU_ADMIN" : "UTILISATEUR_RETROGRADE",
            $"Rôle de « {u.Login} » : {ancienRole} → {nouveauRole}", u.Id);
        _ctx.SaveChanges();
        Charger();
    }

    private void BtnCreer_Click(object sender, RoutedEventArgs e)
    {
        string login = TxtLogin.Text.Trim();
        string motDePasse = TxtMotDePasse.Password;
        string role = ((ComboBoxItem)CboRole.SelectedItem)?.Content?.ToString() ?? "Utilisateur";

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(motDePasse))
        {
            MessageBox.Show("Login et mot de passe obligatoires.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_ctx.Utilisateurs.Any(u => u.Login.ToLower() == login.ToLower()))
        {
            MessageBox.Show($"Le login « {login} » existe déjà.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var u = new Utilisateur
        {
            Login = login,
            MotDePasseHash = PasswordHasher.Hash(motDePasse),
            Role = role
        };

        _ctx.Utilisateurs.Add(u);
        Journaliser("UTILISATEUR_CREATE", $"Compte « {login} » créé avec le rôle {role}", null);
        _ctx.SaveChanges();

        TxtLogin.Text = "";
        TxtMotDePasse.Password = "";
        CboRole.SelectedIndex = 1;
        Charger();
        MessageBox.Show($"Compte « {login} » créé.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnRenitialiserMdp_Click(object sender, RoutedEventArgs e)
    {
        var u = UtilisateurSelectionne;
        if (u == null) return;

        string nouveau = TxtNouveauMdp.Password;
        if (string.IsNullOrWhiteSpace(nouveau) || nouveau.Length < 4)
        {
            MessageBox.Show("Le nouveau mot de passe doit contenir au moins 4 caractères.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"Réinitialiser le mot de passe de « {u.Login} » ?",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        u.MotDePasseHash = PasswordHasher.Hash(nouveau);
        Journaliser("UTILISATEUR_RESET_PASSWORD", $"Réinitialisation du mot de passe de « {u.Login} »", u.Id);
        _ctx.SaveChanges();
        TxtNouveauMdp.Password = "";
        MessageBox.Show($"Mot de passe de « {u.Login} » réinitialisé.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnSupprimer_Click(object sender, RoutedEventArgs e)
    {
        var u = UtilisateurSelectionne;
        if (u == null) return;

        if (u.Role == "Admin" && NombreAdmins <= 1)
        {
            MessageBox.Show("Impossible de supprimer le dernier compte Administrateur.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"Supprimer le compte « {u.Login} » (rôle {u.Role}) ?",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Journaliser("UTILISATEUR_DELETE", $"Compte « {u.Login} » (rôle {u.Role}) supprimé", u.Id);
        _ctx.Utilisateurs.Remove(u);
        _ctx.SaveChanges();
        Charger();
    }

    private void Journaliser(string action, string details, int? utilisateurId)
    {
        _ctx.Journaux.Add(new Journal
        {
            DateHeure = DateTime.Now,
            UtilisateurId = Session.Current?.Id,
            Action = action,
            Details = details
        });
    }
}
