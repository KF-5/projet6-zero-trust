# Projet 6 - accueil d'un membre en une commande (PowerShell EN ADMINISTRATEUR, sur le PC du labo)
#   powershell -ExecutionPolicy Bypass -File D:\Labo-P6\acces\accueillir-membre.ps1 -Prenom ahmed -Role 4 -Cle "ssh-ed25519 AAAA... ahmed"
# Fait :  1. sa cle sur le compte tunnel p6labo (ajouter-cle.ps1)
#         2. son compte " membre " dans le panneau (mot de passe provisoire affiche une fois)
#         3. son compte Linux sur la ou les VM de son role (cle limitee au tunnel, sudo avec SON mot de passe)
#         4. son fichier de configuration SSH : D:\Labo-P6\acces\membres\<prenom>\
# Reste a la main (rappele a la fin) : partage Tailscale, invitation GitHub, compte OPNsense (roles 2 et 3).
# Les mots de passe sont affiches a l'ecran, jamais ecrits dans un fichier.
param(
  [Parameter(Mandatory = $true)][string]$Prenom,
  [Parameter(Mandatory = $true)][ValidateSet(2, 3, 4, 5, 6)][int]$Role,
  [Parameter(Mandatory = $true)][string]$Cle
)
$ErrorActionPreference = 'Stop'
$Prenom = $Prenom.Trim().ToLower(); $Cle = $Cle.Trim()
if ($Prenom -notmatch '^[a-z][a-z0-9_-]{1,19}$') { Write-Host "Prenom invalide (minuscules sans accent, ex. ahmed)." -ForegroundColor Red; exit 1 }
if ($Cle -notmatch '^(ssh-ed25519|ecdsa-sha2-nistp256|ssh-rsa) [A-Za-z0-9+/=]{40,}( .*)?$') { Write-Host "Cle publique invalide." -ForegroundColor Red; exit 1 }
$ici = $PSScriptRoot

# VM de chaque role (IP -> groupes Linux en plus de sudo et projet6)
$plan = @{
  2 = [ordered]@{ '10.99.0.10' = '-' }
  3 = [ordered]@{ '10.99.0.10' = '-' }
  4 = [ordered]@{ '10.99.0.20' = 'docker' }
  5 = [ordered]@{ '10.99.0.10' = '-'; '10.99.0.66' = '-' }
  6 = [ordered]@{ '10.99.0.10' = 'adm' }
}[$Role]
$noms = @{ '10.99.0.10' = 'p6-collecteur'; '10.99.0.20' = 'p6-docker'; '10.99.0.66' = 'p6-kali' }

function Mdp([int]$n) {
  $c = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
  $b = New-Object byte[] $n; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
  -join ($b | ForEach-Object { $c[$_ % $c.Length] })
}

Write-Host "=== Accueil de $Prenom (role $Role) ===" -ForegroundColor Cyan

# 1. Cle sur le compte tunnel
Write-Host "`n[1] Cle sur le compte tunnel p6labo" -ForegroundColor Cyan
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ici 'ajouter-cle.ps1') $Cle
if ($LASTEXITCODE -ne 0) { exit 1 }

# 2. Compte panneau " membre "
Write-Host "`n[2] Compte du panneau (role membre)" -ForegroundColor Cyan
$py = 'D:\Labo-P6\panneau\.venv\Scripts\python.exe'
$mdpPanneau = Mdp 16
$env:P6_MDP = $mdpPanneau
$code = @'
import os, sys
sys.path.insert(0, r"D:\Labo-P6\panneau"); os.chdir(r"D:\Labo-P6\panneau")
import panneau as p
n = sys.argv[1]; u = p.charger_utilisateurs()
if n in u:
    print("EXISTE")
else:
    u[n] = {"hash": p.generate_password_hash(os.environ["P6_MDP"]), "role": "membre"}
    p.enregistrer_utilisateurs(u); p.journal("console", "utilisateur ajoute", n, "membre"); print("CREE")
