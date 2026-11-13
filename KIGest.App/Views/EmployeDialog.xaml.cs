using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Data.Context;

namespace KIGest.App.Views;

public class AttributItem
{
    public int Id { get; set; }
    public string Cle { get; set; } = string.Empty;
    public string? Valeur { get; set; }
}

public partial class EmployeDialog : Window
{
    private readonly KiGestDbContext _ctx;
    private readonly Employe? _employe;
    private List<AttributItem> _attributs = new();

    public EmployeDialog(KiGestDbContext ctx, Employe? employe)
    {
        _ctx = ctx;
        _employe = employe;
        InitializeComponent();

        CboService.ItemsSource = _ctx.Services.OrderBy(s => s.Libelle).ToList();

        if (_employe != null)
        {
            TxtTitre.Text = "Modifier l'employé";
            TxtMatricule.Text = _employe.Matricule;
            TxtNom.Text = _employe.Nom;
            TxtPrenom.Text = _employe.Prenom;
            TxtBadge.Text = _employe.BadgeNumero;
            TxtCin.Text = _employe.CIN ?? "";
            CboActif.IsChecked = _employe.Actif;
            DtpEmbauche.SelectedDate = _employe.DateEmbauche;

            if (_employe.ServiceId.HasValue)
                CboService.SelectedItem = CboService.Items
                    .Cast<Service>()
                    .FirstOrDefault(s => s.Id == _employe.ServiceId.Value);

            // Charger attributs personnalisés
            var list = _ctx.AttributsEmployes
                .Where(a => a.EmployeId == _employe.Id)
                .ToList();
            _attributs = list.Select(a => new AttributItem { Id = a.Id, Cle = a.Cle, Valeur = a.Valeur }).ToList();
        }

        GrdAttributs.ItemsSource = _attributs;
    }

    private void BtnEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNom.Text) || string.IsNullOrWhiteSpace(TxtPrenom.Text))
        {
            MessageBox.Show("Le nom et le prénom sont obligatoires.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var employe = _employe ?? new Employe();

            employe.Matricule = TxtMatricule.Text.Trim();
            employe.Nom = TxtNom.Text.Trim();
            employe.Prenom = TxtPrenom.Text.Trim();
            employe.BadgeNumero = TxtBadge.Text.Trim();
            employe.CIN = string.IsNullOrWhiteSpace(TxtCin.Text) ? null : TxtCin.Text.Trim();
            employe.ServiceId = (CboService.SelectedItem as Service)?.Id;
            employe.DateEmbauche = DtpEmbauche.SelectedDate;
            employe.Actif = CboActif.IsChecked == true;

            if (_employe == null)
                _ctx.Employes.Add(employe);

            _ctx.SaveChanges();

            // Sauvegarder les attributs personnalisés
            var employeId = employe.Id;
            var existants = _ctx.AttributsEmployes.Where(a => a.EmployeId == employeId).ToList();
            var clesEnGrid = _attributs.Select(a => a.Cle.Trim()).ToList();

            // Supprimer les attributs qui ne sont plus présents dans la grille
            foreach (var a in existants)
            {
                if (!clesEnGrid.Contains(a.Cle))
                    _ctx.AttributsEmployes.Remove(a);
            }

            // Ajouter / mettre à jour depuis la grille
            foreach (var attr in _attributs)
            {
                string clef = attr.Cle.Trim();
                if (string.IsNullOrWhiteSpace(clef)) continue;

                var existante = _ctx.AttributsEmployes.FirstOrDefault(a => a.EmployeId == employeId && a.Cle == clef);
                if (existante == null)
                {
                    _ctx.AttributsEmployes.Add(new AttributEmploye { EmployeId = employeId, Cle = clef, Valeur = attr.Valeur });
                }
                else
                {
                    existante.Valeur = attr.Valeur;
                }
            }

            _ctx.SaveChanges();

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur d'enregistrement : " + ex.Message, "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAjouterAttribut_Click(object sender, RoutedEventArgs e)
    {
        _attributs.Add(new AttributItem { Cle = "", Valeur = "" });
        GrdAttributs.Items.Refresh();
    }

    private void BtnAnnuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
