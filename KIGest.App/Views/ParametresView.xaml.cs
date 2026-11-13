using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KIGest.App.Services;
using KIGest.Core.Entities;
using KIGest.Data.Context;
using KIGest.Data.Services;

namespace KIGest.App.Views;

public partial class ParametresView : Page
{
    private static readonly Dictionary<string, (string Icone, string Description)> GroupesConfig = new()
    {
        ["Société"] = ("S", "Raison sociale, identification fiscale, coordonnées"),
        ["Général"] = ("G", "Culture, format de dates, langue, thème"),
        ["Pointage"] = ("P", "Horaires, tolérance, anti-rebond, synchronisation"),
        ["Présences"] = ("Pr", "Jours de travail, heures sup, alertes absences"),
        ["Exports"] = ("E", "Format et dossiers de destination des rapports Excel/PDF"),
        ["Notifications"] = ("N", "SMTP, destinataires, absences et retards par email"),
        ["Sécurité"] = ("Sec", "Mots de passe, verrouillage, expiration, audit"),
        ["Sauvegarde"] = ("Sv", "Dossier, rétention et fréquence des sauvegardes"),
    };

    private readonly KiGestDbContext _ctx;
    private readonly ParametresService _parametres;
    private readonly Dictionary<string, FrameworkElement> _controles = new();
    private readonly Dictionary<string, Parametre> _parametresCourants = new();
    private string _groupeCourant = string.Empty;

    public ParametresView(KiGestDbContext ctx, ParametresService parametres)
    {
        _ctx = ctx;
        _parametres = parametres;
        InitializeComponent();

        ChargerGroupes();
    }

    private static string GetIcone(string groupe) =>
        GroupesConfig.TryGetValue(groupe, out var cfg) ? cfg.Icone : "◆";

    private static string GetDescription(string groupe) =>
        GroupesConfig.TryGetValue(groupe, out var cfg) ? cfg.Description : "";

    private void ChargerGroupes()
    {
        var groupes = _ctx.Parametres.Select(p => p.Groupe).Distinct().OrderBy(g => g).ToList();
        LstGroupes.ItemsSource = groupes;
        if (groupes.Count > 0) LstGroupes.SelectedIndex = 0;
    }

