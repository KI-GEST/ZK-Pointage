# ZK Pointage — KI-GEST

[![Version](https://img.shields.io/badge/version-1.6.0-FF4055)](https://github.com/KI-GEST/ZK-Pointage/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Licence](https://img.shields.io/badge/license-Propriétaire-gray)](#licence)
[![SQL Server](https://img.shields.io/badge/SQL-Server-A80000)](https://www.microsoft.com/sql-server)
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
- **Tableau de bord** : derniers pointages temps réel, KPIs, détection des départs anticipés (« Parti tôt »)
- **Pointeuses** : export/import de la configuration (JSON), bouton « Supprimer le journal », lecture du **numéro de série** et **image** de la pointeuse
- **Stabilité** : thread STA dédié pour les appels COM (plus de blocage), timeouts et cooldown (5 timeouts → 2 min de pause), démarrage rapide (index SQL `PointagesBruts`)
- **Gestion d'utilisateurs** (rôles Admin / Utilisateur), **sauvegarde / restauration** de la base

## Prérequis

- Windows 10 / 11 64 bits
- SQL Server Express **ou** Standard — l'installateur peut provisionner SQL Server Express 2017 automatiquement, ou utiliser votre instance existante
- Pointeuse ZKTeco (SDK `zkemkeeper.dll` 32 bits, enregistré automatiquement par l'installateur)

## Installation

**Première installation (à partir de zéro) :** téléchargez **`KI-GEST PointageSetup_1.7.5.exe`** depuis la page [**Releases**](https://github.com/KI-GEST/ZK-Pointage/releases) et lancez-le (droits administrateur requis).

**Toutes les versions suivantes (mises à jour depuis une installation existante) :** aucune manipulation nécessaire. L'application :
- détecte automatiquement la présence d'une mise à jour (bouton « Mise à jour » — sidebar SYSTÈME) ;
- télécharge un **petit delta (fichiers modifiés uniquement)**, beaucoup plus léger que le Setup ;
- remplace les fichiers et redémarre toute seule.

L'assistant de première installation :

1. **Instance SQL Server**, au choix :
   - **Installer SQL Server Express 2017** — tout est automatique : instance `SQLEXPRESS`, base `POINTAGE`, zéro configuration ; l'instance est détectée si elle existe déjà ;
   - **Utiliser une instance SQL Server existante** (Express ou complète, locale ou distante).
2. **Méthode de connexion SQL Server**, au choix :
   - **Utilisateur Windows actuel** (authentification intégrée) — recommandé en local, aucun mot de passe à saisir ; l'installateur provisionne aussi un login Windows pour l'utilisateur qui l'exécute ;
   - **Identifiants SQL** (utilisateur + mot de passe).
3. **Création automatique de la base** si absente (droits SQL suffisants, ou compte administrateur Windows — repli automatique sur l'authentification intégrée).
4. Écrit `KIGest.json` (connexion) à côté de l'exécutable, chiffrée DPAPI au premier démarrage — **aucun mot de passe en clair sur disque**.
5. En cas de problème de connexion après une mise à jour, utilisez « **Réparer la connexion KI-GEST** » (Menu Démarrer → KI-GEST Pointage) : crée le login Windows de l'opérateur (db_owner sur POINTAGE) et bascule la configuration en authentification Windows intégrée.

Au premier démarrage, l'application crée automatiquement toutes les tables, les paramètres et le compte **`admin` / `admin`** — **changez ce mot de passe immédiatement** (bouton « Changer MDP »).

## Compatibilité ZKTeco

### Mises à jour

- **Mise à jour assistée** : au démarrage, l'application vérifie GitHub et propose d'installer la nouvelle version en un clic (bouton « Mise à jour » — sidebar SYSTÈME).
- **Service de mise à jour** : un service Windows « KI-GEST Pointage » vérifie quotidiennement la disponibilité des mises à jour (sans rien télécharger) et journalise dans l'Observateur d'événements.

Pointeuses ZKTeco fonctionnant avec le SDK `zkemkeeper.dll` (famille SpeedFace, iFace, TFT, Black&White) : **pointage par badge** (IC/RFID, pointage par carte) et **reconnaissance faciale** (modèles SpeedFace comme le ZMM510). Les pointages sont lus par l'appareil (temps réel, toutes les 3 s) et stockés dans SQL Server.

## Cas d'usage

- Gestion de présence et du temps de travail en entreprise
- Logiciel de paie corrélé aux heures supplémentaires
- Contrôle d'accès par badge / visage multi-pointeuses
- Rapports de présence quotidiens et mensuels

## Configuration

Le fichier `KIGest.json` (à côté de `KIGest.App.exe`) contient la connexion SQL — prioritaire sur toute autre configuration. Modifiable après installation, l'application se reconnecte au redémarrage.

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
- **Chaîne SQL & secrets SMTP** : chiffrés DPAPI (machine locale), jamais en clair sur disque après le premier démarrage.
- **Sauvegardes** (`.bak`) : stockées dans un dossier protégé — lecture restreinte, écriture réservée au compte SQL Server.
- **Traçabilité** : journal des connexions, échecs, changements de mot de passe et actions sensibles.

## Support

KI-GEST — Sfax, Tunisie.

## Licence

Propriétaire — © KI-GEST. Tous droits réservés.
