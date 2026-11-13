============================================================
 KI-GEST Pointage — Guide d'installation (production)
============================================================

Contenu du dossier installateur :
  KI-GEST PointageSetup_x.x.x.exe   → installateur (produit par ISCC.exe)
  publish\                    → application publiée (aucune dépendance requise)
  publish\sdk\                → SDK ZKTeco 32 bits (zkemkeeper.dll…)

------------------------------------------------------------
 1) Préparation du serveur SQL Server
------------------------------------------------------------
L'ASSISTANT PEUT LE FAIRE AUTOMATIQUEMENT (recommandé) :
  - à l'étape « Connexion SQL Server », l'assistant demande les identifiants ;
    le compte doit avoir CREATE DATABASE (ex. 'sa' ou un login admin) — ou l'assistant
    basculera sur l'authentification Windows intégrée (installateur lancé en administrateur) ;
    choisir ensuite « Créer automatiquement la base et le compte s'ils sont absents ».
  - le compte saisi doit être sysadmin (ou disposer de CREATE DATABASE + ALTER ANY LOGIN).
    Ex. 'sa', ou le compte Windows d'un administrateur SQL.
  - l'assistant crée alors : la base POINTAGE, le login SQL choisi (si absent),
    l'utilisateur dans la base et lui affecte le rôle db_owner.
  - l'application crée ensuite toutes les tables, les paramètres et
    le compte administrateur (login : admin / admin) au premier démarrage.

Sinon, préparation manuelle (SSMS), une seule fois :

    CREATE DATABASE POINTAGE;
    GO
    IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'gmao')
        CREATE LOGIN gmao WITH PASSWORD = '123456', CHECK_POLICY = OFF;
    GO
    ALTER ROLE db_owner ADD MEMBER gmao;   -- (sur la base POINTAGE)
    GO

L'application crée ensuite automatiquement toutes les tables, les paramètres
et le compte administrateur (login : admin / admin) au premier démarrage.

------------------------------------------------------------
 2) Installation de l'application
------------------------------------------------------------
Double-cliquez sur KI-GEST PointageSetup_x.x.x.exe (droits administrateur requis).
L'installateur :
  - copie l'application dans « C:\Program Files (x86)\KI-GEST Pointage »
    (l'application est 32 bits pour rester compatible zkemkeeper.dll)
  - copie les DLL SDK dans « ...\KI-GEST Pointage\sdk »
  - enregistre zkemkeeper.dll (regsvr32)
  - propose l'étape « Méthode de connexion SQL » :
      a. L'UTILISATEUR WINDOWS actuel (authentification intégrée) — recommandé pour une machine locale ;
      b. Des IDENTIFIANTS SQL (utilisateur + mot de passe).
  - propose l'étape « Serveur et base de données » : serveur (localhost, DESKTOP-XXX\SQLEXPRESS, IP)
    et nom de la base (POINTAGE).
  - en mode SQL, propose l'étape « Identifiants SQL Server » : login et mot de passe.
  - propose ensuite l'étape « Création de la base » : si vous choisissez
    « Créer automatiquement », l'assistant crée la base POINTAGE et le login SQL s'ils sont absents.
  - écrit « KIGest.json » à côté de KIGest.App.exe
  - crée le raccourci du menu Démarrer et du bureau

Pour un poste de développement, voir aussi `KIGest.json.exemple` à la racine de la solution.

Serveur : IP ou nom d'instance (ex. « localhost », « DESKTOP-T5QIOQU\SQLEXPRESS » ou « 192.168.0.x,1433 »).
KIGest.json est PRIORITAIRE sur appsettings.json. Pour modifier la connexion plus tard,
éditez simplement ce fichier puis relancez l'application — aucune réinstallation n'est nécessaire.

SÉCURITÉ DU MOT DE PASSE :
- appsettings.json ne contient AUCUN identifiant SQL (vide dans le produit livré).
- La connexion est écrite par l'assistant dans « KIGest.json » (en clair uniquement
  entre l'installation et le premier démarrage).
- Au PREMIER démarrage, KIGest.json est automatiquement converti en données chiffrées DPAPI
  (portée machine locale) : lisible uniquement sur cette machine, impossible de le détourner ailleurs.
  Conserver ensuite une copie chiffrée n'est donc plus sensible.
- Le poste de développement doit copier KIGest.json.exemple en KIGest.json
  (à côté de l'exe en build Debug — chiffré automatiquement au premier lancement).

------------------------------------------------------------
 3) Premier démarrage
------------------------------------------------------------
  1. Lancer KI-GEST Pointage (icône bureau / menu Démarrer)
  2. Connexion : admin / admin
  3. IMPORTANT : changer le mot de passe (bouton « Changer MDP »)
  4. Paramètres → vérifier le serveur SQL (localhost), la base POINTAGE
  5. Pointeuses → ajouter la pointeuse (IP, port 4370, clé de communication)
     puis « Vérifier la connexion »
  6. Tableau de bord → « Synchroniser maintenant » pour l'import complet
     (la synchronisation temps réel est ensuite active par défaut)

------------------------------------------------------------
 4) Mise à jour / désinstallation
------------------------------------------------------------
  Mise à jour : lancer le nouveau KI-GEST PointageSetup_x.x.x.exe (remplace les fichiers)
  Désinstallation : Panneau de configuration → Programmes → KI-GEST Pointage
                    (zkemkeeper.dll sera désenregistré automatiquement)
  La base SQL Server « POINTAGE » n'est PAS supprimée à la désinstallation.

------------------------------------------------------------
 5) Sauvegarde / restauration
------------------------------------------------------------
Page « Sauvegarde » de l'application (.bak dans C:\KI-GEST Pointage\Backups).
La restauration exige un compte SQL sysadmin (ou restauration manuelle en SSMS).

------------------------------------------------------------
 6) Dépannage
------------------------------------------------------------
  « zkemkeeper.dll n'est pas enregistré »
     → Ouvrir une invite administrateur :
       C:\Windows\SysWOW64\regsvr32.exe "C:\Program Files (x86)\KI-GEST Pointage\sdk\zkemkeeper.dll"
  L'application est 32 bits : le regsvr32 à utiliser est bien celui de SysWOW64.

  « Connexion échouée » vers la pointeuse
     → Vérifier IP/port 4370/pare-feu, et la clé de communication sur
       la pointeuse : Menu → Comm → Clé comm (0 par défaut).
