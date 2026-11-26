# ZK Pointage — KI-GEST

[![Version](https://img.shields.io/badge/version-1.9.3-FF4055)](https://github.com/KI-GEST/ZK-Pointage/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Licence](https://img.shields.io/badge/license-Propriétaire-gray)](#licence)
[![SQL Server / Access](https://img.shields.io/badge/SQL%20Server%20%2F%20Access-A80000)](https://www.microsoft.com/sql-server)
[![Platform](https://img.shields.io/badge/Windows-10%2F11-0078D6)](https://www.microsoft.com/windows)

## Logiciel de pointage pour pointeuses ZKTeco — time attendance software

Application de bureau Windows de **gestion de présence / pointage** pour **pointeuses ZKTeco** (badge + reconnaissance faciale **SpeedFace**) : présences, pointages temps réel, rapports **Excel** et **PDF**, export **paie**, multi-pointeuses.

Application française, contexte tunisien. Édition **KI-GEST Pointage**.

> ZKTeco time attendance software · logiciel de pointage ZKTeco · pointeuse badge · gestion de présence · ZKTeco time attendance system

---

## Fonctionnalités

- **Synchronisation temps réel** des pointages depuis les pointeuses (lecture incrémentale, rafraîchissement toutes les 3 s)
- **Présences** : calcul réglementaire (retard, départ anticipé, heures travaillées, **heures supplémentaires**), corrections manuelles tracées, congés approuvés, jours fériés tunisiens
- **Rapports** : journalier et mensuel par employé et par service — exports **Excel** et **PDF**
- **Export paie** : heures supplémentaires **125 % / 150 % / 200 %**
- **Multi-pointeuses** : import/export employés, reboot, sync heure, effacement logs
- **Tableau de bord** : derniers pointages temps réel, KPIs, détection des départs anticipés (« Parti tôt ») — avec **filtre société local** (KPI, derniers pointages et état des pointeuses) et colonne « Société » en vue consolidée
- **Pointeuses** : export/import de la configuration (JSON), bouton « Supprimer le journal », lecture du **numéro de série** et **image** de la pointeuse ; **société de rattachement visible et modifiable** (liste, fiche, assistant de scan réseau), reprise dans l'export/import JSON
- **Stabilité** : thread STA dédié pour les appels COM (plus de blocage), timeouts et cooldown (5 timeouts → 2 min de pause), démarrage rapide (index SQL `PointagesBruts`)
- **Gestion d'utilisateurs** (rôles Admin / Utilisateur), **sauvegarde / restauration** de la base
- **Multi-sociétés** : un seul install pour plusieurs sociétés — sélecteur header (vue **« Toutes les sociétés » par défaut**), page **Sociétés** (CRUD intégral), toutes les données filtrées par la société choisie ; les pages **Horaires & affectations** et **Tableau de bord** disposent en plus de leur propre filtre société (colonne « Société » en vue consolidée) ; à la première installation, une société **« Ma Société »** est créée automatiquement
- **Localization FR / EN** : interface bilingue (Paramètres > Général > Langue), satellite assemblies générés à la compilation

## Prérequis

- Windows 10 / 11 64 bits
- **SQL Server** Express/Standard (l'installateur peut provisionner SQL Server Express 2017 automatiquement, ou utiliser votre instance existante) **ou fichier Access `.mdb` local** (aucun serveur à installer)
- Pointeuse ZKTeco (SDK `zkemkeeper.dll` 32 bits, enregistré automatiquement par l'installateur)

## Installation

**Première installation (à partir de zéro) :** téléchargez **`KI-GEST PointageSetup_1.7.6.exe`** depuis la page [**Releases**](https://github.com/KI-GEST/ZK-Pointage/releases) et lancez-le (droits administrateur requis).

**Toutes les versions suivantes (mises à jour depuis une installation existante) :** aucune manipulation nécessaire. L'application :
- détecte automatiquement la présence d'une mise à jour (bouton « Mise à jour » — sidebar SYSTÈME) ;
- télécharge un **petit delta (fichiers modifiés uniquement)**, beaucoup plus léger que le Setup ;
- remplace les fichiers et redémarre toute seule.

L'assistant de première installation :

1. **Stockage de la base**, au choix :
   - **Installer SQL Server Express 2017** — tout est automatique : instance `SQLEXPRESS`, base `POINTAGE`, zéro configuration ; l'instance est détectée si elle existe déjà ;
   - **Utiliser une instance SQL Server existante** (Express ou complète, locale ou distante) ;
   - **Fichier Access local** — `POINTAGE.mdb` est créé dans le dossier racine de l'installation, aucun serveur SQL n'est nécessaire.
2. **Méthode de connexion SQL Server**, au choix (étapes ignorées en mode fichier Access) :
   - **Utilisateur Windows actuel** (authentification intégrée) — recommandé en local, aucun mot de passe à saisir ; l'installateur provisionne aussi un login Windows pour l'utilisateur qui l'exécute ;
   - **Identifiants SQL** (utilisateur + mot de passe).
3. **Création automatique de la base** si absente (droits SQL suffisants, ou compte administrateur Windows — repli automatique sur l'authentification intégrée).
4. Écrit `KIGest.json` (connexion) à côté de l'exécutable, chiffrée DPAPI au premier démarrage — **aucun mot de passe en clair sur disque**.
5. En cas de problème de connexion après une mise à jour, utilisez « **Réparer la connexion KI-GEST** » (Menu Démarrer → KI-GEST Pointage) : crée le login Windows de l'opérateur (db_owner sur POINTAGE) et bascule la configuration en authentification Windows intégrée.

Au premier démarrage, l'application applique ses **migrations EF Core** : création automatique des tables sur une base neuve, ou **inscription comme déjà appliquée de la migration initiale** sur une base existante (aucune recréation ni modification de schéma), puis insertion des paramètres, d'une société **« Ma Société »** (renommable depuis la page Sociétés) et du compte **`admin` / `admin`** — **changez ce mot de passe immédiatement** (bouton « Changer MDP »). Les évolutions de schéma futures sont ainsi appliquées automatiquement au démarrage, sans script SQL manuel. En mode **fichier Access**, le schéma (19 tables) est créé par `EnsureCreated` au premier démarrage.

## Compatibilité ZKTeco

### Mises à jour

- **Updates delta only** : depuis la v1.7.6, la distribution publique ne publie plus que le `delta-<version>.zip`. La v1.7.6 (setup complet, multi-fichiers) reste la base — une seule installation nécessaire. Toutes les versions suivantes se mettent à jour automatiquement.
- **Mise à jour assistée** : au démarrage, l'application vérifie GitHub et propose d'installer la nouvelle version en un clic (bouton « Mise à jour » — sidebar SYSTÈME).
- **Service de mise à jour** : un service Windows « KI-GEST Pointage » vérifie quotidiennement la disponibilité des mises à jour (sans rien télécharger) et journalise dans l'Observateur d'événements.

Pointeuses ZKTeco fonctionnant avec le SDK `zkemkeeper.dll` (famille SpeedFace, iFace, TFT, Black&White) : **pointage par badge** (IC/RFID, pointage par carte) et **reconnaissance faciale** (modèles SpeedFace comme le ZMM510). Les pointages sont lus par l'appareil (temps réel, toutes les 3 s) et stockés dans SQL Server.

## Cas d'usage

- Gestion de présence et du temps de travail en entreprise
- Logiciel de paie corrélé aux heures supplémentaires
- Contrôle d'accès par badge / visage multi-pointeuses
- Rapports de présence quotidiens et mensuels

## Configuration

Le fichier `KIGest.json` (à côté de `KIGest.App.exe`) contient la connexion — prioritaire sur toute autre configuration. Le choix du backend se fait **dans l'installateur** (SQL Server Express / instance existante / fichier Access local) et se modifie ensuite dans l'application : **Paramètres → Base** :

- **SQL Server — Windows intégré** (recommandé) ou **compte SQL** : serveur, base, utilisateur, mot de passe, bouton « Tester la connexion » ;
- **Fichier Access local** : chemin de `POINTAGE.mdb`, créé automatiquement s'il n'existe pas dans le dossier d'installation.

Le mot de passe SQL est chiffré DPAPI dans `KIGest.json`. Plus aucun raccourci dédié n'est nécessaire. Le pilote ODBC Microsoft Access **32 bits** est requis pour le mode fichier (fourni avec Office/Access Database Engine).

Depuis la **v1.9.2**, l'installateur accorde aux utilisateurs standards l'écriture sur `KIGest.json` (en plus du dossier et de `POINTAGE.mdb`), afin que le changement SQL ↔ Access depuis **Paramètres → Base** fonctionne sans élévation. Sur une installation 1.9.0/1.9.1, exécutez `Reparer-Access.ps1` en administrateur (fourni dans la release 1.9.1/1.9.2). Redémarrez l'application après le changement de backend.

Depuis la **v1.9.3**, la page **Paramètres → Base** contient aussi deux boutons de **conversion des données** :

- **SQL → Access** : copie la base SQL active vers le fichier `.mdb` affiché dans le formulaire ;
- **Access → SQL** : copie le fichier `.mdb` actif vers la base SQL affichée dans le formulaire.

La cible est **vidée puis remplacée entièrement** (opération irréversible), dans une transaction, en conservant les identifiants et les relations. Les champs chiffrés (CIN) sont copiés sans déchiffrement. Le schéma SQL existant n'est jamais modifié : la base SQL cible doit déjà être initialisée. Après une conversion réussie, la configuration est enregistrée automatiquement et un redémarrage est nécessaire.

Depuis la **v1.8.7**, si les identifiants SQL ne fonctionnent plus (mot de passe changé, compte verrouillé), l'application réessaie automatiquement avec l'**authentification Windows intégrée** sur le même serveur et la même base, sans réécrire `KIGest.json` : les deux modes restent disponibles.

Exemple :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=POINTAGE;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
  }
}
```

## Sécurité

- **Mots de passe** : hachage PBKDF2-HMAC-SHA256 (100 000 itérations, sel aléatoire).
- **Premier démarrage** : le mot de passe par défaut `admin` doit être changé **obligatoirement** avant tout autre écran — l'application ne laisse pas accéder le tableau de bord tant qu'il est en place.
- **Verrouillage du compte** après 5 échecs de connexion (15 minutes, paramétrable), journalisé.
- **Chaîne SQL & secrets SMTP** : chiffrés DPAPI (machine locale), jamais en clair sur disque après le premier démarrage. Le chemin du fichier Access est également stocké chiffré.
- **CIN (données personnelles)** : chiffrées DPAPI au repos dans la base (colonne `Employes.CIN`), jamais affichées en clair — seul l'admin peut les voir.
- **Sauvegardes** (`.bak`) : `BACKUP DATABASE` côté SQL Server ; copie du fichier `.mdb` côté Access. L'installateur accorde l'écriture aux utilisateurs sur le fichier **et sur son dossier** (Jet doit pouvoir créer `POINTAGE.ldb` à côté de la base).
- **Traçabilité** : journal des connexions, échecs, changements de mot de passe et actions sensibles.

## Support

KI-GEST — Sfax, Tunisie.

## Licence

Propriétaire — © KI-GEST. Tous droits réservés.
