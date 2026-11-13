using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using KIGest.Data.Context;
using KIGest.App.Services;

namespace KIGest.App.Views;

public partial class LoginView : Window
{
    private readonly KiGestDbContext _ctx;

    public LoginView(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
        Title = Services.AppInfo.Affichage;
        TxtVersion.Text = Services.AppInfo.Affichage;

        // Société affichée depuis les paramètres (table Parametres, groupe « Société »)
        try
        {
            var societe = _ctx.Parametres
                .AsNoTracking()
                .FirstOrDefault(p => p.Cle == "Societe.Nom")?.Valeur;
            TxtSociete.Text = string.IsNullOrWhiteSpace(societe) ? "(non renseignée)" : societe;
        }
        catch
        {
            TxtSociete.Text = "(non disponible)";
        }

        TxtLogin.Focus();
    }

    private void BtnConnexion_Click(object sender, RoutedEventArgs e)
    {
        string login = TxtLogin.Text.Trim();
        string password = TxtPassword.Password;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            TxtErreur.Text = "Identifiant et mot de passe obligatoires.";
            return;
        }

        // Recherche par login exact
        var user = _ctx.Utilisateurs.FirstOrDefault(u => u.Login == login);

        if (user == null)
        {
            TxtErreur.Text = "Identifiant inconnu.";
            return;
        }

        if (!PasswordHasher.Verify(password, user.MotDePasseHash))
        {
            TxtErreur.Text = "Mot de passe incorrect.";
            return;
        }

        // Ouvrir la session
        Session.Current = new UtilisateurSession
        {
            Id = user.Id,
            Login = user.Login,
            Role = user.Role,
            Nom = user.Login
        };

        _ctx.Journaux.Add(new Core.Entities.Journal
        {
            DateHeure = System.DateTime.Now,
            UtilisateurId = user.Id,
            Action = "LOGIN",
            Details = $"Connexion de {user.Login}"
        });
        _ctx.SaveChanges();

        DialogResult = true;
        Close();
    }

    private void BtnQuitter_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
