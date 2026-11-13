using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using KIGest.App.Services;
using KIGest.Core.Services;
using KIGest.Data.Context;
using KIGest.Data.Services;

namespace KIGest.App.Views;

public partial class RapportsView : Page
{
    private class LigneJournalier
    {
        public string Matricule { get; set; } = "";
        public string Nom { get; set; } = "";
        public string Prenom { get; set; } = "";
        public string Service { get; set; } = "";
        public string Entree { get; set; } = "";
        public string Sortie { get; set; } = "";
        public double HeuresTravaillees { get; set; }
        public int RetardMin { get; set; }
        public int DepartAnticipeMin { get; set; }
        public double HeuresSup { get; set; }
        public string Statut { get; set; } = "";
    }

    private class LigneMensuel
    {
        public string Matricule { get; set; } = "";
        public string Nom { get; set; } = "";
        public string Prenom { get; set; } = "";
        public string Service { get; set; } = "";
        public int JoursPresents { get; set; }
        public int Absences { get; set; }
        public int Retards { get; set; }
        public int TotalRetardMin { get; set; }
        public double HeuresTravaillees { get; set; }
        public double HeuresSup { get; set; }
        public int Conges { get; set; }
    }

    private class LigneService
    {
        public string Service { get; set; } = "";
        public int Effectif { get; set; }
        public int JoursPresents { get; set; }
        public int Absences { get; set; }
        public int Retards { get; set; }
        public int TotalRetardMin { get; set; }
        public double HeuresTravaillees { get; set; }
        public double HeuresSup { get; set; }
    }

    private class LignePaie
    {
        public string Matricule { get; set; } = "";
        public string Nom { get; set; } = "";
        public string Prenom { get; set; } = "";
        public string Service { get; set; } = "";
        public int JoursPresents { get; set; }
        public double HeuresNormales { get; set; }
        public double Sup125 { get; set; }
        public double Sup150 { get; set; }
        public double Sup200 { get; set; }
        public double TotalSup => Math.Round(Sup125 + Sup150 + Sup200, 2);
        public int Absences { get; set; }
        public int Conges { get; set; }
    }

    private readonly KiGestDbContext _ctx;
    private readonly ParametresService _parametres;

    private List<LigneJournalier> _jourData = new();
    private List<LigneMensuel> _mensuelData = new();
    private List<LigneService> _serviceData = new();
    private List<LignePaie> _paieData = new();

    public RapportsView(KiGestDbContext ctx, ParametresService parametres)
    {
        _ctx = ctx;
        _parametres = parametres;
        InitializeComponent();

        DtpJournalier.SelectedDate = DateTime.Today;
        DtpMensuel.SelectedDate = DateTime.Today;
        DtpService.SelectedDate = DateTime.Today;
        DtpPaie.SelectedDate = DateTime.Today;

        var services = _ctx.Services.AsNoTracking().OrderBy(s => s.Libelle).ToList();
        CboServiceJour.ItemsSource = services;
        CboServiceMensuel.ItemsSource = _ctx.Services.AsNoTracking().OrderBy(s => s.Libelle).ToList();
        CboServiceJour.SelectedIndex = -1;
        CboServiceMensuel.SelectedIndex = -1;
    }

    private Dictionary<int, string> ServicesParId() =>
        _ctx.Services.AsNoTracking().ToDictionary(s => s.Id, s => s.Libelle);

    private int? ServiceSelectionne(ComboBox cbo) =>
        (cbo.SelectedItem as Core.Entities.Service)?.Id;

    private static bool EstTravaille(PresenceCalculee p) =>
        p.Statut == StatutPresence.Present || p.Statut == StatutPresence.Retard;

    // ==================== Journalier ====================

