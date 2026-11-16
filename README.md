# ZK Pointage — KI-GEST

Application de bureau Windows de **gestion de présence / pointage** pour pointeuses **ZKTeco**.
Application française, contexte tunisien. Édition **KI-GEST Pointage**.

![Version](https://img.shields.io/badge/version-1.6.0-FF4055)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Licence](https://img.shields.io/badge/license-Propriétaire-gray)

> Ce dépôt contient **le programme d'installation** et ce fichier.
> Le code source complet reste dans l'historique Git (premier commit de la branche `master`).

---

## Fonctionnalités

- **Synchronisation temps réel** des pointages depuis les pointeuses (lecture incrémentale, rafraîchissement toutes les 3 s)
- **Présences** : calcul réglementaire (retard, départ anticipé, heures travaillées, heures supplémentaires), corrections manuelles tracées, congés approuvés, jours fériés tunisiens
- **Rapports** : journalier et mensuel par employé et par service — exports **Excel** et **PDF**
- **Export paie** : heures supplémentaires 125 % / 150 % / 200 %
- **Multi-pointeuses** : import/export employés, reboot, sync heure, effacement logs
- **Tableau de bord** : derniers pointages temps réel, KPIs, détection des départs anticipés (« Parti tôt »)
- **Pointeuses** : export/import de la configuration (JSON), bouton « Supprimer le journal », lecture du **numéro de série** et **image** de la pointeuse
- **Stabilité** : thread STA dédié pour les appels COM (plus de blocage), timeouts et cooldown (5 timeouts → 2 min de pause), démarrage rapide (index SQL `PointagesBruts`)
- **Gestion d'utilisateurs** (rôles Admin / Utilisateur), **sauvegarde / restauration** de la base

## Prérequis

- Windows 10 / 11 64 bits
- SQL Server (Express ou Standard) — local ou réseau
- Pointeuse ZKTeco (SDK `zkemkeeper.dll` 32 bits, enregistré automatiquement par l'installateur)

## Installation

Lancez **`KI-GEST PointageSetup_1.6.0.exe`** (droits administrateur requis). L'assistant :

1. **Méthode de connexion SQL Server**, au choix :
   - **Utilisateur Windows actuel** (authentification intégrée) — recommandé en local, aucun mot de passe à saisir ;
   - **Identifiants SQL** (utilisateur + mot de passe).
2. **Serveur et base de données** : serveur (`localhost`, `.\SQLEXPRESS`, `DESKTOP-XXX\SQLEXPRESS`, `192.168.0.x`) et nom de la base (`POINTAGE`).
3. **Création automatique de la base** si absente (droits SQL suffisants, ou compte administrateur Windows — repli automatique sur l'authentification intégrée).
4. Écrit `KIGest.json` (connexion) à côté de l'exécutable, chiffrée DPAPI au premier démarrage — **aucun mot de passe en clair sur disque**.

Au premier démarrage, l'application crée automatiquement toutes les tables, les paramètres et le compte **`admin` / `admin`** — **changez ce mot de passe immédiatement** (bouton « Changer MDP »).

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

## Support

KI-GEST — Sfax, Tunisie.

## Licence

Propriétaire — © KI-GEST. Tous droits réservés.
