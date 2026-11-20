# Projet 6 - ajoute la cle PUBLIQUE d'un membre de l'equipe au compte p6labo
# A lancer dans PowerShell EN ADMINISTRATEUR :
#   powershell -ExecutionPolicy Bypass -File D:\Labo-P6\acces\ajouter-cle.ps1 "ssh-ed25519 AAAA... prenom"
# La cle publique est la ligne unique du fichier id_ed25519.pub du membre (jamais le fichier sans .pub).
param([Parameter(Mandatory = $true)][string]$Cle)
$ErrorActionPreference = 'Stop'

$Cle = $Cle.Trim()
if ($Cle -notmatch '^(ssh-ed25519|ecdsa-sha2-nistp256|ssh-rsa) [A-Za-z0-9+/=]{40,}( .*)?$') {
    Write-Host "Ce n'est pas une cle publique SSH valide (elle doit commencer par ssh-ed25519)." -ForegroundColor Red
    exit 1
}
if ($Cle -match 'PRIVATE KEY') { Write-Host "C'est une cle PRIVEE : ne jamais la partager !" -ForegroundColor Red; exit 1 }

$fichier = Join-Path $env:ProgramData 'ssh\p6labo_authorized_keys'
$existantes = @(Get-Content $fichier -ErrorAction SilentlyContinue)
$corps = ($Cle -split ' ')[1]
if ($existantes | Where-Object { $_ -like "*$corps*" }) { Write-Host "Cette cle est deja autorisee."; exit 0 }

[IO.File]::AppendAllText($fichier, $Cle + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Host "Cle ajoutee. Cles autorisees :" -ForegroundColor Green
Get-Content $fichier | ForEach-Object { $p = $_ -split ' '; Write-Host ("  - {0} ...{1}  {2}" -f $p[0], $p[1].Substring($p[1].Length - 8), ($p[2..9] -join ' ')) }