    private void BtnGenererJournalier_Click(object sender, RoutedEventArgs e)
    {
        var date = (DtpJournalier.SelectedDate ?? DateTime.Today).Date;
        var fin = date.AddDays(1);
        var services = ServicesParId();

        var r = PresencePeriode.Calculer(_ctx, _parametres, date, fin, ServiceSelectionne(CboServiceJour), null);

        _jourData = r.Resultats
            .OrderBy(x => x.Date).ToList()
            .Select(x =>
            {
                var emp = r.Employes.First(e => e.Id == x.EmployeId);
                services.TryGetValue(emp.ServiceId ?? -1, out var serviceLibelle);
                return new LigneJournalier
                {
                    Matricule = emp.Matricule,
                    Nom = emp.Nom,
                    Prenom = emp.Prenom,
                    Service = serviceLibelle ?? "—",
                    Entree = x.Entree?.ToString("HH:mm") ?? "",
                    Sortie = x.Sortie?.ToString("HH:mm") ?? "",
                    HeuresTravaillees = Math.Round(x.HeuresTravaillees, 2),
                    RetardMin = x.RetardMin,
                    DepartAnticipeMin = x.DepartAnticipeMin,
                    HeuresSup = Math.Round(x.HeuresSup, 2),
                    Statut = PresencePeriode.StatutFr(x.Statut)
                };
            })
            .OrderBy(l => l.Nom).ThenBy(l => l.Prenom)
            .ToList();

        GrdJournalier.ItemsSource = _jourData;
        BtnExcelJournalier.IsEnabled = _jourData.Count > 0;
        BtnPdfJournalier.IsEnabled = _jourData.Count > 0;
    }

    // ==================== Mensuel par employé ====================

    private PresencePeriodeResult CalculerMois(DateTime mois)
    {
        var debut = new DateTime(mois.Year, mois.Month, 1);
        var fin = debut.AddMonths(1);
        return PresencePeriode.Calculer(_ctx, _parametres, debut, fin, ServiceSelectionne(CboServiceMensuel), null);
    }

    private void BtnGenererMensuel_Click(object sender, RoutedEventArgs e)
    {
        var mois = DtpMensuel.SelectedDate ?? DateTime.Today;
        var services = ServicesParId();
        var r = CalculerMois(mois);

        _mensuelData = new List<LigneMensuel>();
        foreach (var emp in r.Employes)
        {
            var jours = r.Resultats.Where(x => x.EmployeId == emp.Id).ToList();
            if (jours.Count == 0) continue;

            services.TryGetValue(emp.ServiceId ?? -1, out var serviceLibelle);
            _mensuelData.Add(new LigneMensuel
            {
                Matricule = emp.Matricule,
                Nom = emp.Nom,
                Prenom = emp.Prenom,
                Service = serviceLibelle ?? "—",
                JoursPresents = jours.Count(j => EstTravaille(j)),
                Absences = jours.Count(j => j.Statut == StatutPresence.Absent),
                Retards = jours.Count(j => j.RetardMin > 0),
                TotalRetardMin = jours.Sum(j => j.RetardMin),
                HeuresTravaillees = Math.Round(jours.Sum(j => j.HeuresTravaillees), 2),
                HeuresSup = Math.Round(jours.Sum(j => j.HeuresSup), 2),
                Conges = jours.Count(j => j.Statut == StatutPresence.Conge)
            });
        }

        _mensuelData = _mensuelData.OrderBy(l => l.Nom).ThenBy(l => l.Prenom).ToList();
        GrdMensuel.ItemsSource = _mensuelData;
        BtnExcelMensuel.IsEnabled = _mensuelData.Count > 0;
        BtnPdfMensuel.IsEnabled = _mensuelData.Count > 0;
    }

    // ==================== Mensuel par service ====================

    private void BtnGenererService_Click(object sender, RoutedEventArgs e)
    {
        var mois = DtpService.SelectedDate ?? DateTime.Today;
        var debut = new DateTime(mois.Year, mois.Month, 1);
        var fin = debut.AddMonths(1);
        var services = ServicesParId();

        var r = PresencePeriode.Calculer(_ctx, _parametres, debut, fin, null, null);

        var groupes = r.Employes
            .GroupBy(emp => emp.ServiceId ?? -1)
            .Select(g =>
            {
                var jours = r.Resultats.Where(x => g.Any(emp => emp.Id == x.EmployeId)).ToList();
                return new LigneService
                {
                    Service = services.TryGetValue(g.Key, out var lib) ? lib : (g.Key == -1 ? "(sans service)" : $"Service #{g.Key}"),
                    Effectif = g.Count(),
                    JoursPresents = jours.Count(j => EstTravaille(j)),
                    Absences = jours.Count(j => j.Statut == StatutPresence.Absent),
                    Retards = jours.Count(j => j.RetardMin > 0),
                    TotalRetardMin = jours.Sum(j => j.RetardMin),
                    HeuresTravaillees = Math.Round(jours.Sum(j => j.HeuresTravaillees), 2),
                    HeuresSup = Math.Round(jours.Sum(j => j.HeuresSup), 2)
                };
            })
            .OrderBy(l => l.Service)
            .ToList();

        _serviceData = groupes;
        GrdService.ItemsSource = _serviceData;
        BtnExcelService.IsEnabled = _serviceData.Count > 0;
        BtnPdfService.IsEnabled = _serviceData.Count > 0;
    }

