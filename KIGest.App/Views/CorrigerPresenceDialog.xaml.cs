using System;
using System.Windows;
using System.Windows.Controls;

namespace KIGest.App.Views;

public partial class CorrigerPresenceDialog : Window
{
    public DateTime? EntreeCorrigee { get; private set; }
    public DateTime? SortieCorrigee { get; private set; }
    public string? Motif { get; private set; }
    public bool ResetRequested { get; private set; }

    private readonly DateTime _date;

    public CorrigerPresenceDialog(string titre, DateTime date, DateTime? entree, DateTime? sortie, string? motif)
    {
        InitializeComponent();

        _date = date;

        TxtTitre.Text = "Corriger le pointage";
        TxtDate.Text = titre + " — " + date.ToString("dddd dd MMMM yyyy", new System.Globalization.CultureInfo("fr-FR"));

        ChkEntree.IsChecked = entree.HasValue;
        TxtEntree.Text = entree?.ToString("HH:mm") ?? "";
        ChkSortie.IsChecked = sortie.HasValue;
        TxtSortie.Text = sortie?.ToString("HH:mm") ?? "";
        TxtMotif.Text = motif ?? "";

        Loaded += (_, _) => TxtMotif.Focus();
    }

    private DateTime? LireHeure(TextBox zone, string libelle)
    {
        string texte = (zone.Text ?? "").Trim();

        if (string.IsNullOrEmpty(texte))
            return null;

        if (TimeSpan.TryParse(texte, out var t))
            return _date.Date.Add(t);

        MessageBox.Show($"L'heure de {libelle} « {texte} » est invalide. Format attendu : HH:mm (ex. 08:07).",
            "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
        throw new FormatException($"Heure invalide ({libelle})");
    }

    private void BtnEntree_Click(object sender, RoutedEventArgs e)
    {
        ChkEntree.IsChecked = false;
        TxtEntree.Text = "";
    }

    private void BtnSortie_Click(object sender, RoutedEventArgs e)
    {
        ChkSortie.IsChecked = false;
        TxtSortie.Text = "";
    }

    private void BtnEffacer_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Effacer la correction existante et rétablir les pointages bruts de la pointeuse ?",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            ResetRequested = true;
            DialogResult = true;
        }
    }

    private void BtnEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(TxtMotif.Text))
            {
                MessageBox.Show("Le motif de la correction est obligatoire.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtMotif.Focus();
                return;
            }

            EntreeCorrigee = ChkEntree.IsChecked == true ? LireHeure(TxtEntree, "entrée") : null;
            SortieCorrigee = ChkSortie.IsChecked == true ? LireHeure(TxtSortie, "sortie") : null;
            Motif = TxtMotif.Text.Trim();

            if (EntreeCorrigee == null && SortieCorrigee == null)
            {
                MessageBox.Show("Saisissez au moins une heure d'entrée ou de sortie à corriger.",
                    "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
        }
        catch (FormatException)
        {
            // message déjà affiché
        }
    }
}