'@
$fpy = Join-Path $env:TEMP 'p6-ajout-panneau.py'
[IO.File]::WriteAllText($fpy, $code, (New-Object Text.UTF8Encoding($false)))
$res = & $py $fpy $Prenom
Remove-Item $fpy -ErrorAction SilentlyContinue
Remove-Item Env:\P6_MDP
if ($res -eq 'CREE') { Write-Host "    Compte panneau $Prenom cree. MOT DE PASSE PANNEAU = $mdpPanneau  (a donner en prive)" -ForegroundColor Yellow }
else { Write-Host "    Le compte panneau $Prenom existe deja (mot de passe inchange)."; $mdpPanneau = $null }

# 3. Comptes Linux
Write-Host "`n[3] Comptes Linux (le mot de passe de karim est demande pour chaque VM : copie, connexion, sudo)" -ForegroundColor Cyan
$tmp = Join-Path $env:TEMP "cle-$Prenom.pub"
[IO.File]::WriteAllText($tmp, $Cle + "`n", (New-Object Text.UTF8Encoding($false)))
foreach ($ip in $plan.Keys) {
  Write-Host "  -> $($noms[$ip]) ($ip)" -ForegroundColor Cyan
  scp -q -o StrictHostKeyChecking=accept-new (Join-Path $ici 'nouveau-membre.sh') $tmp "karim@${ip}:~/"
  if ($LASTEXITCODE -ne 0) { Write-Host "    Copie impossible vers $ip" -ForegroundColor Red; continue }
  ssh -t "karim@$ip" "sudo bash ~/nouveau-membre.sh $Prenom ~/cle-$Prenom.pub $($plan[$ip])"
}
Remove-Item $tmp -ErrorAction SilentlyContinue

# 4. Fichier de configuration SSH du membre
$dossier = Join-Path $ici "membres\$Prenom"; New-Item -ItemType Directory -Force $dossier | Out-Null
$cfg = @"
# Projet 6 - configuration SSH de $Prenom (role $Role)
# A AJOUTER a la fin de %USERPROFILE%\.ssh\config (cree le fichier s'il n'existe pas, sans extension).
# Cle utilisee : %USERPROFILE%\.ssh\p6labo (la tienne, creee avec ssh-keygen ; jamais partagee).

# 1) Les tunnels (console OPNsense, panneau, VNC) : ssh -N p6labo-tunnel   (laisser la fenetre ouverte)
Host p6labo-tunnel
    HostName 100.125.208.28
    User p6labo
    IdentityFile ~/.ssh/p6labo
    LocalForward 8443 10.99.0.1:443
    LocalForward 8080 127.0.0.1:8080
    LocalForward 5901 127.0.0.1:5901
    LocalForward 5902 127.0.0.1:5902
    LocalForward 5903 127.0.0.1:5903
    LocalForward 5904 127.0.0.1:5904

# 2) Le rebond (utilise automatiquement par les lignes suivantes)
Host p6labo
    HostName 100.125.208.28
    User p6labo
    IdentityFile ~/.ssh/p6labo

"@
foreach ($ip in $plan.Keys) {
  $cfg += @"
# 3) Ta VM : ssh $($noms[$ip])
Host $($noms[$ip])
    HostName $ip
    User $Prenom
    IdentityFile ~/.ssh/p6labo
    ProxyJump p6labo

"@
}
[IO.File]::WriteAllText((Join-Path $dossier 'config-ssh.txt'), $cfg, (New-Object Text.UTF8Encoding($false)))
Write-Host "`n[4] Fichier a envoyer a $Prenom : $dossier\config-ssh.txt" -ForegroundColor Cyan

# 5. Rappel de ce qui reste a la main
Write-Host "`n=== Reste a faire a la main pour $Prenom ===" -ForegroundColor Cyan
Write-Host "  - Tailscale : console Tailscale > machine best-technology > ... > Share > son adresse e-mail"
Write-Host "  - GitHub : github.com/KF-5/projet6-zero-trust > Settings > Collaborators > Add people"
if ($Role -in 2, 3) { Write-Host "  - OPNsense : System > Access > Users > + ; nom $Prenom ; droits limites a sa partie (pare-feu pour le role 2, Intrusion Detection pour le role 3), pas " All pages "" }
Write-Host "  - Lui envoyer EN PRIVE : mots de passe ci-dessus (panneau, Linux), mot de passe VNC ; et le fichier config-ssh.txt"
Write-Host "  - Premiere connexion : ssh $($noms[@($plan.Keys)[0]])  -> il tape le mot de passe provisoire Linux, puis en choisit un nouveau"