    // ==================== Export paie (H. sup) ====================

    private void BtnGenererPaie_Click(object sender, RoutedEventArgs e)
    {
        var mois = DtpPaie.SelectedDate ?? DateTime.Today;
        var debut = new DateTime(mois.Year, mois.Month, 1);
        var fin = debut.AddMonths(1);
        var services = ServicesParId();

        // Seuil journalier en heures (les taux 125/150/200 % sont appliqués par la paie)
        double seuilJour = _parametres.GetDouble("HeuresSup.SeuilJour", 8);

        var horaires = _ctx.Horaires.AsNoTracking().ToList();
        var affectations = _ctx.AffectationsHoraires.AsNoTracking()
            .Where(a => a.DateDebut < fin && (a.DateFin == null || a.DateFin >= debut)).ToList();
        var affectationsService = _ctx.AffectationsServiceHoraires.AsNoTracking()
            .Where(a => a.DateDebut < fin && (a.DateFin == null || a.DateFin >= debut)).ToList();
        string horaireParDefautLibelle = _parametres.Get("Pointage.HoraireParDefaut", "");
        var horaireParDefaut = !string.IsNullOrWhiteSpace(horaireParDefautLibelle)
            ? horaires.FirstOrDefault(h => h.Libelle == horaireParDefautLibelle) : null;

        var r = PresencePeriode.Calculer(_ctx, _parametres, debut, fin, null, null);

        Core.Entities.Horaire? HoraireDuJour(Core.Entities.Employe emp, DateTime date)
        {
            var aff = affectations.FirstOrDefault(a => a.EmployeId == emp.Id && a.DateDebut <= date && (a.DateFin == null || a.DateFin >= date));
            if (aff != null) return horaires.FirstOrDefault(h => h.Id == aff.HoraireId);
            if (emp.ServiceId.HasValue)
            {
                var affSrv = affectationsService.Where(a => a.ServiceId == emp.ServiceId.Value && a.DateDebut <= date && (a.DateFin == null || a.DateFin >= date))
                    .OrderByDescending(a => a.DateDebut).FirstOrDefault();
                if (affSrv != null) return horaires.FirstOrDefault(h => h.Id == affSrv.HoraireId);
            }
            return horaireParDefaut;
        }

        _paieData = new List<LignePaie>();
        foreach (var emp in r.Employes)
        {
            var jours = r.Resultats.Where(x => x.EmployeId == emp.Id).ToList();
            if (jours.Count == 0) continue;

            double normales = 0, sup125 = 0, sup150 = 0, sup200 = 0;
            foreach (var jour in jours)
            {
                if (jour.HeuresTravaillees <= 0) continue;

                double h = jour.HeuresTravaillees;
                double supJour = Math.Max(0, h - seuilJour);

                if (supJour > 0)
                {
                    // Classement du jour : férié/repos > nuit > normal
                    // (colonnes = heures sup classées par taux applicable ; le taux lui-même est appliqué en paie)
                    bool ferieTravaille = jour.Statut == StatutPresence.Ferie || jour.Statut == StatutPresence.Repos;
                    bool nuit = HoraireDuJour(emp, jour.Date)?.NuitOuiNon == true;

                    if (ferieTravaille)
                        sup200 += supJour;
                    else if (nuit)
                        sup150 += supJour;
                    else
                        sup125 += supJour;

                    normales += h - supJour;
                }
                else
                {
                    normales += h;
                }
            }

            services.TryGetValue(emp.ServiceId ?? -1, out var serviceLibelle);
            _paieData.Add(new LignePaie
            {
                Matricule = emp.Matricule,
                Nom = emp.Nom,
                Prenom = emp.Prenom,
                Service = serviceLibelle ?? "—",
                JoursPresents = jours.Count(j => EstTravaille(j)),
                HeuresNormales = Math.Round(normales, 2),
                Sup125 = Math.Round(sup125, 2),
                Sup150 = Math.Round(sup150, 2),
                Sup200 = Math.Round(sup200, 2),
                Absences = jours.Count(j => j.Statut == StatutPresence.Absent),
                Conges = jours.Count(j => j.Statut == StatutPresence.Conge)
            });
        }

        _paieData = _paieData.OrderBy(l => l.Nom).ThenBy(l => l.Prenom).ToList();
        GrdPaie.ItemsSource = _paieData;
        BtnExcelPaie.IsEnabled = _paieData.Count > 0;
        BtnPdfPaie.IsEnabled = _paieData.Count > 0;
    }

