# Projet 6 - etape 8 bis : installation du panneau de controle des VM
# A lancer dans un PowerShell NORMAL (pas administrateur), sur le PC du labo :
#   powershell -ExecutionPolicy Bypass -File D:\Labo-P6\panneau\installer-panneau.ps1
# Le panneau doit tourner sous TON compte Windows : c'est lui qui fait tourner les VM dans VMware.
$ErrorActionPreference = 'Stop'
function Etape($t) { Write-Host ""; Write-Host "=== $t ===" -ForegroundColor Cyan }
$dossier = 'D:\Labo-P6\panneau'
Set-Location $dossier

Etape "1/5 Environnement Python isole (.venv) et dependances (Flask, Waitress)"
if (-not (Test-Path "$dossier\.venv\Scripts\python.exe")) { python -m venv "$dossier\.venv" }
& "$dossier\.venv\Scripts\python.exe" -m pip install --quiet --disable-pip-version-check -r "$dossier\requirements.txt"
Write-Host "Dependances installees."

Etape "2/5 Droits du dossier : seulement toi, SYSTEM et les administrateurs"
$moi = "$env:USERDOMAIN\$env:USERNAME"
icacls $dossier /inheritance:r /grant:r "${moi}:(OI)(CI)F" '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
Write-Host "Le compte p6labo et les autres utilisateurs ne peuvent pas lire les mots de passe du panneau."

Etape "3/5 Ton compte administrateur du panneau"
$liste = & "$dossier\.venv\Scripts\python.exe" "$dossier\panneau.py" utilisateur liste
if (-not ($liste -match 'admin')) {
    Write-Host "Choisis le mot de passe de ton compte 'karim' (12 caracteres minimum, il ne s'affiche pas) :"
    & "$dossier\.venv\Scripts\python.exe" "$dossier\panneau.py" utilisateur ajouter karim --admin
} else { Write-Host "Un compte administrateur existe deja." }

Etape "4/5 Demarrage automatique a l'ouverture de ta session Windows"
$demarrage = [Environment]::GetFolderPath('Startup')
$raccourci = (New-Object -ComObject WScript.Shell).CreateShortcut("$demarrage\Panneau labo P6.lnk")
$raccourci.TargetPath = "$dossier\.venv\Scripts\pythonw.exe"
$raccourci.Arguments = "`"$dossier\panneau.py`" serve"
$raccourci.WorkingDirectory = $dossier
$raccourci.Description = 'Projet 6 - panneau de controle des VM (127.0.0.1:8080)'
$raccourci.Save()
Write-Host "Raccourci cree dans : $demarrage"

Etape "5/5 Lancement et verification"
$deja = Get-NetTCPConnection -LocalAddress 127.0.0.1 -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue
if (-not $deja) {
    Start-Process -FilePath "$dossier\.venv\Scripts\pythonw.exe" -ArgumentList "`"$dossier\panneau.py`" serve" -WorkingDirectory $dossier
    Start-Sleep -Seconds 4
}
$ecoute = Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue
$ecoute | Format-Table LocalAddress, LocalPort, State -AutoSize
if ($ecoute.LocalAddress -contains '0.0.0.0') { Write-Host "ATTENTION : le panneau ecoute sur toutes les adresses !" -ForegroundColor Red }
$r = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8080/connexion'
Write-Host "Reponse du panneau : HTTP $($r.StatusCode)" -ForegroundColor Green
Write-Host ""
Write-Host "Termine. Ouvre http://127.0.0.1:8080 dans ton navigateur et connecte-toi avec 'karim'."
Write-Host "Ajouter un membre : .venv\Scripts\python.exe panneau.py utilisateur ajouter <prenom>"
