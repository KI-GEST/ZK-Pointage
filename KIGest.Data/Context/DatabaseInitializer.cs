using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using KIGest.Core.Entities;

namespace KIGest.Data.Context;

/// <summary>
/// Crée la base si nécessaire et insère les paramètres par défaut.
/// </summary>
public static class DatabaseInitializer
{
    public static readonly (string Cle, string Valeur, string Description, string Type, string Groupe)[] ParametresParDefaut =
    {
        // Société
        ("Societe.Nom", "Ma Société", "Raison sociale affichée sur les rapports et exports", "Texte", "Société"),
        ("Societe.FormeJuridique", "SARL", "SARL, SA, SNC, EI, GIE...", "Texte", "Société"),
        ("Societe.MatriculeFiscale", "", "Matricule fiscale / N° TVA (ex. 0000000/A/...)", "Texte", "Société"),
        ("Societe.CNSS", "", "Numéro CNSS de l'entreprise", "Texte", "Société"),
        ("Societe.RegistreCommerce", "", "Extrait du registre de commerce (RCS)", "Texte", "Société"),
        ("Societe.Adresse", "", "Adresse de la société", "Texte", "Société"),
        ("Societe.Ville", "", "Ville", "Texte", "Société"),
        ("Societe.Pays", "Tunisie", "Pays / nationalité", "Texte", "Société"),
        ("Societe.Telephone", "", "Téléphone de la société", "Texte", "Société"),
        ("Societe.Email", "", "Email de contact", "Texte", "Société"),
        ("Societe.SiteWeb", "", "Site web (optionnel)", "Texte", "Société"),

        // Général
        ("Locale.Culture", "fr-TN", "Culture de l'interface (heures, dates, nombres)", "Texte", "Général"),
        ("Locale.FormatDate", "dd/MM/yyyy", "Format d'affichage des dates", "Texte", "Général"),
        ("Locale.Theme", "Sombre", "Thème de l'interface (Sombre ou Clair)", "Enum", "Général"),
        ("Locale.LangueInterface", "FR", "Langue de l'interface (FR ou AR)", "Enum", "Général"),

        // Pointage
        ("Pointage.ToleranceRetardMin", "10", "Tolérance de retard par défaut (minutes)", "Nombre", "Pointage"),
        ("Pointage.AntiRebondMin", "2", "Anti-rebond : 2 pointages à moins de X minutes sont fusionnés", "Nombre", "Pointage"),
        ("Pointage.IntervalleSyncMin", "15", "Intervalle de synchronisation automatique (minutes, 0 = désactivé)", "Nombre", "Pointage"),
        ("Pointage.HeureDebut", "08:00", "Heure de début d'horaire par défaut (HH:mm)", "Heure", "Pointage"),
        ("Pointage.HeureFin", "17:00", "Heure de fin d'horaire par défaut (HH:mm)", "Heure", "Pointage"),
        ("Pointage.PauseDebut", "12:00", "Début de pause par défaut (HH:mm)", "Heure", "Pointage"),
        ("Pointage.PauseFin", "13:00", "Fin de pause par défaut (HH:mm)", "Heure", "Pointage"),
        ("Pointage.HoraireParDefaut", "Horaire de bureau", "Horaire par défaut appliqué aux employés sans affectation individuelle ni service", "Texte", "Pointage"),
        ("Pointage.NumeroMachine", "1", "Numéro de machine par défaut (1 = première pointeuse)", "Nombre", "Pointage"),
        ("HeuresSup.SeuilJour", "8", "Seuil journalier (heures) au-delà duquel s'appliquent les heures supplémentaires", "Nombre", "Heures sup"),
        ("HeuresSup.TauxNormal", "125", "Taux des heures supplémentaires normales (%)", "Nombre", "Heures sup"),
        ("HeuresSup.TauxNuit", "150", "Taux des heures supplémentaires en horaire de nuit (%)", "Nombre", "Heures sup"),
        ("HeuresSup.TauxFerie", "200", "Taux des heures supplémentaires travaillées un férié/repos (%)", "Nombre", "Heures sup"),
        ("Employee.EmployeeIDFormat", "Matricule", "Format d'ID employé (Matricule ou BadgeNumero)", "Enum", "Employee ID"),
        ("Employee.EmployeeIDLength", "8", "Longueur maximale de l'identifiant employé en caractères", "Nombre", "Employee ID"),
        ("Employee.StaffCheckInMode", "Entrée/Sortie", "Mode d'enregistrement (Entrée, Sortie, Entrée/Sortie)", "Enum", "Employee ID"),
        ("Employee.AutoSynchroniserAppareil", "true", "Synchroniser automatiquement les données vers les appareils", "Booleen", "Employee ID"),
        ("Employee.ReuploadFromDevice", "false", "Re-uploader les templates biométriques depuis la pointeuse (override)", "Booleen", "Employee ID"),
        ("Pointage.DelaiAntiRebondVehicule", "2", "Anti-rebond spécifique visites (minutes)", "Nombre", "Pointage"),

        // Présences
        ("Presence.JoursTravailSemaine", "5", "Nombre de jours travaillés par semaine (5, 5.5 ou 6)", "Nombre", "Présences"),
        ("Presence.HeuresSupTaux125", "true", "Heures supplémentaires à 125 %", "Booleen", "Présences"),
        ("Presence.HeuresSupTaux150", "true", "Heures supplémentaires à 150 %", "Booleen", "Présences"),
        ("Presence.HeuresSupTaux200", "false", "Heures supplémentaires à 200 %", "Booleen", "Présences"),
        ("Presence.SeuilAlerteRetardMin", "30", "Seuil d'alerte retard répété (minutes)", "Nombre", "Présences"),
        ("Presence.SeuilDepartment", "", "Service concerné(s) par les calculs avancés (liste séparée par des points-virgules)", "Texte", "Présences"),

        // Exports
        ("Export.DossierExcel", @"C:\KI-GEST Pointage\Exports\Excel", "Dossier de destination des exports Excel", "Texte", "Exports"),
        ("Export.DossierPDF", @"C:\KI-GEST Pointage\Exports\PDF", "Dossier de destination des exports PDF", "Texte", "Exports"),
        ("Export.FormatParDefaut", "Excel", "Format d'export par défaut (Excel ou PDF)", "Enum", "Exports"),
        ("Export.AvecLogo", "true", "Inclure le logo de la société dans les PDF", "Booleen", "Exports"),
        ("Export.EntetePersonnalise", "", "Ligne d'en-tête personnalisée des PDF (texte libre)", "Texte", "Exports"),
        ("Export.PiedPage", "Document généré par KI-GEST Pointage", "Ligne de pied de page des PDF", "Texte", "Exports"),

        // Notifications
        ("Notif.Actif", "false", "Activer l'envoi de notifications (email)", "Booleen", "Notifications"),
        ("Notif.SmtpServeur", "", "Serveur SMTP (ex. smtp.office365.com)", "Texte", "Notifications"),
        ("Notif.SmtpPort", "587", "Port SMTP (25, 465 ou 587)", "Nombre", "Notifications"),
        ("Notif.SmtpSecurite", "STARTTLS", "Sécurité SMTP (None, SSL, STARTTLS)", "Enum", "Notifications"),
        ("Notif.EmailExpediteur", "", "Adresse expéditeur (ex. alertes@monemail.com)", "Texte", "Notifications"),
        ("Notif.EmailDestinataires", "", "Destinataires des alertes (séparés par des point-virgules)", "Texte", "Notifications"),
        ("Notif.MotDePasse", "", "Mot de passe SMTP", "Texte", "Notifications"),
        ("Notif.EnvoyerAbsences", "true", "Envoyer une notification pour les absences", "Booleen", "Notifications"),
        ("Notif.EnvoyerRetards", "false", "Envoyer une notification pour les retards répétés", "Booleen", "Notifications"),
        ("Notif.Frequence", "Quotidien", "Fréquence de synthèse (Quotidien, Hebdomadaire)", "Enum", "Notifications"),

        // Sécurité
        ("Security.JoursExpirationMotDePasse", "90", "Durée de validité d'un mot de passe (jours)", "Nombre", "Sécurité"),
        ("Security.TentativesMaxConnexion", "5", "Nombre de tentatives de connexion échouées autorisées", "Nombre", "Sécurité"),
        ("Security.VerrouillageApresEchec", "5", "Blocage du compte après X échecs", "Nombre", "Sécurité"),
        ("Security.DureeVerrouillageMin", "15", "Durée du verrouillage temporaire (minutes)", "Nombre", "Sécurité"),
        ("Security.LongueurMinPassword", "8", "Longueur minimale des mots de passe", "Nombre", "Sécurité"),
        ("Security.ExigerMajuscule", "true", "Exiger au moins une majuscule", "Booleen", "Sécurité"),
        ("Security.ExigerMinMaj", "true", "Exiger au moins une minuscule", "Booleen", "Sécurité"),
        ("Security.ExigerChiffre", "true", "Exiger au moins un chiffre", "Booleen", "Sécurité"),
        ("Security.ExigerSpecial", "false", "Exiger au moins un caractère spécial", "Booleen", "Sécurité"),
        ("Security.RoleParDefaut", "RH", "Rôle par défaut à la création d'un compte (Admin, RH, Consultation)", "Enum", "Sécurité"),
        ("Security.TimeoutSessionMin", "30", "Déconnexion automatique après X minutes d'inactivité", "Nombre", "Sécurité"),
        ("Security.AuditToutesActions", "true", "Enregistrer toutes les actions dans le journal", "Booleen", "Sécurité"),

        // Sauvegarde
        ("Sauvegarde.Actif", "true", "Activer les sauvegardes automatiques", "Booleen", "Sauvegarde"),
        ("Sauvegarde.Dossier", @"C:\KI-GEST Pointage\Backups", "Dossier des sauvegardes", "Texte", "Sauvegarde"),
        ("Sauvegarde.RetentionJours", "30", "Nombre de jours de conservation des sauvegardes", "Nombre", "Sauvegarde"),
        ("Sauvegarde.Frequence", "Quotidien", "Fréquence (Quotidien ou Hebdomadaire)", "Enum", "Sauvegarde"),
        ("Sauvegarde.Heure", "02:00", "Heure de la sauvegarde planifiée (HH:mm)", "Heure", "Sauvegarde")
    };

    public static void Initialize(KiGestDbContext context)
    {
        context.Database.EnsureCreated();

        var existing = context.Parametres.Select(p => p.Cle).ToList();
        foreach (var def in ParametresParDefaut)
        {
            if (!existing.Contains(def.Cle))
            {
                context.Parametres.Add(new Parametre
                {
                    Cle = def.Cle,
                    Valeur = def.Valeur,
                    Description = def.Description,
                    Type = def.Type,
                    Groupe = def.Groupe,
                    DateModification = DateTime.Now
                });
            }
        }
        context.SaveChanges();

        if (!context.Utilisateurs.Any())
        {
            context.Utilisateurs.Add(new Utilisateur
            {
                Login = "admin",
                MotDePasseHash = PasswordHasher.Hash("admin"),
                Role = "Admin"
            });
            context.SaveChanges();
        }
    }
}
