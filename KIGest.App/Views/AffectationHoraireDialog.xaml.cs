using System;
using System.Collections.Generic;
using System.Windows;
using KIGest.Core.Entities;

namespace KIGest.App.Views;

public partial class AffectationHoraireDialog : Window
{
    public int? HoraireId { get; private set; }
    public DateTime? DateDebut { get; private set; }
    public DateTime? DateFin { get; private set; }

    public AffectationHoraireDialog(string cibleNom, List<Horaire> horaires,
        int? horaireActuelId = null, DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        InitializeComponent();
        TxtCible.Text = cibleNom;
        CboHoraire.ItemsSource = horaires;

        if (horaireActuelId.HasValue)
            CboHoraire.SelectedItem = horaires.FirstOrDefault(h => h.Id == horaireActuelId.Value);
        if (dateDebut.HasValue)
            DtpDebut.SelectedDate = dateDebut;
        if (dateFin.HasValue)
            DtpFin.SelectedDate = dateFin;
        if (dateDebut == null)
            DtpDebut.SelectedDate = DateTime.Today;
    }

    private void BtnEffacerFin_Click(object sender, RoutedEventArgs e)
    {
        DtpFin.SelectedDate = null;
    }

    private void BtnEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        if (CboHoraire.SelectedItem is Horaire h)
        {
            HoraireId = h.Id;
        }

        if (DtpDebut.SelectedDate is DateTime debut)
        {
            DateDebut = debut;
        }

        DateFin = DtpFin.SelectedDate;

        if (HoraireId is null)
        {
            MessageBox.Show("Sélectionnez un horaire.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (DateDebut is null)
        {
            MessageBox.Show("Une date de début est obligatoire.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
