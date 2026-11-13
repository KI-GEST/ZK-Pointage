using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using KIGest.App.Services;
using KIGest.Core.Entities;
using KIGest.Data.Context;
using KIGest.Data.Services;

namespace KIGest.App.Views;

public partial class SauvegardeView : Page
{
    private class SauvegardeRow
    {
        public string Nom { get; set; } = string.Empty;
        public string CheminComplet { get; set; } = string.Empty;
        public DateTime DateCreation { get; set; }
        public long TailleOctets { get; set; }
        public string TailleAffichee => TailleOctets >= 1024 * 1024
            ? $"{TailleOctets / (1024.0 * 1024):0.0} Mo"
            : $"{TailleOctets / 1024.0:0.0} Ko";
    }

    private const string DossierParDefaut = @"C:\KI-GEST Pointage\Backups";

    private readonly KiGestDbContext _ctx;
    private readonly ParametresService _parametres;
    private readonly string _connectionString;

    public SauvegardeView(KiGestDbContext ctx, ParametresService parametres)
    {
        _ctx = ctx;
        _parametres = parametres;
        InitializeComponent();

        _connectionString = SqlConfig.ConnectionString
            ?? throw new InvalidOperationException("Chaîne de connexion SQL Server introuvable (appsettings.json / KIGest.json).");

        TxtDossier.Text = _parametres.Get("Sauvegarde.DossierDefaut", DossierParDefaut);
        ChargerListe();
    }

