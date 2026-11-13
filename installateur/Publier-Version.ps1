<#
.SYNOPSIS
    Publie une nouvelle version de KI-GEST Pointage : incrément d'AppVersion, publish x86,
    copie des DLL SDK 32 bits et compilation de l'installateur Inno Setup.
.DESCRIPTION
    Étapes :
      1. (Optionnel) Met à jour #define MyAppVersion dans installateur\KIGest.iss
      2. Arrête KIGest.App.exe si en cours
      3. Supprime le dossier publish\ et repère la publication
         (dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true)
      4. Copie les DLL SDK 32 bits dans publish\sdk\
      5. Compile l'installateur avec ISCC.exe (recherche dans les emplacements connus)
    Résultat : installateur\Output\KI-GEST PointageSetup_<version>.exe
.PARAMETER Version
    Nouvelle version (ex. 1.1.0). Si omis, la version actuelle du .iss est conservée.
.EXAMPLE
    .\installateur\Publier-Version.ps1 -Version 1.1.0
.EXAMPLE
    .\installateur\Publier-Version.ps1            # recompile la version actuelle
#>
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$root       = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$issPath    = Join-Path $root 'installateur\KIGest.iss'
$publishDir = Join-Path $root 'publish'
$sdkSrc     = Join-Path $root 'standalone_sdk\SDK-Ver6.3.1.37\32bit'
$sdkDst     = Join-Path $publishDir 'sdk'
$outDir     = Join-Path $root 'installateur\Output'

function Trouver-Iscc {
    $candidats = @(
        (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source),
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        'C:\Program Files\Inno Setup 6\ISCC.exe',
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 7\ISCC.exe',
        'C:\Program Files (x86)\Inno Setup 7\ISCC.exe'
    ) | Where-Object { $_ -and (Test-Path $_) }
    return $candidats | Select-Object -First 1
}

# 1. Version dans le .iss
$contenu = Get-Content $issPath -Raw
if ($Version) {
    if ($contenu -notmatch '#define MyAppVersion\s+"([^"]+)"') {
        throw "Impossible de trouver #define MyAppVersion dans $issPath"
    }
    $ancienne = $Matches[1]
    $contenu = $contenu -replace '#define MyAppVersion\s+"[^"]+"', "#define MyAppVersion `"$Version`""
    Set-Content -Path $issPath -Value $contenu -Encoding UTF8 -NoNewline
    Write-Host "AppVersion : $ancienne -> $Version" -ForegroundColor Cyan
}
else {
    if ($contenu -match '#define MyAppVersion\s+"([^"]+)"') { $Version = $Matches[1] }
    Write-Host "Version conservée : $Version" -ForegroundColor Cyan
}

# 2. Arrêter l'application si elle tourne
$procs = Get-Process -Name KIGest.App -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host "Arrêt de KIGest.App ($($procs.Count) processus)…" -ForegroundColor Yellow
    $procs | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# 3. Publication x86 self-contained
Write-Host "Publication Release x86…" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root 'KIGest.App\KIGest.App.csproj') -c Release -r win-x86 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Échec de dotnet publish (code $LASTEXITCODE)" }

# Neutralise le fichier appsettings.json livré : aucun identifiant SQL n'est expédié avec le produit.
# La connexion est fournie par KIGest.json, généré par l'assistant d'installation,
# puis chiffré en DPAPI (portée machine) au premier démarrage de l'application.
$appsettingsPublish = Join-Path $publishDir 'appsettings.json'
Set-Content -Path $appsettingsPublish -Value @'
{
  "ConnectionStrings": {}
}
'@ -Encoding UTF8
Write-Host "appsettings.json vidé de ses identifiants (connexion => KIGest.json)." -ForegroundColor Yellow

# 4. DLL SDK 32 bits
Write-Host "Copie des DLL SDK 32 bits…" -ForegroundColor Cyan
New-Item -ItemType Directory -Force $sdkDst | Out-Null
Copy-Item (Join-Path $sdkSrc '*.dll') $sdkDst -Force
$nbDll = (Get-ChildItem $sdkDst -Name).Count
Write-Host "$nbDll DLL copiées."

# 5. Compilation Inno Setup
$iscc = Trouver-Iscc
if (-not $iscc) {
    throw "ISCC.exe introuvable. Installez Inno Setup 6 (winget install JRSoftware.InnoSetup)."
}
Write-Host "Compilation avec ISCC : $iscc" -ForegroundColor Cyan
& $iscc $issPath
if ($LASTEXITCODE -ne 0) { throw "Échec de la compilation Inno Setup (code $LASTEXITCODE)" }

$exe = Get-ChildItem $outDir -Filter "KI-GEST PointageSetup_*.exe" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
Write-Host "Installateur généré : $($exe.FullName)" -ForegroundColor Green
Write-Host ("Taille : {0:0.0} Mo" -f ($exe.Length / 1MB)) -ForegroundColor Green
