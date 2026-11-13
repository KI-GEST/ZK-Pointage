using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KIGest.App.Services;
using KIGest.Data.Context;

namespace KIGest.App.Views;

public partial class ChangerMotDePasseView : Page
{
    private readonly KiGestDbContext _ctx;

    public ChangerMotDePasseView(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        if (!Session.EstConnecte)
        {
            TxtErreur.Text = "Session non établie.";
            BtnEnregistrer.IsEnabled = false;
        }
    }

    private void BtnEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        TxtErreur.Text = "";

        if (!Session.EstConnecte)
        {
            TxtErreur.Text = "Session non établie.";
            return;
        }

        var user = _ctx.Utilisateurs.FirstOrDefault(u => u.Id == Session.Current!.Id);
        if (user == null)
        {
            TxtErreur.Text = "Utilisateur introuvable.";
            return;
        }

        if (!PasswordHasher.Verify(TxtAncien.Password, user.MotDePasseHash))
        {
            TxtErreur.Text = "Mot de passe actuel incorrect.";
            return;
        }

        string nouveau = TxtNouveau.Password;
        if (nouveau.Length < 4)
        {
            TxtErreur.Text = "Mot de passe trop court (min. 4 caractères).";
            return;
        }

        if (nouveau != TxtConfirmation.Password)
        {
            TxtErreur.Text = "Les deux saisies ne correspondent pas.";
            return;
        }

        user.MotDePasseHash = PasswordHasher.Hash(nouveau);

        _ctx.Journaux.Add(new Core.Entities.Journal
        {
            DateHeure = System.DateTime.Now,
            UtilisateurId = user.Id,
            Action = "CHANGE_PASSWORD",
            Details = $"Changement du mot de passe pour {user.Login}"
        });

        _ctx.SaveChanges();
        MessageBox.Show("Mot de passe changé avec succès.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
        TxtAncien.Password = "";
        TxtNouveau.Password = "";
        TxtConfirmation.Password = "";
    }
}
