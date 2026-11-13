# ZK Pointage — KI-GEST

Application de bureau Windows (.NET 8 / WPF) de **gestion de présence / pointage** pour pointeuses **ZKTeco**.
Édition française, contexte tunisien. Dépôt officiel de la gamme **KI-GEST Pointage**.

![Version](https://img.shields.io/badge/version-1.2.0-FF4055)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Licence](https://img.shields.io/badge/license-Propriétaire-gray)

---

## Fonctionnalités

- **Synchronisation temps réel** des pointages (lecture incrémentale `ReadNewGLogData`, rafraîchissement toutes les 3 s)
- **Rapports** journalier / mensuel par employé et par service — exports **Excel** (ClosedXML) et **PDF** (QuestPDF)
- **Présences** : calcul réglementaire (retard, départ anticipé, heures travaillées, heures sup), corrections manuelles tracées, congés approuvés, jours fériés tunisiens
- **Export paie** : heures supplémentaires classées **125 % / 150 % / 200 %**
- **Gestion multi-pointeuses** (connexion, import/export employés, reboot, sync heure, effacement logs)
- **Congés & jours fériés** : demandes, approbation, par employé
- **Utilisateurs** : écran de gestion réservé aux administrateurs (rôles Admin / Utilisateur)
- **Sauvegarde / restauration** de la base SQL Server (.bak)
- **Tableau de bord** métier : flux des derniers pointages, KPI, état des pointeuses, détection « Parti tôt » (départ avant l'heure de fin)
- **Installateur Inno Setup** avec assistant de connexion SQL Server (identifiants SQL **ou** utilisateur Windows intégré) et **création automatique de la base** si absente
- Design **KI-GEST** : thème sombre/clair, rouge de marque `#FF4055`

## Stack

| Composant | Choix |
|---|---|
| Langage | C# 12 / .NET 8 (LTS) |
| UI | WPF (MVVM léger, thème UI/UX BJK) |
| Données | EF Core 8 + SQL Server |
| SDK pointeuse | zkemkeeper.dll (COM, **32 bits**) |
| Rapports | QuestPDF (PDF), ClosedXML (Excel) |
| Config | `KIGest.json` (prioritaire, chiffré DPAPI auto) + `appsettings.json` |
| Logs | Serilog (fichier) |
| Installateur | Inno Setup 6 |

## Prérequis

- Windows 10/11 64 bits (l'application est 32 bits pour la compatibilité zkemkeeper)
- .NET SDK 8 (équivalent runtime pour les postes avec l'exe self-contained)
- SQL Server (Express suffit) — instance locale ou distante
- Pointeuse(s) ZKTeco (K40, SpeedFace…) avec SDK Standalone enregistré (`regsvr32 zkemkeeper.dll`)

## Installation (utilisateur final)

Téléchargez `KI-GEST PointageSetup_x.x.x.exe` (dossier `installateur` du projet) et lancez-le :

1. L'assistant propose la **méthode de connexion** :
   - **Utilisateur Windows** (authentification intégrée) — recommandé en local ;
   - **Identifiants SQL** (login + mot de passe).
2. Renseignez le **serveur** (`localhost`, `DESKTOP-XXX\SQLEXPRESS`, `IP`) et la **base** (`POINTAGE`).
3. Choisissez si l'assistant doit **créer la base automatiquement** (droits SQL requis, ou authentification Windows intégrée).
4. L'assistant écrit `KIGest.json` à côté de l'exécutable (chiffrement DPAPI au premier démarrage, le mot de passe SQL ne reste pas en clair).
5. Premier démarrage : l'application crée automatiquement toutes les tables, les paramètres et le compte `admin` / `admin` — **changez ce mot de passe immédiatement**.

> Le fichier `KIGest.json.exemple` à la racine sert de modèle pour un poste de développement.

## Développement

```powershell
# Restaurer / construire
dotnet build KIGest.App\KIGest.App.csproj

# Regénérer l'installation 1.2.0
.\installateur\Publier-Version.ps1 -Version 1.2.0
```

Première exécution : créer un `KIGest.json` à côté de `KIGest.App.exe` (Debug) en s'inspirant de `KIGest.json.exemple`.

## Structure de la solution

```
KIGest.slnx
├─ KIGest.App      # WPF (vues, thèmes, export, session)
├─ KIGest.Core     # Entités, PresenceCalculator, règles métier (§9 du cahier des charges)
├─ KIGest.Data     # EF Core, contexte, initialiseur, seed (73 paramètres), PasswordHasher
└─ KIGest.Device   # Wrapper COM zkemkeeper (Connect_Net, ReadNewGLogData, SSR_*…)
installateur\      # Inno Setup : KIGest.iss, Publier-Version.ps1, README-Installation.txt
```

## Sécurité

- Mots de passe applicatifs hachés **PBKDF2**, jamais en clair ; vérifiés au login
- Base applicative SQL : le mot de passe SQL vit dans `KIGest.json` **chiffré DPAPI (machine locale)** après le premier démarrage
- Pointages bruts jamais modifiés — corrections tracées dans `CorrectionsPointages`
- Journal `Journaux` : connexions, changement de mot de passe, corrections, opérations sur la base

## Licence

Propriétaire — © KI-GEST. Tous droits réservés.
