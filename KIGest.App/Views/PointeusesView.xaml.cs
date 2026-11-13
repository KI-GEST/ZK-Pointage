using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;
using KIGest.Data.Context;
using KIGest.Device.Services;

namespace KIGest.App.Views;

public partial class PointeusesView : Page
{
    private readonly KiGestDbContext _ctx;
    private readonly List<Pointeuse> _pointeuses = new();
    private ZkDevice? _device;
    private Pointeuse? _courante;

    public PointeusesView(KiGestDbContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();
        ChargerPointeuses();
    }

    private void ChargerPointeuses()
    {
        _pointeuses.Clear();
        _pointeuses.AddRange(_ctx.Pointeuses.OrderBy(p => p.Nom).ToList());
        LstPointeuses.ItemsSource = null;
        LstPointeuses.ItemsSource = _pointeuses;
        LstPointeuses.DisplayMemberPath = "Nom";
    }

    private void Log(string message)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zk_debug.log");
            File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message + Environment.NewLine);
        }
        catch { }
    }

    private void BtnAjouter_Click(object sender, RoutedEventArgs e)
    {
        var item = new Pointeuse
        {
            Nom = "Nouvelle pointeuse",
            AdresseIP = "192.168.0.40",
            Port = 4370,
            CleCom = 0,
            NumeroMachine = 1,
            Actif = true
        };
        _ctx.Pointeuses.Add(item);
        _ctx.SaveChanges();

        _pointeuses.Add(item);
        LstPointeuses.ItemsSource = null;
        LstPointeuses.ItemsSource = _pointeuses;
        LstPointeuses.SelectedItem = item;
    }

    private void BtnSupprimer_Click(object sender, RoutedEventArgs e)
    {
        if (LstPointeuses.SelectedItem is not Pointeuse item) return;

        if (MessageBox.Show($"Supprimer la pointeuse « {item.Nom} » ?", "KI-GEST Pointage",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _ctx.Pointeuses.Remove(item);
        _ctx.SaveChanges();

        _pointeuses.Remove(item);
        LstPointeuses.ItemsSource = null;
        LstPointeuses.ItemsSource = _pointeuses;
        ViderFormulaire();
    }

    private void BtnEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        if (LstPointeuses.SelectedItem is not Pointeuse item) return;

        string ip = TxtIp.Text.Trim();
        if (string.IsNullOrWhiteSpace(ip))
        {
            MessageBox.Show("L'adresse IP est obligatoire.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_ctx.Pointeuses.Any(p => p.AdresseIP == ip && p.Id != item.Id))
        {
            MessageBox.Show($"Une autre pointeuse utilise déjà l'adresse {ip}.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        item.Nom = string.IsNullOrWhiteSpace(TxtNom.Text) ? ip : TxtNom.Text.Trim();
        item.AdresseIP = ip;
        int.TryParse(TxtPort.Text.Trim(), out int port);
        int.TryParse(TxtCommKey.Text.Trim(), out int cle);
        int.TryParse(TxtMachineNo.Text.Trim(), out int machine);
        item.Port = port == 0 ? 4370 : port;
        item.CleCom = cle;
        item.NumeroMachine = machine == 0 ? 1 : machine;

        try
        {
            _ctx.SaveChanges();
            LstPointeuses.ItemsSource = null;
            LstPointeuses.ItemsSource = _pointeuses;
            LstPointeuses.SelectedItem = item;
            TxtStatus.Text = "Enregistré ✓";
            TxtStatus.Foreground = (SolidColorBrush)FindResource("Brush.Status.Present");
            TxtStatusDetail.Text = "Pointeuse enregistrée dans la base POINTAGE.";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur d'enregistrement : " + ex.Message, "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Error);
            Log("EXCEPTION save: " + ex.Message);
        }
    }

    private void LstPointeuses_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstPointeuses.SelectedItem is Pointeuse item)
        {
            _courante = item;
            TxtNom.Text = item.Nom;
            TxtIp.Text = item.AdresseIP;
            TxtPort.Text = item.Port.ToString();
            TxtCommKey.Text = item.CleCom.ToString();
            TxtMachineNo.Text = item.NumeroMachine.ToString();
            TxtStatus.Text = "Non testé";
            TxtStatusDetail.Text = "";
            TxtStatus.Foreground = (SolidColorBrush)FindResource("Brush.MutedForeground");
            BtnDisconnect.IsEnabled = false;
        }
        else
        {
            _courante = null;
        }
    }

    private void ViderFormulaire()
    {
        TxtNom.Text = "";
        TxtIp.Text = "";
        TxtPort.Text = "4370";
        TxtCommKey.Text = "0";
        TxtMachineNo.Text = "1";
        TxtStatus.Text = "Non testé";
        TxtStatusDetail.Text = "";
        TxtStatus.Foreground = (SolidColorBrush)FindResource("Brush.MutedForeground");
    }

    private void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnTest.IsEnabled = false;
            TxtStatus.Text = "Test en cours...";
            TxtStatusDetail.Text = "Connexion à la pointeuse...";
            TxtStatus.Foreground = (SolidColorBrush)FindResource("Brush.MutedForeground");

            string ip = TxtIp.Text.Trim();
            if (!int.TryParse(TxtPort.Text.Trim(), out int port)) port = 4370;
            if (!int.TryParse(TxtCommKey.Text.Trim(), out int commKey)) commKey = 0;
            if (!int.TryParse(TxtMachineNo.Text.Trim(), out int machineNo)) machineNo = 1;

            _device?.Dispose();
            _device = new ZkDevice();

            bool connected = _device.Connect(ip, port, commKey);

            if (connected)
            {
                TxtStatus.Text = "Connexion établie ✓";
                TxtStatus.Foreground = (SolidColorBrush)FindResource("Brush.Status.Present");
                TxtStatusDetail.Text = $"Connecté à {ip}:{port} (N° machine: {machineNo})";
                Log("SUCCESS: Connected to " + ip + ":" + port);
                BtnDisconnect.IsEnabled = true;

                if (_courante != null)
                {
                    _courante.DerniereSynchro = DateTime.Now;
                    _ctx.SaveChanges();
                }
            }
            else
            {
                TxtStatus.Text = "Connexion échouée ✗";
                TxtStatus.Foreground = (SolidColorBrush)FindResource("Brush.Status.Absent");
                TxtStatusDetail.Text = "Vérifiez l'adresse IP, le port 4370, le pare-feu et la clé de communication.";
                Log("FAILED: Connect_Net returned false to " + ip + ":" + port);
                BtnDisconnect.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "Erreur";
            TxtStatus.Foreground = (SolidColorBrush)FindResource("Brush.Status.Absent");
            TxtStatusDetail.Text = ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : "");
            Log("EXCEPTION: " + ex.Message);
            BtnDisconnect.IsEnabled = false;
        }
        finally
        {
            BtnTest.IsEnabled = true;
        }
    }

    private Pointeuse? PointeuseCourante => LstPointeuses.SelectedItem as Pointeuse;

    private void BtnSyncTime_Click(object sender, RoutedEventArgs e)
    {
        var p = PointeuseCourante;
        if (p == null) { MessageBox.Show("Sélectionnez une pointeuse."); return; }
        try
        {
            using var d = new ZkDevice();
            if (!d.Connect(p.AdresseIP, p.Port, p.CleCom)) { MessageBox.Show("Connexion échouée."); return; }
            d.SyncTime(p.NumeroMachine);
            d.Disconnect();
            MessageBox.Show("Heure synchronisée.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show("Erreur : " + ex.Message); }
    }

    private void BtnReboot_Click(object sender, RoutedEventArgs e)
    {
        var p = PointeuseCourante;
        if (p == null) { MessageBox.Show("Sélectionnez une pointeuse."); return; }
        if (MessageBox.Show("Redémarrer la pointeuse ? Les employés ne pourront pas badger pendant ~30 s.",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            using var d = new ZkDevice();
            if (!d.Connect(p.AdresseIP, p.Port, p.CleCom)) { MessageBox.Show("Connexion échouée."); return; }
            d.RestartDevice(p.NumeroMachine);
            d.Disconnect();
            MessageBox.Show("Redémarrage envoyé.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show("Erreur : " + ex.Message); }
    }

    private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
    {
        var p = PointeuseCourante;
        if (p == null) { MessageBox.Show("Sélectionnez une pointeuse."); return; }
        if (MessageBox.Show("Effacer TOUS les pointages de l'appareil ? Action irréversible.",
                "KI-GEST Pointage", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            using var d = new ZkDevice();
            if (!d.Connect(p.AdresseIP, p.Port, p.CleCom)) { MessageBox.Show("Connexion échouée."); return; }
            d.ClearAttendanceLogs(p.NumeroMachine);
            d.Disconnect();
            MessageBox.Show("Pointages effacés.", "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show("Erreur : " + ex.Message); }
    }

    private void BtnClearData_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Cette opération complète nécessite des droits administrateur sur l'appareil (SSR_DeleteDeviceData). Non implémenté.",
            "KI-GEST Pointage", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _device?.Disconnect();
            _device?.Dispose();
            _device = null;

            TxtStatus.Text = "Déconnecté";
            TxtStatus.Foreground = (SolidColorBrush)FindResource("Brush.MutedForeground");
            TxtStatusDetail.Text = "La connexion à la pointeuse a été fermée.";
            BtnDisconnect.IsEnabled = false;
        }
        catch (Exception ex)
        {
            TxtStatusDetail.Text = ex.Message;
        }
    }
}