    private string MasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master", Pooling = false };
        return builder.ConnectionString;
    }

    private void ChargerListe()
    {
        string dossier = TxtDossier.Text.Trim();
        var lignes = new List<SauvegardeRow>();

        try
        {
            if (Directory.Exists(dossier))
            {
                lignes = Directory.GetFiles(dossier, "*.bak")
                    .Select(f =>
                    {
                        var info = new FileInfo(f);
                        return new SauvegardeRow
                        {
                            Nom = info.Name,
                            CheminComplet = f,
                            DateCreation = info.LastWriteTime,
                            TailleOctets = info.Length
                        };
                    })
                    .OrderByDescending(r => r.DateCreation)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Impossible de lire le dossier : " + ex.Message);
        }

        GrdSauvegardes.ItemsSource = lignes;
    }

    private void GrdSauvegardes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnSupprimerBak.IsEnabled = GrdSauvegardes.SelectedItem != null;
    }

    private void BtnActualiser_Click(object sender, RoutedEventArgs e) => ChargerListe();

    private void BtnParcourir_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("Dossier de sauvegarde (ex. C:\\KI-GEST Pointage\\Backups)", TxtDossier.Text)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Valeur))
        {
            TxtDossier.Text = dialog.Valeur.Trim();
            _parametres.Set("Sauvegarde.DossierDefaut", TxtDossier.Text);
            ChargerListe();
        }
    }

    // ==================== Sauvegarde ====================

    private void BtnSauvegarder_Click(object sender, RoutedEventArgs e)
    {
        string dossier = TxtDossier.Text.Trim();
        if (string.IsNullOrWhiteSpace(dossier))
        {
            MessageBox.Show("Veuillez saisir un dossier de sauvegarde.");
            return;
        }

        try
        {
            Directory.CreateDirectory(dossier);
            _parametres.Set("Sauvegarde.DossierDefaut", dossier);

            string nom = $"POINTAGE_backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            string chemin = System.IO.Path.Combine(dossier, nom);

            BtnSauvegarder.IsEnabled = false;
            Sauvegarder(chemin);

            _ctx.Journaux.Add(new Journal { DateHeure = DateTime.Now, Action = "SAUVEGARDE", Details = $"Sauvegarde créée : {chemin}" });
            _ctx.SaveChanges();

            ChargerListe();
            MessageBox.Show("Sauvegarde terminée :\n" + chemin, "Sauvegarde", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Échec de la sauvegarde :\n" + ex.Message, "Sauvegarde", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSauvegarder.IsEnabled = true;
        }
    }

    private void Sauvegarder(string chemin)
    {
        // sqlcmd-style backup : BACKUP DATABASE [POINTAGE] TO DISK = '...' WITH FORMAT, INIT, COMPRESSION, CHECKSUM
        string sql = $"BACKUP DATABASE [POINTAGE] TO DISK = @chemin WITH FORMAT, INIT, COMPRESSION, CHECKSUM;";

        using var conn = new SqlConnection(MasterConnectionString());
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
        cmd.Parameters.Add("@chemin", SqlDbType.NVarChar, 260).Value = chemin;
        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (SqlException ex) when (ex.Message.Contains("COMPRESSION", StringComparison.OrdinalIgnoreCase))
        {
            // COMPRESSION non supportée sur cette édition : nouvel essai sans compression
            using var cmd2 = new SqlCommand($"BACKUP DATABASE [POINTAGE] TO DISK = @chemin WITH FORMAT, INIT, CHECKSUM;", conn) { CommandTimeout = 300 };
            cmd2.Parameters.Add("@chemin", SqlDbType.NVarChar, 260).Value = chemin;
            cmd2.ExecuteNonQuery();
        }
    }

    // ==================== Restauration ====================

    private void BtnRestaurer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Sauvegarde SQL Server|*.bak", Title = "Choisir le fichier .bak à restaurer" };
        if (dialog.ShowDialog() != true) return;

        string fichier = dialog.FileName;
        if (!File.Exists(fichier))
        {
            MessageBox.Show("Fichier introuvable.");
            return;
        }

        var confirmation = MessageBox.Show(
            $"Restaurer la base POINTAGE depuis :\n{fichier}\n\nTOUTES les données actuelles seront ÉCRASÉES. Cette opération est irréversible.\n\nContinuer ?",
            "Restauration", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            // Fermer les connexions de l'application sinon la restauration échoue
            SqlConnection.ClearAllPools();
            Restaurer(MasterConnectionString(), fichier);

            try
            {
                _ctx.Journaux.Add(new Journal { DateHeure = DateTime.Now, Action = "RESTAURATION", Details = $"Base restaurée depuis : {fichier}" });
                _ctx.SaveChanges();
            }
            catch { /* journal non bloquant après restauration */ }

            MessageBox.Show("Restauration terminée avec succès.\nVeuillez REDÉMARRER l'application.", "Restauration", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Échec de la restauration :\n" + ex.Message +
                "\n\nLa restauration nécessite un compte SQL avec le droit CREATE DATABASE (sysadmin). " +
                "Ouvrez SSMS, connectez-vous en sa et faites RESTORE DATABASE POINTAGE FROM DISK = N'" + fichier + "' WITH REPLACE.",
                "Restauration", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void Restaurer(string masterConnectionString, string fichier)
    {
        using var conn = new SqlConnection(masterConnectionString);
        conn.Open();

        // 1. Forcer la déconnexion de tous les utilisateurs
        Exec(conn, "ALTER DATABASE [POINTAGE] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;");
        try
        {
            // 2. Restaurer
            Exec(conn, $"RESTORE DATABASE [POINTAGE] FROM DISK = @fichier WITH REPLACE, RECOVERY;",
                ("@fichier", SqlDbType.NVarChar, fichier));
        }
        finally
        {
            // 3. Remettre en multi-utilisateurs dans tous les cas
            try { Exec(conn, "ALTER DATABASE [POINTAGE] SET MULTI_USER;"); } catch { /* DB peut être restée en SINGLE_USER : à corriger en SSMS */ }
        }
    }

    private static void Exec(SqlConnection conn, string sql, params (string Name, SqlDbType Type, object Value)[] parameters)
    {
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 600 };
        foreach (var (name, type, value) in parameters)
            cmd.Parameters.Add(name, type, 260).Value = value;
        cmd.ExecuteNonQuery();
    }

    // ==================== Suppression d'un fichier de sauvegarde ====================

    private void BtnSupprimerBak_Click(object sender, RoutedEventArgs e)
    {
        if (GrdSauvegardes.SelectedItem is not SauvegardeRow row) return;

        if (MessageBox.Show($"Supprimer le fichier {row.Nom} ?", "Sauvegarde",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            File.Delete(row.CheminComplet);
            ChargerListe();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Suppression impossible : " + ex.Message);
        }
    }
}
