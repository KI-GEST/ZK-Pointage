using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using KIGest.App.Views;
using KIGest.Data.Services;
using KIGest.App.Services;

namespace KIGest.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;

    public MainWindow(IServiceProvider services)
    {
        ServicesLog("MainWindow ctor");
        _services = services;
        ServicesLog("InitializeComponent…");
        InitializeComponent();

        ServicesLog("TxtSessionUser…");
        // Libellé de session
        TxtSessionUser.Text = Session.EstConnecte ? $"{Session.Current!.Login} ({Session.Current.Role})" : "Non connecté";

        ServicesLog("TxtThemeLabel…");
        Title = AppInfo.Affichage;
        TxtThemeLabel.Text = "Thème : " + ThemeService.ThemeCourant;
        TxtVersion.Text = AppInfo.Affichage;
        // La gestion des utilisateurs est réservée aux Admin
        BtnNavUtilisateurs.Visibility = Session.EstAdmin ? Visibility.Visible : Visibility.Collapsed;
        ServicesLog("Naviguer Dashboard…");
        Naviguer<DashboardView>("Tableau de bord");
        // Avec ShutdownMode=OnExplicitShutdown, fermer la fenêtre principale arrête l'application
        Closed += (_, _) => Application.Current.Shutdown();
        ServicesLog("MainWindow ctor OK");
    }

    private void ServicesLog(string msg)
    {
        try
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ki_startup.log");
            System.IO.File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " - " + msg + Environment.NewLine);
        }
        catch { }
    }

    private T Resolve<T>() where T : Page
    {
        return (T)_services.GetRequiredService(typeof(T));
    }

    private void Naviguer<T>(string titre) where T : Page
    {
        TxtTitreEcran.Text = titre;
        MainFrame.Navigate(Resolve<T>());
    }

    private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        // Bascule le thème entre Sombre et Clair
        string nouveau = ThemeService.ThemeCourant == ThemeService.Sombre ? ThemeService.Clair : ThemeService.Sombre;
        ThemeService.Appliquer(nouveau);
        TxtThemeLabel.Text = "Thème : " + nouveau;

        try
        {
            using var scope = _services.CreateScope();
            var parametres = scope.ServiceProvider.GetRequiredService<ParametresService>();
            parametres.Set("Locale.Theme", nouveau);
        }
        catch { }
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Voulez-vous vraiment vous déconnecter ?", "KI-GEST Pointage",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Session.Current = null;
        // Fermer la fenêtre principale — le redémarrage proposera la connexion
        Close();
    }

    private void BtnChangerMdp_Click(object sender, RoutedEventArgs e) => Naviguer<ChangerMotDePasseView>("Changer le mot de passe");

    public void RefreshSessionLabel()
    {
        TxtSessionUser.Text = Session.EstConnecte ? $"{Session.Current!.Login} ({Session.Current.Role})" : "Non connecté";
    }

    private void NavDashboard_Click(object sender, RoutedEventArgs e) => Naviguer<DashboardView>("Tableau de bord");
    private void NavEmployes_Click(object sender, RoutedEventArgs e) => Naviguer<EmployesView>("Employés");
    private void NavPointeuses_Click(object sender, RoutedEventArgs e) => Naviguer<PointeusesView>("Pointeuses");
    private void NavPointages_Click(object sender, RoutedEventArgs e) => Naviguer<PointagesView>("Pointages");
    private void NavPresences_Click(object sender, RoutedEventArgs e) => Naviguer<PresencesView>("Présences");
    private void NavHoraires_Click(object sender, RoutedEventArgs e) => Naviguer<HorairesView>("Horaires & affectations");
    private void NavConges_Click(object sender, RoutedEventArgs e) => Naviguer<CongesView>("Congés & jours fériés");
    private void NavWorkCodes_Click(object sender, RoutedEventArgs e) => Naviguer<WorkCodeView>("Work Codes");
    private void NavMessages_Click(object sender, RoutedEventArgs e) => Naviguer<MessagesView>("Messages");
    private void NavSauvegarde_Click(object sender, RoutedEventArgs e) => Naviguer<SauvegardeView>("Sauvegarde & restauration");
    private void NavRapports_Click(object sender, RoutedEventArgs e) => Naviguer<RapportsView>("Rapports");
    private void NavUtilisateurs_Click(object sender, RoutedEventArgs e) => Naviguer<UtilisateursView>("Utilisateurs");
    private void NavParametres_Click(object sender, RoutedEventArgs e) => Naviguer<ParametresView>("Paramètres");
}