    private void LstGroupes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstGroupes.SelectedItem is not string groupe) return;
        _groupeCourant = groupe;
        TxtTitreSection.Text = "Configuration " + groupe;
        TxtDescriptionSection.Text = GetDescription(groupe);
        TxtIconBadge.Text = GetIcone(groupe);
        ChargerParametres(groupe);
    }

    // ---------- Construction dynamique du formulaire ----------

    private void ChargerParametres(string groupe)
    {
        PanneauParametres.Children.Clear();
        _controles.Clear();
        _parametresCourants.Clear();

        var liste = _ctx.Parametres.Where(p => p.Groupe == groupe)
            .OrderBy(p => p.Cle)
            .ToList();

        foreach (var p in liste)
        {
            _parametresCourants[p.Cle] = p;
            PanneauParametres.Children.Add(ConstruireLigne(p));
        }

        TxtStatut.Text = $"{liste.Count} paramètre(s) — cliquez sur « Enregistrer » pour sauvegarder.";
    }

    private FrameworkElement ConstruireLigne(Parametre p)
    {
        var grid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 10)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Libellé
        var label = new TextBlock
        {
            Text = p.Cle,
            Style = (Style)Application.Current.FindResource("Text.Label"),
            FontSize = 12,
            Margin = new Thickness(0, 4, 12, 0),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(label, 0);
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        // Contrôle selon le type
        FrameworkElement control = p.Type switch
        {
            "Booleen" => ConstruireCheckBox(p),
            "Enum" => ConstruireComboBox(p),
            "Nombre" => ConstruireNombre(p),
            "Heure" => ConstruireHeure(p),
            "MotDePasse" => ConstruireMotDePasse(p),
            _ when p.Cle.Contains("MotDePasse", StringComparison.OrdinalIgnoreCase) => ConstruireMotDePasse(p),
            _ when p.Cle.Contains("Email", StringComparison.OrdinalIgnoreCase) => ConstruireTexte(p, "email"),
            _ when p.Cle.Contains("Dossier", StringComparison.OrdinalIgnoreCase) => ConstruireDossier(p),
            _ => ConstruireTexte(p, null)
        };

        Grid.SetColumn(control, 1);
        Grid.SetRow(control, 0);
        grid.Children.Add(control);
        _controles[p.Cle] = control;

        // Tooltip description
        if (!string.IsNullOrWhiteSpace(p.Description))
            ToolTipService.SetToolTip(label, p.Description);

        return grid;
    }

    private TextBox ConstruireTexte(Parametre p, string? placeholder)
    {
        return new TextBox
        {
            Text = p.Valeur ?? string.Empty,
            Height = 32,
            Padding = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = p.Description
        };
    }

    private TextBox ConstruireNombre(Parametre p)
    {
        var tb = ConstruireTexte(p, null);
        tb.TextAlignment = TextAlignment.Right;
        return tb;
    }

    private TextBox ConstruireHeure(Parametre p)
    {
        var tb = ConstruireTexte(p, null);
        tb.Width = 120;
        tb.HorizontalAlignment = HorizontalAlignment.Left;
        return tb;
    }

    private CheckBox ConstruireCheckBox(Parametre p)
    {
        var chk = new CheckBox
        {
            IsChecked = string.Equals(p.Valeur, "true", StringComparison.OrdinalIgnoreCase),
            Content = "Activé",
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = p.Description
        };
        return chk;
    }

    private ComboBox ConstruireComboBox(Parametre p)
    {
        var combo = new ComboBox
        {
            Height = 32,
            ToolTip = p.Description,
            SelectedValuePath = "Content"
        };

        var options = p.Valeur switch
        {
            _ when p.Cle == "Locale.Theme" => new[] { "Sombre", "Clair" },
            _ when p.Cle == "Locale.LangueInterface" => new[] { "FR", "AR" },
            _ when p.Cle == "Export.FormatParDefaut" => new[] { "Excel", "PDF" },
            _ when p.Cle == "Notif.SmtpSecurite" => new[] { "None", "SSL", "STARTTLS" },
            _ when p.Cle == "Notif.Frequence" => new[] { "Quotidien", "Hebdomadaire" },
            _ when p.Cle == "Sauvegarde.Frequence" => new[] { "Quotidien", "Hebdomadaire" },
            _ when p.Cle == "Security.RoleParDefaut" => new[] { "Admin", "RH", "Consultation" },
            _ => new[] { p.Valeur ?? "" }
        };

        combo.ItemsSource = options;
        combo.SelectedItem = p.Valeur ?? string.Empty;
        return combo;
    }

    private PasswordBox ConstruireMotDePasse(Parametre p)
    {
        var pb = new PasswordBox
        {
            Height = 32,
            Width = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
            Password = p.Valeur ?? string.Empty,
            ToolTip = p.Description
        };
        return pb;
    }

    private Grid ConstruireDossier(Parametre p)
    {
        var panel = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tb = ConstruireTexte(p, null);
        Grid.SetColumn(tb, 0);

        var btn = new Button
        {
            Content = "…",
            Width = 32,
            Height = 32,
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = "Parcourir…"
        };
        btn.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Sélectionner un fichier dans le dossier cible",
                Filter = "Tous les fichiers (*.*)|*.*",
                FileName = "dossier"
            };
            if (dlg.ShowDialog() == true)
                tb.Text = System.IO.Path.GetDirectoryName(dlg.FileName) ?? tb.Text;
        };
        Grid.SetColumn(btn, 1);

        panel.Children.Add(tb);
        panel.Children.Add(btn);
        return panel;
    }

    // ---------- Lecture / Enregistrement ----------

    private void BtnEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int modifies = 0;

            foreach (var (clef, controle) in _controles)
            {
                if (_parametresCourants.TryGetValue(clef, out var p))
                {
                    string nouvelleValeur = LireValeur(controle);
                    if (p.Valeur != nouvelleValeur)
                    {
                        _parametres.Set(clef, nouvelleValeur);
                        modifies++;
                    }
                }
            }

            TxtStatut.Text = modifies > 0
                ? $"{modifies} modification(s) enregistrée(s) à {DateTime.Now:HH:mm:ss}."
                : "Aucune modification.";
        }
        catch (Exception ex)
        {
            TxtStatut.Text = "Erreur : " + ex.Message;
        }
    }

    private static string LireValeur(FrameworkElement controle)
    {
        return controle switch
        {
            TextBox tb => tb.Text,
            CheckBox chk => (chk.IsChecked == true ? "true" : "false"),
            ComboBox cb => cb.SelectedItem?.ToString() ?? "",
            PasswordBox pb => pb.Password,
            Grid g => LireValeurGrille(g),
            _ => string.Empty
        };
    }

    private static string LireValeurGrille(Grid grid)
    {
        foreach (var child in grid.Children.OfType<TextBox>())
            return child.Text;
        return string.Empty;
    }

    private void BtnReinitialiser_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Rétablir les valeurs par défaut de la section actuelle ?",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var parDefaut = DatabaseInitializer.ParametresParDefaut
            .Where(d => d.Groupe == _groupeCourant)
            .ToDictionary(d => d.Cle, d => d.Valeur);

        foreach (var clef in parDefaut.Keys)
        {
            _parametres.Set(clef, parDefaut[clef]);
        }

        ChargerParametres(_groupeCourant);
        TxtStatut.Text = $"Valeurs rétablies pour la section « {_groupeCourant} ».";
    }
}
