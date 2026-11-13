; =====================================================
; KI-GEST Pointage — Installateur Inno Setup
; Application de gestion de présence (pointage) ZKTeco
; =====================================================
; Compiler avec « Inno Setup 6 » (https://jrsoftware.org/isinfo.php) :
;   ISCC.exe KIGest.iss
;
; Le script suppose que la publication x86 a été copiée dans .\publish
; (dotnet publish KIGest.App -c Release -r win-x86 --self-contained true
;  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish)
; et que les DLL SDK 32 bits sont dans .\publish\sdk.

#define MyAppName      "KI-GEST Pointage"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "KI-GEST Pointage"
#define MyAppExeName   "KIGest.App.exe"
#define PublishDir     "..\publish"

[Setup]
AppId={{8A3F5E21-4C6B-4D2A-9F1E-7B2C5A90D341}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename=KI-GEST PointageSetup_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\KIGest.App\Resources\pointage.ico

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Code]
var
  ModePage: TInputOptionWizardPage;
  ConnPage: TInputQueryWizardPage;
  SqlPage: TInputQueryWizardPage;
  DbOptionPage: TInputOptionWizardPage;

function ModeWindows(): Boolean;
begin
  Result := ModePage.SelectedValueIndex = 0;   // 0 = Windows, 1 = SQL
end;

function EscJson(S: String): String;        // "..." dans JSON : \" et \\
var
  I: Integer;
begin
  Result := '';
  for I := 1 to Length(S) do
  begin
    if S[I] = '\' then Result := Result + '\\'
    else if S[I] = '"' then Result := Result + '\"'
    else Result := Result + S[I];
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = ConnPage.ID then
  begin
    if Trim(ConnPage.Values[0]) = '' then begin MsgBox('Le serveur SQL est obligatoire.', mbError, MB_OK); Result := False; exit; end;
    if Trim(ConnPage.Values[1]) = '' then begin MsgBox('Le nom de la base est obligatoire.', mbError, MB_OK); Result := False; exit; end;
  end
  else if CurPageID = SqlPage.ID then
  begin
    if Trim(SqlPage.Values[0]) = '' then begin MsgBox('L''utilisateur SQL est obligatoire.', mbError, MB_OK); Result := False; exit; end;
    if Trim(SqlPage.Values[1]) = '' then begin MsgBox('Le mot de passe SQL est obligatoire.', mbError, MB_OK); Result := False; exit; end;
  end;
end;

function ConfigurationExiste(): Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\KIGest.json'));
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  if ConfigurationExiste() then
    Result := True
  else if PageID = SqlPage.ID then
    Result := ModeWindows()   // en mode Windows, pas de page identifiants
  else
    Result := False;
end;

function SeConfigJson(Serveur, Base, Util, Pwd: String): String;
begin
  if ModeWindows() then
    Result := '{' + #13#10 +
      '  "ConnectionStrings": {' + #13#10 +
      '    "DefaultConnection": "Server=' + EscJson(Serveur) +
      ';Database=' + EscJson(Base) +
      ';Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"' + #13#10 +
      '  }' + #13#10 +
      '}'
  else
    Result := '{' + #13#10 +
      '  "ConnectionStrings": {' + #13#10 +
      '    "DefaultConnection": "Server=' + EscJson(Serveur) +
      ';Database=' + EscJson(Base) +
      ';User Id=' + EscJson(Util) +
      ';Password=' + EscJson(Pwd) +
      ';TrustServerCertificate=True;MultipleActiveResultSets=True;"' + #13#10 +
      '  }' + #13#10 +
      '}';
end;

function SeConfigArgs(): String;
begin
  if ModeWindows() then
    Result := '--initdb --server=""' + ConnPage.Values[0] + '"" --database=""' + ConnPage.Values[1] + '"" --auth=windows'
  else
    Result := '--initdb --server=""' + ConnPage.Values[0] + '"" --database=""' + ConnPage.Values[1] + '"" --user=""' + SqlPage.Values[0] + '"" --password=""' + SqlPage.Values[1] + '"" --auth=sql';
end;

procedure InitializeWizard();
begin
  ModePage := CreateInputOptionPage(wpSelectDir,
    'Méthode de connexion SQL',
    'Comment l''application doit-elle accéder à la base POINTAGE ?',
    'Choisissez votre méthode de connexion :',
    True, True);
  ModePage.Add('Utiliser l''UTILISATEUR WINDOWS actuel (authentification intégrée — recommandé)');
  ModePage.Add('Utiliser des IDENTIFIANTS SQL (saisissez login et mot de passe à l''étape suivante)');
  ModePage.SelectedValueIndex := 0;

  ConnPage := CreateInputQueryPage(ModePage.ID,
    'Serveur et base de données',
    'Où se trouve la base POINTAGE ?',
    'Indiquez l''adresse du serveur (ex. localhost, DESKTOP-XXX\SQLEXPRESS, 192.168.0.x) et le nom de la base.');
  ConnPage.Add('Serveur :', False);
  ConnPage.Add('Base de données :', False);
  ConnPage.Values[0] := '';
  ConnPage.Values[1] := 'POINTAGE';

  SqlPage := CreateInputQueryPage(ConnPage.ID,
    'Identifiants SQL Server',
    'Connexion en mode SQL',
    'Le compte indiqué sera utilisé par l''application au démarrage.');
  SqlPage.Add('Utilisateur SQL :', False);
  SqlPage.Add('Mot de passe :', True);
  SqlPage.Values[0] := '';
  SqlPage.Values[1] := '';

  DbOptionPage := CreateInputOptionPage(SqlPage.ID,
    'Création de la base',
    'Que doit faire l''assistant ?',
    'La base POINTAGE doit exister une seule fois sur le serveur. Choisissez :',
    True, True);
  DbOptionPage.Add('Créer automatiquement la base et le compte s''ils sont absents (droits suffisants requis ou authentification Windows)');
  DbOptionPage.Add('Ne rien créer : la base POINTAGE existe déjà sur ce serveur');
  DbOptionPage.SelectedValueIndex := 0;
end;

function CreerConfigJson(): String;
begin
  if ModeWindows() then
    Result := SeConfigJson(ConnPage.Values[0], ConnPage.Values[1], '', '')
  else
    Result := SeConfigJson(ConnPage.Values[0], ConnPage.Values[1], SqlPage.Values[0], SqlPage.Values[1]);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Fichier: String;
  ResultCode: Integer;
  LogContenu: String;
  LogAnsi: AnsiString;
begin
  if CurStep = ssPostInstall then
  begin
    Fichier := ExpandConstant('{app}\KIGest.json');
    if ConfigurationExiste() then exit;

    // 1. Création automatique via le mode --initdb de l'application
    if DbOptionPage.SelectedValueIndex = 0 then
    begin
      ResultCode := 0;
      LogContenu := '';
      try
        Exec(
          ExpandConstant('{app}\KIGest.App.exe'),
          SeConfigArgs(),
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      except
        LogContenu := GetExceptionMessage;
        ResultCode := -1;
      end;

      if ResultCode <> 0 then
      begin
        try
          begin
            if not LoadStringFromFile(ExpandConstant('{app}\KIGest.setup.log'), LogAnsi) then
              LogContenu := '(journal indisponible)'
            else
              LogContenu := LogAnsi;
          end;
        except
          LogContenu := '(journal indisponible)';
        end;
        MsgBox(
          'La base n''a pas pu être créée automatiquement (code ' + IntToStr(ResultCode) + ').' + #13#10 + #13#10 +
          LogContenu + #13#10 + #13#10 +
          'L''application sera quand même installée.' + #13#10 +
          'Créez la base manuellement (voir installateur\README-Installation.txt), puis relancez l''installateur.',
          mbError, MB_OK);
      end;
    end;

    // 2. Écrire la configuration (chiffrement DPAPI au premier démarrage de l'app)
    SaveStringToFile(Fichier, CreerConfigJson(), False);
  end;
end;

[Tasks]
Name: "desktopicon"; Description: "Créer une icône sur le bureau"; GroupDescription: "Icônes :"

[Files]
; Application publiée (self-contained x86 — aucun prérequis .NET)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; SDK ZKTeco 32 bits (zkemkeeper.dll sera enregistré à la fin de l'installation)
Source: "{#PublishDir}\sdk\*.dll"; DestDir: "{app}\sdk"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Désinstaller {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Enregistrement COM de zkemkeeper.dll (obligatoire, 32 bits)
Filename: "{sys}\regsvr32.exe"; Parameters: "/s ""{app}\sdk\zkemkeeper.dll"""; StatusMsg: "Enregistrement du SDK ZKTeco..."; Flags: runhidden
; Lancer l'application en fin d'installation
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer KI-GEST Pointage"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\regsvr32.exe"; Parameters: "/u /s ""{app}\sdk\zkemkeeper.dll"""; Flags: runhidden; RunOnceId: "UnregisterZkemkeeper"

[Messages]
WelcomeLabel2=Ce programme installe [name/ver] sur votre ordinateur.%n%nÀ la première installation, l'assistant vous émène à travers la connexion SQL Server (serveur, base, identifiants) et peut créer automatiquement la base POINTAGE si les droits SQL le permettent (voir le fichier README-Installation.txt fourni).