    // ==================== Exports ====================

    private static bool? PickSaveFile(string filtre, string nomParDefaut, out string chemin)
    {
        var dialog = new SaveFileDialog { Filter = filtre, FileName = nomParDefaut, AddExtension = true };
        bool result = dialog.ShowDialog() == true;
        chemin = dialog.FileName;
        return result;
    }

    private void BtnExcelJournalier_Click(object sender, RoutedEventArgs e)
    {
        if (PickSaveFile("Fichier Excel|*.xlsx", $"Journalier_{DateTime.Today:yyyyMMdd}.xlsx", out var path) != true) return;
        var entetes = new[] { "Matricule", "Nom", "Prénom", "Service", "Entrée", "Sortie", "H. trav.", "Retard (min)", "Dép. anticipé (min)", "H. sup", "Statut" };
        var lignes = _jourData.Select(l => (IReadOnlyList<object?>)new object?[]
            { l.Matricule, l.Nom, l.Prenom, l.Service, l.Entree, l.Sortie, l.HeuresTravaillees, l.RetardMin, l.DepartAnticipeMin, l.HeuresSup, l.Statut }).ToList();
        ExportHelper.ExportExcel("Rapport journalier", $"Date : {(DtpJournalier.SelectedDate ?? DateTime.Today):dd/MM/yyyy}", entetes, lignes, path);
        MessageBox.Show("Export terminé : " + path, "Rapports", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnPdfJournalier_Click(object sender, RoutedEventArgs e)
    {
        if (PickSaveFile("Fichier PDF|*.pdf", $"Journalier_{DateTime.Today:yyyyMMdd}.pdf", out var path) != true) return;
        var entetes = new[] { "Matricule", "Nom", "Prénom", "Entree", "Sortie", "H. trav.", "Retard", "Dép. ant.", "H. sup", "Statut" };
        var lignes = _jourData.Select(l => (IReadOnlyList<object?>)new object?[]
            { l.Matricule, l.Nom, l.Prenom, l.Entree, l.Sortie, l.HeuresTravaillees, l.RetardMin, l.DepartAnticipeMin, l.HeuresSup, l.Statut }).ToList();
        ExportHelper.ExportPdf("Rapport journalier", $"Date : {(DtpJournalier.SelectedDate ?? DateTime.Today):dd/MM/yyyy}", entetes, lignes, path, paysage: true);
        MessageBox.Show("Export terminé : " + path, "Rapports", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnExcelMensuel_Click(object sender, RoutedEventArgs e)
    {
        if (PickSaveFile("Fichier Excel|*.xlsx", $"Mensuel_{DateTime.Today:yyyyMM}.xlsx", out var path) != true) return;
        var mois = DtpMensuel.SelectedDate ?? DateTime.Today;
        var entetes = new[] { "Matricule", "Nom", "Prénom", "Service", "J. présents", "Absences", "Retards", "Min. retard", "H. trav.", "H. sup", "Congés" };
        var lignes = _mensuelData.Select(l => (IReadOnlyList<object?>)new object?[]
            { l.Matricule, l.Nom, l.Prenom, l.Service, l.JoursPresents, l.Absences, l.Retards, l.TotalRetardMin, l.HeuresTravaillees, l.HeuresSup, l.Conges }).ToList();
        ExportHelper.ExportExcel("Rapport mensuel (par employé)", MoisLibelle(mois), entetes, lignes, path);
        MessageBox.Show("Export terminé : " + path, "Rapports", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnPdfMensuel_Click(object sender, RoutedEventArgs e)
    {
        if (PickSaveFile("Fichier PDF|*.pdf", $"Mensuel_{DateTime.Today:yyyyMM}.pdf", out var path) != true) return;
        var mois = DtpMensuel.SelectedDate ?? DateTime.Today;
        var entetes = new[] { "Matricule", "Nom", "Prénom", "J. pré.", "Abs.", "Ret.", "Min retard", "H. trav.", "H. sup", "Congés" };
        var lignes = _mensuelData.Select(l => (IReadOnlyList<object?>)new object?[]
            { l.Matricule, l.Nom, l.Prenom, l.JoursPresents, l.Absences, l.Retards, l.TotalRetardMin, l.HeuresTravaillees, l.HeuresSup, l.Conges }).ToList();
        ExportHelper.ExportPdf("Rapport mensuel (par employé)", MoisLibelle(mois), entetes, lignes, path, paysage: true);
        MessageBox.Show("Export terminé : " + path, "Rapports", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnExcelService_Click(object sender, RoutedEventArgs e)
    {
        if (PickSaveFile("Fichier Excel|*.xlsx", $"ParService_{DateTime.Today:yyyyMM}.xlsx", out var path) != true) return;
        var mois = DtpService.SelectedDate ?? DateTime.Today;
        var entetes = new[] { "Service", "Effectif", "J. présence", "Absences", "Retards", "Min. retard", "H. trav.", "H. sup" };
        var lignes = _serviceData.Select(l => (IReadOnlyList<object?>)new object?[]
            { l.Service, l.Effectif, l.JoursPresents, l.Absences, l.Retards, l.TotalRetardMin, l.HeuresTravaillees, l.HeuresSup }).ToList();
        ExportHelper.ExportExcel("Rapport mensuel (par service)", MoisLibelle(mois), entetes, lignes, path);
        MessageBox.Show("Export terminé : " + path, "Rapports", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnPdfService_Click(object sender, RoutedEventArgs e)
    {
        if (PickSaveFile("Fichier PDF|*.pdf", $"ParService_{DateTime.Today:yyyyMM}.pdf", out var path) != true) return;
        var mois = DtpService.SelectedDate ?? DateTime.Today;
        var entetes = new[] { "Service", "Effectif", "J. présence", "Absences", "Retards", "Min. retard", "H. trav.", "H. sup" };
        var lignes = _serviceData.Select(l => (IReadOnlyList<object?>)new object?[]
            { l.Service, l.Effectif, l.JoursPresents, l.Absences, l.Retards, l.TotalRetardMin, l.HeuresTravaillees, l.HeuresSup }).ToList();
        ExportHelper.ExportPdf("Rapport mensuel (par service)", MoisLibelle(mois), entetes, lignes, path, paysage: true);
        MessageBox.Show("Export terminé : " + path, "Rapports", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnExcelPaie_Click(object sender, RoutedEventArgs e)
    {
        if (PickSaveFile("Fichier Excel|*.xlsx", $"ExportPaie_{DateTime.Today:yyyyMM}.xlsx", out var path) != true) return;
        var mois = DtpPaie.SelectedDate ?? DateTime.Today;
        var entetes = new[] { "Matricule", "Nom", "Prénom", "Service", "J. présence", "H. normales", "H. sup 125 %", "H. sup 150 %", "H. sup 200 %", "Total H. sup", "Absences", "Congés" };
        var lignes = _paieData.Select(l => (IReadOnlyList<object?>)new object?[]
            { l.Matricule, l.Nom, l.Prenom, l.Service, l.JoursPresents, l.HeuresNormales, l.Sup125, l.Sup150, l.Sup200, l.TotalSup, l.Absences, l.Conges }).ToList();
        ExportHelper.ExportExcel("Export paie — heures supplémentaires", MoisLibelle(mois), entetes, lignes, path);
        MessageBox.Show("Export terminé : " + path, "Rapports", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnPdfPaie_Click(object sender, RoutedEventArgs e)
    {
        if (PickSaveFile("Fichier PDF|*.pdf", $"ExportPaie_{DateTime.Today:yyyyMM}.pdf", out var path) != true) return;
        var mois = DtpPaie.SelectedDate ?? DateTime.Today;
        var entetes = new[] { "Matricule", "Nom", "H. norm.", "125 %", "150 %", "200 %", "Total sup", "Abs.", "Congés" };
        var lignes = _paieData.Select(l => (IReadOnlyList<object?>)new object?[]
            { l.Matricule, l.Nom, l.HeuresNormales, l.Sup125, l.Sup150, l.Sup200, l.TotalSup, l.Absences, l.Conges }).ToList();
        ExportHelper.ExportPdf("Export paie — heures supplémentaires", MoisLibelle(mois), entetes, lignes, path, paysage: true);
        MessageBox.Show("Export terminé : " + path, "Rapports", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string MoisLibelle(DateTime mois) =>
        new DateTime(mois.Year, mois.Month, 1).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"));
}
