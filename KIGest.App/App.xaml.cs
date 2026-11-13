using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KIGest.Data.Context;
using KIGest.Data.Services;
using KIGest.App.Services;

namespace KIGest.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Mode ligne de commande de l'installateur : créer la base sans ouvrir l'UI
        if (e.Args.Any(a => string.Equals(a, "--initdb", StringComparison.OrdinalIgnoreCase)))
        {
            int code = Services.DatabaseBootstrapper.Run(e.Args);
            Shutdown(code);
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        try
        {
            using var scope = ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<KiGestDbContext>();
            DatabaseInitializer.Initialize(context);

            // Thème enregistré dans les paramètres (défaut : Sombre)
            string theme = context.Parametres.FirstOrDefault(p => p.Cle == "Locale.Theme")?.Valeur ?? ThemeService.Sombre;
            ThemeService.Appliquer(theme);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Impossible d'accéder à la base SQL Server « POINTAGE ».\n\n" + ex.Message +
                "\n\nVérifiez que le serveur SQL est démarré et que la base POINTAGE existe " +
                "(elle doit être créée une fois par un administrateur SQL, l'utilisateur applicatif n'ayant pas le droit de création).",
                "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Login obligatoire
        using var scope2 = ServiceProvider.CreateScope();
        var contextLogin = scope2.ServiceProvider.GetRequiredService<KiGestDbContext>();
        LogError("Chargement login…");
        var login = new Views.LoginView(contextLogin);
        LogError("ShowDialog()…");
        bool? loginOk = login.ShowDialog();
        LogError($"ShowDialog() retourné {loginOk}");

        if (loginOk != true)
        {
            LogError("Login non réussi (DialogResult != true) -> Shutdown(0)");
            Shutdown(0);
            return;
        }

        LogError("Show MainWindow…");
        try
        {
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            LogError("MainWindow construite. Show()…");
            mainWindow.Show();
            mainWindow.Activate();
            LogError("MainWindow OK affichée et activée.");
        }
        catch (Exception ex)
        {
            LogError("ERREUR affichage MainWindow: " + ex);
            MessageBox.Show("Erreur à l'ouverture : " + ex.Message + "\n\n" + ex,
                "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void LogError(string message)
    {
        try
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ki_startup.log");
            System.IO.File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message + Environment.NewLine);
        }
        catch { }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var connectionString = SqlConfig.ConnectionString
            ?? throw new InvalidOperationException("Chaîne de connexion SQL Server introuvable (appsettings.json / KIGest.json).");

        services.AddDbContext<KiGestDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddSingleton<ParametresService>();
        services.AddTransient<MainWindow>();
        services.AddTransient<Views.DashboardView>();
        services.AddTransient<Views.EmployesView>();
        services.AddTransient<Views.PointeusesView>();
        services.AddTransient<Views.PointagesView>();
        services.AddTransient<Views.PresencesView>();
        services.AddTransient<Views.HorairesView>();
        services.AddTransient<Views.ChangerMotDePasseView>();
        services.AddTransient<Views.CongesView>();
        services.AddTransient<Views.UtilisateursView>();
        services.AddTransient<Views.SauvegardeView>();
        services.AddTransient<Views.WorkCodeView>();
        services.AddTransient<Views.MessagesView>();
        services.AddTransient<Views.RapportsView>();
        services.AddTransient<Views.ParametresView>();
    }
}
