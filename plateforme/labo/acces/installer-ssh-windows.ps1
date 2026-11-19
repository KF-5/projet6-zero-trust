# Projet 6 - etape 8.2 : serveur OpenSSH de l'hote Windows, reserve aux tunnels du labo
# A lancer dans PowerShell EN ADMINISTRATEUR :
#   powershell -ExecutionPolicy Bypass -File D:\Labo-P6\acces\installer-ssh-windows.ps1
# Relancable sans risque.
#
# Securite :
#  - compte local "p6labo" NON administrateur, mot de passe aleatoire inconnu de tous
#  - connexion par cle uniquement (les cles sont dans C:\ProgramData\ssh\p6labo_authorized_keys)
#  - pas de terminal : le compte ne peut ouvrir que des tunnels vers 4 destinations du labo
#  - pare-feu Windows : SSH accepte seulement depuis le reseau Tailscale 100.64.0.0/10

$ErrorActionPreference = 'Stop'
function Etape($t) { Write-Host ""; Write-Host "=== $t ===" -ForegroundColor Cyan }

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Lance ce script dans PowerShell 'Executer en tant qu'administrateur'." -ForegroundColor Red
    exit 1
}

Etape "1/6 Installation du serveur OpenSSH (fonctionnalite Windows)"
$cap = Get-WindowsCapability -Online -Name 'OpenSSH.Server*' | Select-Object -First 1
if ($cap.State -ne 'Installed') {
    Add-WindowsCapability -Online -Name $cap.Name | Out-Null
    Write-Host "OpenSSH Server installe."
} else { Write-Host "OpenSSH Server deja installe." }

Etape "2/6 Compte p6labo (non administrateur, mot de passe aleatoire)"
if (-not (Get-LocalUser -Name 'p6labo' -ErrorAction SilentlyContinue)) {
    $car = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#%+=?'
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $octets = New-Object byte[] 32; $rng.GetBytes($octets)
    $mdp = -join ($octets | ForEach-Object { $car[$_ % $car.Length] })
    $secure = ConvertTo-SecureString $mdp -AsPlainText -Force
    $mdp = $null
    New-LocalUser -Name 'p6labo' -Password $secure -PasswordNeverExpires -UserMayNotChangePassword `
        -FullName 'Labo P6 (tunnels SSH)' -Description 'Labo P6 : tunnels SSH par cle uniquement' | Out-Null
    Write-Host "Compte p6labo cree (mot de passe aleatoire, jamais affiche)."
} else { Write-Host "Compte p6labo deja present." }
# Groupe 'Utilisateurs' (SID universel S-1-5-32-545), jamais 'Administrateurs'
try { Add-LocalGroupMember -SID 'S-1-5-32-545' -Member 'p6labo' -ErrorAction Stop } catch { }
$admins = Get-LocalGroupMember -SID 'S-1-5-32-544' | Where-Object { $_.Name -like '*\p6labo' }
if ($admins) { throw "p6labo est administrateur : arret par securite." }
Write-Host "p6labo n'est pas administrateur : OK."

Etape "3/6 Fichier des cles autorisees (cles existantes conservees)"
$cles = Join-Path $env:ProgramData 'ssh\p6labo_authorized_keys'
New-Item -ItemType Directory -Force -Path (Split-Path $cles) | Out-Null
if (-not (Test-Path $cles)) { New-Item -ItemType File -Path $cles | Out-Null }
# Droits : seuls SYSTEM et Administrateurs (exige par OpenSSH ; p6labo ne peut pas modifier ses cles)
icacls $cles /inheritance:r /grant '*S-1-5-18:F' '*S-1-5-32-544:F' | Out-Null
Write-Host "Fichier : $cles"

Etape "4/6 Configuration de sshd"
$conf = Join-Path $env:ProgramData 'ssh\sshd_config'
# Premier demarrage : sshd genere ses cles d'hote (necessaires pour valider la configuration)
if (-not (Test-Path (Join-Path $env:ProgramData 'ssh\ssh_host_ed25519_key'))) {
    Start-Service sshd; Start-Sleep -Seconds 2; Stop-Service sshd
}
if ((Test-Path $conf) -and -not (Test-Path "$conf.origine")) { Copy-Item $conf "$conf.origine" }
$contenu = @'
# Projet 6 - serveur SSH de l'hote du labo (genere par installer-ssh-windows.ps1)
# Original sauvegarde dans sshd_config.origine
Port 22
PubkeyAuthentication yes
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitEmptyPasswords no
AllowUsers p6labo
X11Forwarding no
AllowAgentForwarding no
GatewayPorts no
PermitTunnel no
LogLevel VERBOSE

Match User p6labo
    AuthorizedKeysFile __PROGRAMDATA__/ssh/p6labo_authorized_keys
    AllowTcpForwarding local
    PermitOpen 10.99.0.1:443 10.99.0.10:22 10.99.0.20:22 10.99.0.66:22 127.0.0.1:8080 127.0.0.1:5901 127.0.0.1:5902 127.0.0.1:5903 127.0.0.1:5904
    PermitTTY no
    ForceCommand cmd.exe /c echo Compte reserve aux tunnels du labo P6.
'@
[IO.File]::WriteAllText($conf, $contenu, (New-Object Text.UTF8Encoding($false)))
& "$env:WINDIR\System32\OpenSSH\sshd.exe" -t
if ($LASTEXITCODE -ne 0) { throw "Configuration sshd invalide." }
Write-Host "Configuration sshd valide."

Etape "5/6 Pare-feu Windows : SSH seulement depuis Tailscale (100.64.0.0/10)"
$regle = Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue
if (-not $regle) {
    New-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -DisplayName 'OpenSSH Server (sshd)' `
        -Direction Inbound -Protocol TCP -LocalPort 22 -Action Allow | Out-Null
}
Set-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -RemoteAddress '100.64.0.0/10' -Profile Any -Enabled True
Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' | Get-NetFirewallAddressFilter |
    ForEach-Object { Write-Host "Adresses distantes autorisees : $($_.RemoteAddress)" }

Etape "6/6 Demarrage du service"
Set-Service sshd -StartupType Automatic
Restart-Service sshd
Get-Service sshd | Format-Table Name, Status, StartType -AutoSize

Write-Host ""
$nb = @(Get-Content $cles | Where-Object { $_ -match '^\s*ssh-' }).Count
if ($nb -eq 0) {
    Write-Host "Termine. Aucune cle n'est encore autorisee : personne ne peut se connecter pour l'instant." -ForegroundColor Green
} else {
    Write-Host "Termine. $nb cle(s) deja autorisee(s), conservee(s) telle(s) quelle(s)." -ForegroundColor Green
}
Write-Host "Etape suivante : ajouter les cles publiques avec ajouter-cle.ps1"
