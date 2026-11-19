# Projet 6 — Étapes détaillées par rôle

Extrait du guide de réalisation (version 1795100112-1abb, 17-19 nov. 2026). Pour chaque étape : but, pourquoi, prérequis, actions dans les interfaces, configuration et commandes, tests et sorties attendues, pannes connues, critère de fin, livrable.

## Sommaire

| Rôle | Étapes | Lignes de commande |
|---|---|---|
| Rôle 1 — Chef de projet et plateforme | 9 | 105 |
| Rôle 2 — Pare-feu et segmentation (C1, C2) | 9 | 146 |
| Rôle 3 — IDS/IPS Suricata et trafic légitime (C3, C4, C8) | 8 | 160 |
| Rôle 4 — Services applicatifs et capture (C5, C10) | 8 | 254 |
| Rôle 5 — Validation et attaques (C6, C7) | 6 | 210 |
| Rôle 6 — Observabilité et analyse (C9, C11, NTP) | 7 | 253 |

## Rôle 1 — Chef de projet et plateforme

**Mise à jour du 17 novembre : les étapes 1 à 5 sont faites.** Le laboratoire tourne sur VMware Workstation Pro, sur ton PC Windows. L'état A est construit et prouvé (`tests.sh` 12/12, `tests-cloisonnement.sh` 19/19), et l'équipe y accède par Tailscale, un tunnel SSH, le panneau de contrôle et les consoles VNC. Le détail pas à pas est dans le récapitulatif du rôle 1.

Il te reste : accueillir chaque membre (partage Tailscale, clé, compte panneau, compte OPNsense), le dépôt Git (étape 6), figer les états (7), intégrer (8), puis le rapport et la démonstration (9).

### Étape 1 — Installer VMware Workstation Pro et protéger le PC hôte

**Quand :** 14 nov. · **État :** ✅ déjà fait · **Prérequis :** modules 8

> **Mise à jour :** Étape réécrite le 17 novembre : elle décrit ce qui a été réellement fait. Proxmox, prévu à l'origine, a été écarté parce que son installateur efface le disque du PC (écart E31).

**But.** Un PC Windows qui fait tourner les 4 VM du laboratoire sans rien effacer, sans rien payer, et sans que le laboratoire puisse toucher Windows ni le réseau de la maison.

**Pourquoi.** Section 6 : tout le laboratoire tient sur un seul PC de 16 Go (ENF7). VMware Workstation Pro est gratuit, s'installe comme un logiciel ordinaire, et offre les réseaux isolés et les instantanés dont le projet a besoin. ENF1 : le laboratoire ne doit rien pouvoir atteindre hors de lui-même.

**Dans les interfaces**

- **Site de Broadcom (compte gratuit)** — Télécharger et installer **VMware Workstation Pro** (gratuit pour un usage personnel). L'ancien VMware Player a été désinstallé avant.
- **VMware › Modifier › Préférences** — Dossier des VM : `D:\Labo-P6\vm` (le disque C: est trop plein). Cocher « laisser les machines virtuelles s'exécuter après la fermeture ».
- **Windows › Paramètres › Réseau › Wi-Fi** — Profil du réseau domestique : **Public**. Windows se montre méfiant envers les autres appareils de la maison.
- **Pare-feu Windows › Règles de trafic entrant** — Règle « **P6 - bloquer le labo vers Windows** » : tout ce qui arrive depuis 10.99.0.0/24 est refusé. Même compromise, une VM ne peut pas attaquer Windows.
- **Sécurité Windows › Exclusions** — Exclure `D:\Labo-P6\vm` : Defender ne scanne pas les disques des VM (sinon il ralentit tout et supprime les outils de Kali).

**Configuration et commandes**

*Contrôles (PowerShell)*

```
Get-NetIPConfiguration | Select-Object InterfaceAlias, IPv4Address        # aucune adresse 10.x sur le Wi-Fi
Get-NetFirewallRule -DisplayName "P6 - bloquer le labo vers Windows" | Select-Object Enabled, Direction, Action
Get-NetIPInterface | Where-Object Forwarding -eq Enabled                     # rien : Windows ne route pas entre réseaux
```

**Tester**

**

```
Get-NetFirewallRule -DisplayName "P6 - bloquer le labo vers Windows" | Select-Object Enabled, Direction, Action
```
Sortie attendue :
```
Enabled Direction Action
------- --------- ------
   True   Inbound  Block
```
- `Inbound Block` : tout ce qui vient du laboratoire vers Windows est refusé ; les réponses à ce que Windows ouvre lui-même (navigateur, SSH) passent, car le pare-feu Windows est « stateful »

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Une VM refuse de démarrer : « VT-x is not available » | Hyper-V ou la sécurité basée sur la virtualisation de Windows occupe VT-x | Désactiver Hyper-V / « Isolation du noyau » ou laisser VMware utiliser l'hyperviseur Windows (plus lent). |
| Le disque C: se remplit | Dossier des VM resté sur C: | Préférences de VMware : `D:\Labo-P6\vm`, puis déplacer les VM existantes VM éteintes. |

**C'est fini quand**

- [ ] VMware Workstation Pro installé, VM dans `D:\Labo-P6\vm`
- [ ] Wi-Fi en profil Public, règle « P6 - bloquer le labo vers Windows » active
- [ ] Routage IP désactivé sur Windows (vérifié)

**À livrer.** Message à l'équipe : plateforme = VMware sur le PC du rôle 1 (écart E31 noté dans `ECARTS.md`).

### Étape 2 — Créer les réseaux virtuels p6-wan, p6-trunk et VMnet9

**Quand :** 14 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes chef-1 ; modules 3, 8

> **Mise à jour :** Remplace l'ancienne étape « ponts vmbr1, vmbr2, vmbr9 ». Les trois réseaux jouent exactement le même rôle ; seuls leurs noms changent.

**But.** Trois réseaux virtuels internes : un WAN simulé, un trunk 802.1Q pour les VLAN, et un réseau de gestion relié à Windows seulement.

**Pourquoi.** Section 6 et figure 3 : un WAN, un trunk qui transporte tous les VLAN internes, un MGMT hors bande. ENF1 : aucun de ces réseaux ne touche la carte Wi-Fi. Passer de l'état A à l'état B ne change jamais le câblage, seulement les VLAN.

**Dans les interfaces**

- **Paramètres d'une VM › Carte réseau › Segment LAN › Segments LAN… › Ajouter** — Créer `p6-wan` et `p6-trunk`. Un segment LAN ne relie **que des VM** : ni Windows ni Internet n'y ont accès. Il transporte les trames étiquetées 802.1Q sans rien régler.
- **Modifier › Éditeur de réseau virtuel › Ajouter un réseau › VMnet9** — Type **Hôte uniquement**, **DHCP décoché**, sous-réseau 10.99.0.0/24. Puis, dans Windows, donner à la carte « VMware Network Adapter VMnet9 » l'adresse fixe **10.99.0.2/24**, sans passerelle.
- **Règle permanente** — Jamais de mode **Pont** (VMnet0) : il brancherait une VM d'attaque directement sur la box de la maison. Le NAT de VMware (VMnet8) ne sert que pendant les installations (étape 4).

**Configuration et commandes**

*Table des réseaux*

```
Réseau     Type VMware                 Rôle                         Plage
p6-wan     segment LAN                 WAN simulé (aucun lien réel)  203.0.113.0/24
p6-trunk   segment LAN                 trunk 802.1Q (VLAN 10, 20…)   —
VMnet9     hôte uniquement, sans DHCP  MGMT ; Windows = 10.99.0.2     10.99.0.0/24
VMnet8     NAT                         Internet, installations seules —
```

**Tester**

*PowerShell*

```
Get-NetIPAddress -InterfaceAlias "*VMnet9*" -AddressFamily IPv4 | Select-Object IPAddress, PrefixLength
```
Sortie attendue :
```
IPAddress PrefixLength
--------- ------------
10.99.0.2           24
```
- `10.99.0.2/24` : Windows est sur MGMT, à une adresse fixe autorisée par l'alias ADMINS d'OPNsense

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Une VM obtient une adresse 192.168.x.x sur MGMT | DHCP de VMnet9 resté actif | Éditeur de réseau virtuel › VMnet9 › décocher « Utiliser le service DHCP local ». |
| Les VLAN ne passent pas sur le trunk | Carte reliée au mauvais segment | Vérifier dans les paramètres de la VM que la carte du trunk est sur le segment `p6-trunk`. |

**C'est fini quand**

- [ ] Segments LAN `p6-wan` et `p6-trunk` créés
- [ ] VMnet9 hôte uniquement sans DHCP, Windows en 10.99.0.2
- [ ] Aucune carte de VM en mode Pont

**À livrer.** Table des réseaux ci-dessus dans `docs/interfaces.md`.

### Étape 3 — Créer les 4 machines virtuelles

**Quand :** 14–16 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes chef-2 ; modules 8

> **Mise à jour :** Remplace la création par `qm create`. Les VM s'appellent `p6-opnsense`, `p6-docker`, `p6-collecteur` et `p6-kali` (et non 100 à 103). Leurs cartes sont des cartes VMware : `em0…em3` sous OPNsense, `ens32…ens35` sous Debian, `eth0…eth2` sous Kali.

**But.** Les 4 VM, avec leurs cartes dans le bon ordre et branchées sur le bon réseau, vérifiées une par une par leur adresse MAC.

**Pourquoi.** Section 6 : un pare-feu, un hôte Docker pour tous les services et postes, un attaquant, un collecteur. Les ressources ont été réduites pour tenir à l'aise dans 16 Go avec Windows (écart E32) : 7,5 Go pour les 4 VM.

**Configuration et commandes**

*Les 4 VM réellement créées*

```
VM             vCPU  RAM     Disque  Cartes (dans l'ordre)
p6-opnsense    2     3 Go    20 Go   em0 p6-wan · em1 p6-trunk · em2 VMnet9 · em3 NAT (débranchée)
p6-docker      2     2,5 Go  30 Go   ens32 VMnet9 · ens33 p6-trunk · ens34 p6-wan · ens35 NAT (débranchée)
p6-collecteur  1     1 Go    20 Go   ens32 VMnet9 · ens33 NAT (débranchée)
p6-kali        1     1,5 Go  25 Go   eth0 p6-wan · eth1 VMnet9 · eth2 NAT (débranchée)

Pour toutes : USB, son et imprimante retirés ; dossiers partagés désactivés ;
glisser-déposer et copier-coller désactivés (isolation de l'invité) ; jamais de disque physique.
```

*Vérifier qu'une carte est la bonne : comparer les MAC*

```
# Dans la VM (ex. OPNsense, console › 8 Shell)
ifconfig em2 | grep ether          # 00:0c:29:57:1f:b1
# Sur Windows : la même MAC doit figurer dans le .vmx, pour la carte reliée à VMnet9
Select-String -Path D:\Labo-P6\vm\p6-opnsense\p6-opnsense.vmx -Pattern 'ethernet\d\.(generatedAddress|vnet|connectionType)'
```

**Tester**

*PowerShell : les cartes NAT ne se branchent jamais au démarrage*

```
Select-String -Path D:\Labo-P6\vm\*\*.vmx -Pattern 'startConnected = "FALSE"'
```
Sortie attendue :
```
p6-collecteur.vmx: ethernet1.startConnected = "FALSE"
p6-docker.vmx: ethernet3.startConnected = "FALSE"
p6-kali.vmx: ethernet2.startConnected = "FALSE"
p6-opnsense.vmx: ethernet3.startConnected = "FALSE"
```
- `4 lignes` : chaque VM a sa carte NAT, et aucune ne se connecte toute seule

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| OPNsense affecte le WAN à la mauvaise carte | Ordre des cartes ≠ ordre attendu | Comparer les MAC (`ifconfig em0`) avec le .vmx, puis réaffecter (console, option 1). |
| La VM Debian démarre en synthèse vocale | Le menu de l'installateur a attendu 30 s | Redémarrer la VM et appuyer sur ↓ dès le menu. |

**C'est fini quand**

- [ ] 4 VM créées avec les ressources du tableau
- [ ] Chaque carte vérifiée par sa MAC
- [ ] Les 4 cartes NAT ont `startConnected = "FALSE"`

**À livrer.** Tableau des VM et des MAC dans `docs/interfaces.md`.

### Étape 4 — Installer les systèmes, les adresses MGMT et l'état A

**Quand :** 14–16 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes chef-3 ; modules 8

> **Mise à jour :** Le rôle 1 a aussi réalisé, pendant la préparation de l'hôte, les premières étapes des rôles 2 (OPNsense, VLAN, NAT, règles FA), 4 (Docker et services de l'état A) et 6 (NTP et syslog), pour que l'équipe démarre sur un état A complet et prouvé. Ces étapes portent la pastille « déjà fait » dans leurs rôles.

**But.** Les 4 systèmes installés, chaque VM à son adresse MGMT fixe, l'heure commune, les journaux centralisés, et l'état A en service.

**Pourquoi.** Section 6 : chaque VM est administrée par MGMT. Adresses MGMT fixées (écart E03) : OPNsense 10.99.0.1, collecteur 10.99.0.10, hôte Docker 10.99.0.20, Kali 10.99.0.66. Les noms eth0…eth2 du rapport deviennent ens32…ens35 sous Debian dans VMware (écart E15).

**Dans les interfaces**

- **Installateur Debian 13 (p6-docker, p6-collecteur)** — Pas de mot de passe root (utilisateur `karim` avec sudo), uniquement « serveur SSH » et « utilitaires usuels », aucun bureau. Noms : `docker-p6`, `collecteur`, domaine `p6.lab`.
- **Installateur Kali 2026.3 (p6-kali)** — Bureau (Xfce) et « default » décochés, « top10 » gardé : Kali reste en ligne de commande.
- **OPNsense (p6-opnsense)** — Installation, interfaces, VLAN 10 et 20, NAT FA1/FA2/FA6, règles FA3/FA4 : voir les étapes 1 à 5 du rôle 2, marquées « déjà fait ».
- **Carte NAT de la VM (installations seulement)** — Paramètres de la VM › carte NAT › cocher « Connecté » le temps des `apt` et `docker pull`, puis la décocher, ainsi que « Connecter à la mise sous tension ».

**Configuration et commandes**

*Adresse MGMT permanente (Debian, ex. collecteur)* — fichier `/etc/network/interfaces.d/p6-mgmt`

```
auto ens32
iface ens32 inet static
    address 10.99.0.10/24
```

> Pas de passerelle sur MGMT : ce réseau ne mène nulle part ailleurs.

*Scripts livrés par le rôle 1 (copiés depuis Windows avec scp, lancés avec sudo)*

```
D:\Labo-P6\collecteur\setup-collecteur.sh   # chrony serveur NTP (allow 10.99.0.0/24, local stratum 8) + rsyslog UDP 514 → /var/log/p6/<ip>.log
D:\Labo-P6\kali\setup-kali.sh               # adresses, outils (nmap, hping3, nikto…), chrony client, SSH écoute sur 10.99.0.66 seulement
D:\Labo-P6\docker\p6-etat-a\setup.sh       # réseaux macvlan p6_lan / p6_dmz / p6_wan et 10 conteneurs de l'état A
D:\Labo-P6\docker\p6-etat-a\tests.sh       # 12 tests des flux FA1 à FA5 depuis les conteneurs
```

*Copier un script dans une VM depuis Windows (PowerShell)*

```
scp -o StrictHostKeyChecking=accept-new D:\Labo-P6\kali\setup-kali.sh karim@10.99.0.66:~/
ssh -t karim@10.99.0.66 "sudo bash ~/setup-kali.sh"
```

**Tester**

*Hôte Docker*

```
ssh -t karim@10.99.0.20 "cd ~/p6-etat-a && sudo bash tests.sh"
```
Sortie attendue :
```
… 12 / 12 conformes
```
- `12 / 12` : FA1, FA2 (par NAT), FA3, FA4 passent ; les faiblesses voulues de l'état A existent ; FA5 bloque le reste. Vérifié le 16 et le 17 novembre, aussi après redémarrage

*Collecteur*

```
ssh karim@10.99.0.10 "chronyc clients; ls /var/log/p6/"
```
Sortie attendue :
```
10.99.0.1 … 10.99.0.20 … 10.99.0.66 …
10.99.0.1.log
```
- `chronyc clients` : OPNsense, l'hôte Docker et Kali prennent l'heure sur le collecteur
- `10.99.0.1.log` : les journaux d'OPNsense (filterlog) arrivent

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Un bloc de commandes collé dans PowerShell « perd » des lignes | `sudo` demande le mot de passe et avale la suite | `sudo -v` d'abord, puis une seule ligne avec `&&`. |
| Sur Kali, chrony ne tourne pas (« 506 Cannot talk to daemon ») | Kali désactive les services au démarrage | `sudo systemctl enable --now chrony` (corrigé dans le script). |
| OPNsense a des heures de retard | VM restée suspendue ; NTP refuse les très gros sauts | Services › Network Time : serveur 10.99.0.10 seul, puis redémarrer le service. |
| Tous les flux via le pare-feu échouent d'un coup | p6-opnsense est resté suspendu ou arrêté | Le démarrer en premier (panneau ou VMware), attendre 30 s, puis les autres. |

**C'est fini quand**

- [ ] 4 systèmes installés, adresses MGMT 10.99.0.1 / .10 / .20 / .66 permanentes
- [ ] NTP et syslog vers le collecteur vérifiés
- [ ] `tests.sh` = 12/12 sur l'hôte Docker
- [ ] Cartes NAT débranchées après les installations

**À livrer.** Instantanés de départ : `03-etat-A-sans-NAT` (docker), `01-collecteur-pret`, `01-kali-pret`. Message aux rôles 2, 4 et 6 : « état A en service, vos premières étapes sont faites, vérifiez-les ».

### Étape 5 — Verrouiller MGMT, prouver le confinement et ouvrir l'accès à l'équipe

**Quand :** 16–17 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes chef-4 ; modules 3, 8

> **Mise à jour :** Le verrouillage est celui du rapport, adapté : Windows (10.99.0.2) est, avec le collecteur, le seul à pouvoir ouvrir une session SSH sur MGMT. La preuve est un script lancé depuis Kali (19 tests). L'accès à distance de l'équipe remplace l'interface web de Proxmox.

**But.** Kali ne peut rien atteindre hors de ce qui est publié, MGMT ne sert à personne pour contourner le pare-feu, et les 5 autres membres travaillent sur le laboratoire depuis chez eux, sans mot de passe partagé.

**Pourquoi.** Section 2.3 : MGMT ne doit pas permettre de joindre une autre machine. Sur un même réseau, OPNsense ne voit pas les échanges directs entre VM : il faut un filtre sur chaque VM (écart E02). ENF1 : confinement prouvé. Accès à distance : sans Proxmox, il fallait un moyen gratuit et sûr d'accéder aux VM (écart E33).

**Dans les interfaces**

- **OPNsense › Firewall › Settings › Advanced (26.7)** — Cocher « Disable anti-lockout rule » : la règle automatique ouvrait la console web à tout MGMT, donc à Kali. L'accès passe désormais par la seule règle « Admin du pare-feu » (MGMT, source ADMINS = 10.99.0.2 et 10.99.0.10, destination This Firewall, ports 22 et 443, **port source any**).
- **OPNsense › Firewall › NAT › Destination NAT** — FA1 et FA2 : « Firewall rule » = **Pass** au lieu de « Register rule ». Avec Register rule, la règle créée autorisait « any → 10.20.0.10:80 » : Kali, dont la passerelle est le pare-feu, joignait le serveur directement, sans NAT.
- **Tailscale (hôte Windows)** — Compte gratuit du rôle 1, machine `best-technology`, adresse **100.125.208.28**. Chaque membre crée **son** compte ; le rôle 1 lui partage uniquement cette machine (⋯ › Share).
- **Serveur OpenSSH de Windows (script installer-ssh-windows.ps1, en administrateur)** — Compte `p6labo` sans droits d'administrateur, connexion **par clé uniquement**, aucun terminal (`PermitTTY no`, `ForceCommand`), tunnels limités par `PermitOpen`, pare-feu Windows limité aux adresses Tailscale 100.64.0.0/10. Une clé par membre : `ajouter-cle.ps1`.
- **Panneau de contrôle (D:\Labo-P6\panneau)** — Démarrer, prendre un instantané (rôle « membre ») ; arrêter, redémarrer, suspendre, revenir à un instantané (rôle « admin »). Écoute sur 127.0.0.1:8080 seulement, un compte par membre, chaque action journalisée. État mis à jour toutes les 3 s, mode sombre.
- **VMware › Paramètres de la VM › Options › Connexions VNC** — Console de chaque VM : ports 5901 (opnsense), 5902 (docker), 5903 (collecteur), 5904 (kali), avec mot de passe, et la ligne `RemoteDisplay.vnc.ip = "127.0.0.1"` dans chaque .vmx : la console n'est joignable que par le tunnel.

**Configuration et commandes**

*Filtre SSH sur p6-docker, p6-collecteur et p6-kali (écrit par restreindre-ssh.sh ; commentaires ajoutés ici)* — fichier `/etc/p6-ssh-filtre.nft`

```
# Table séparée : ne touche pas aux règles de Docker (jamais de « flush ruleset » sur l'hôte Docker)
table inet p6_ssh
delete table inet p6_ssh
table inet p6_ssh {
    chain entree {
        type filter hook input priority -10; policy accept;
        iifname "lo" accept
        tcp dport 22 ip saddr { 10.99.0.2, 10.99.0.10 } accept   # Windows et collecteur seulement
        tcp dport 22 counter log prefix "p6-ssh-refuse " drop    # tout autre essai est noté puis jeté
    }
}
```

> Rechargée à chaque démarrage par `p6-ssh-filtre.service`, avant le démarrage de SSH. Lancement : `sudo bash restreindre-ssh.sh` sur chacune des 3 VM.

*Tunnel d'un membre (PowerShell, sur son propre PC)*

```
ssh -i $HOME\.ssh\p6labo -N -L 8443:10.99.0.1:443 -L 8080:127.0.0.1:8080 -L 5901:127.0.0.1:5901 -L 5902:127.0.0.1:5902 -L 5903:127.0.0.1:5903 -L 5904:127.0.0.1:5904 p6labo@100.125.208.28
# puis : https://127.0.0.1:8443 (OPNsense) · http://127.0.0.1:8080 (panneau) · TightVNC Viewer 127.0.0.1::5901 à ::5904
```

**Tester**

*Depuis Kali (lecture seule, 19 tests)*

```
ssh -t karim@10.99.0.66 "sudo bash ~/tests-cloisonnement.sh"
```
Sortie attendue :
```
… box 192.168.1.1, 8.8.8.8, Internet : bloqués
… Windows ping / 445 / 3389 : bloqués
… console OPNsense, SSH docker et collecteur : bloqués
… FA1, FA2 par 203.0.113.1 : passent ; FA5, FA6 : bloqués
19 / 19 conformes
```
- `19 / 19` : premier passage 15/19 : quatre trous bouchés (anti-lockout, SSH sur 2 VM, NAT en Register rule). Revérifié le 17 novembre après l'ajout des consoles VNC

*Windows : les consoles VNC n'écoutent que sur la machine elle-même*

```
Get-NetTCPConnection -LocalPort 5901,5902,5903,5904 -State Listen | Select-Object LocalAddress,LocalPort
```
Sortie attendue :
```
127.0.0.1  5901
127.0.0.1  5902
127.0.0.1  5903
127.0.0.1  5904
```
- `127.0.0.1` : personne sur le réseau ne voit ces ports ; seul un tunnel SSH autorisé y mène

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Après avoir désactivé l'anti-lockout, Windows n'ouvre plus la console d'OPNsense | Règle « Admin du pare-feu » avec un port source ≠ any : elle ne correspondait jamais | Console OPNsense › 8 Shell › `pfctl -d`, corriger (port source any), Apply, `pfctl -e`. Toujours tester avec une connexion neuve. |
| Le tunnel s'ouvre mais la page affiche ERR_CONNECTION_CLOSED | Windows lui-même ne joint pas la cible | Tester chaque morceau : `Test-NetConnection 10.99.0.1 -Port 443` sur l'hôte. |
| « Arrêter » dans le panneau ne fait rien sur p6-opnsense | Pas de VMware Tools dans OPNsense | L'éteindre par sa console (option 5, puis y) ou Power › Power Off dans l'interface web. |
| TightVNC avertit « connexion non chiffrée » | Normal : VNC parle à 127.0.0.1 | Le flux passe dans le tunnel SSH, qui est chiffré : « Connect without encryption ». |

**C'est fini quand**

- [ ] `tests-cloisonnement.sh` = 19/19 depuis Kali
- [ ] Filtre `p6_ssh` actif sur les 3 VM, anti-lockout désactivée, règle admin corrigée
- [ ] Tunnel testé depuis un autre PC : OPNsense, panneau et consoles VNC
- [ ] Instantanés `07-vnc` (OPNsense) et `vnc-pret` (3 autres)

**À livrer.** Sorties des tests copiées dans `docs/preuve-confinement.txt` (citées dans le rapport final, écart E02). Message à l'équipe (PDF de 2 pages du 17 novembre, qui remplace celui du 16) : créer son compte Tailscale, générer sa clé `p6labo`, envoyer la ligne `.pub`.

### Étape 6 — Créer le dépôt Git et ses conventions

**Quand :** 15–16 nov. · **Prérequis :** étapes chef-4 ; modules 11

> **Mise à jour :** Les VM n'ont pas Internet : le paquet du dépôt (`git bundle`) se copie **depuis Windows** avec `scp` (SSH n'est accepté que depuis Windows 10.99.0.2 et le collecteur 10.99.0.10). Les comptes des VM sont `karim` avec `sudo`, pas `root` : les commandes ci-dessous utilisent donc `karim@`, et le dépôt est rangé dans `/opt/projet6` (chemin attendu par les scripts des autres rôles), appartenant à `karim`. Les scripts déjà livrés (`p6-etat-a`, `setup-*.sh`, tests) sont dans `D:\Labo-P6` : à commiter en premier.

**But.** Un dépôt unique, structuré, où chaque rôle commite ses fichiers et où chaque état est marqué par un tag.

**Pourquoi.** Section 6 : chaque état est figé par un snapshot et un tag Git (etat-A, etat-B) contenant config.xml, docker-compose.yml, flows.csv et les règles Suricata. ENF2 : reproductibilité.

**Configuration et commandes**

*Arborescence et premier commit (sur le PC d'un membre)*

```
git init projet6 && cd projet6
mkdir -p conception/uml docs plateforme/nftables opnsense politique services/{nginx,api,bind9,postgres} \
         ids sonde campagne capture analyse resultats/{A,B,synthese} rapport
cp ../conception/uml/*.puml conception/uml/
cat > .gitignore <<'EOF'
# captures trop lourdes : archivées à part (zip daté) ; pas de commentaire en fin de ligne dans .gitignore
resultats/**/pcap/
*.pyc
__pycache__/
.env
EOF
cat > README.md <<'EOF'
Projet 6 — Zero Trust. Une branche par rôle (role-1 … role-6), fusion dans main après relecture.
Tags : etat-A, etat-B (configurations figées), campagne-A, campagne-B (résultats archivés).
EOF
git add . && git commit -m "Structure du dépôt"
```

*Conventions*

```
# Branches : role-1-plateforme, role-2-parefeu, role-3-ids, role-4-services, role-5-attaques, role-6-analyse
# Message de commit : « [rôle N] verbe + objet », ex. « [rôle 2] Ajoute F04 à flows.csv »
# Fusion dans main : par le rôle 1, après un test (test_flux ou exécution à blanc)
# Jamais de mot de passe en clair : .env non versionné, modèle .env.exemple versionné
```

*Synchroniser les VM sans accès réseau (écart E26)*

```
# Sur le PC : paquet autonome du dépôt
git bundle create projet6.bundle --all
# Le copier depuis Windows sur le collecteur ET sur l'hôte Docker (SSH n'accepte que Windows et le collecteur) :
scp projet6.bundle karim@10.99.0.10:~/
scp projet6.bundle karim@10.99.0.20:~/
# Puis, sur chaque VM (git absent ? rebrancher la carte NAT le temps de « sudo apt install git ») :
sudo install -d -o karim -g karim /opt/projet6 && git clone ~/projet6.bundle /opt/projet6   # première fois
cd /opt/projet6 && git pull ~/projet6.bundle main                                              # les fois suivantes
```

**Tester**

**

```
git log --oneline | head -3
git ls-files | head
```
Sortie attendue :
```
a1b2c3d Structure du dépôt
.gitignore
README.md
conception/uml/01_topologie_etat_A.puml
…
```
- `conception/uml/…` : les diagrammes de référence sont versionnés

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Un PCAP de 500 Mo bloque le push | `git status` | Le retirer (`git rm --cached`), vérifier le .gitignore, archiver les PCAP en zip daté. |
| Deux rôles modifient flows.csv en même temps | Conflit de fusion | flows.csv appartient au rôle 2 : les autres proposent par message, le rôle 2 commite. |

**C'est fini quand**

- [ ] Dépôt créé, 6 membres ont accès
- [ ] Arborescence et conventions commitées
- [ ] /opt/projet6 présent sur le collecteur ET sur l'hôte Docker, synchronisation testée une fois

**À livrer.** Message à tous : adresse du dépôt, branches, conventions. Chaque rôle crée sa branche.

### Étape 7 — Figer un état : snapshots et tags

**Quand :** 21 et 26 nov. · **Prérequis :** étapes parefeu-7, attaques-5, parefeu-9, ids-6, attaques-6 ; modules 11

**But.** Les états A et B reconstructibles à l'identique : snapshot des 4 VM et tag Git portant sur le même contenu.

**Pourquoi.** Section 6 et 10.3 (étape 1 : « restaurer le snapshot de l'état testé »). Risque « démonstration qui échoue » (section 11.2) : les snapshots A et B sont la parade.

**Configuration et commandes**

*Figer l'état A (PowerShell, sur l'hôte Windows)*

```
# VMware enregistre disques + RAM : chaque VM reprend exactement où elle était (utile pour la démo)
$vr = 'C:\Program Files\VMware\VMware Workstation\vmrun.exe'
foreach ($v in 'p6-opnsense','p6-docker','p6-collecteur','p6-kali') {
  & $vr -T ws snapshot "D:\Labo-P6\vm\$v\$v.vmx" etat-A
}
& $vr -T ws listSnapshots D:\Labo-P6\vm\p6-opnsense\p6-opnsense.vmx
# Compter quelques minutes : la RAM des 4 VM (≈ 8 Go) est écrite sur D:.
# Panneau : « Prendre un instantané » sur chaque VM (le nom reçoit le préfixe de l'utilisateur et la date).
```

*Marquer le dépôt (PC)*

```
# Le rôle 2 a commité opnsense/config-etat-A.xml, le rôle 4 services/, le rôle 3 ids/
git checkout main && git pull
CAMP=resultats/A/2026-11-20        # dossier de la campagne A (rôle 5)
echo "commit $(git rev-parse --short HEAD) · $(date -u +%FT%TZ)" > $CAMP/VERSION.txt
git add -A && git commit -m "[rôle 1] Fige l'état A"
git tag -a etat-A -m "État A : FA1–FA6, VLAN 10/20, test_flux 100 %"
```

*Revenir à un état (PowerShell, ou « Revenir » dans le panneau, rôle admin)*

```
foreach ($v in 'p6-opnsense','p6-collecteur','p6-docker','p6-kali') {
  & $vr -T ws revertToSnapshot "D:\Labo-P6\vm\$v\$v.vmx" etat-A
}
# Une VM figée en marche revient suspendue : la redémarrer (« Démarrer » dans le panneau), OPNsense en premier.
# ATTENTION : tout ce qui n'a pas été figé depuis le dernier instantané est perdu.
```

**Tester**

**

```
& $vr -T ws listSnapshots D:\Labo-P6\vm\p6-opnsense\p6-opnsense.vmx
git tag -n
```
Sortie attendue :
```
Total snapshots: 9
01-opnsense-base
…
07-vnc
etat-A
etat-B
etat-A  État A : FA1–FA6, VLAN 10/20, test_flux 100 %
etat-B  État B : F01–F99, Suricata IPS, test_flux 100 %
```
- `arbre etat-A → etat-B` : etat-B est enfant de etat-A : on peut revenir à l'un ou à l'autre
- `git tag -n` : chaque snapshot a son tag, avec le même contenu

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| L'instantané dure plusieurs minutes et la VM semble figée | VMware écrit la RAM de la VM sur le disque | Attendre, ne pas interrompre (pas de Ctrl+C) : interrompre peut abîmer la VM. Vérifier l'activité du disque D: dans le Gestionnaire des tâches. |
| Après rollback, OPNsense a une heure fausse | Snapshot avec RAM restauré des jours plus tard | Laisser NTP resynchroniser ; contrôler avec le rôle 6 avant toute mesure. |
| Snapshot et tag ne correspondent pas | VERSION.txt absent | Toujours figer les deux dans la même demi-heure, et noter le commit dans la description du snapshot. |

**C'est fini quand**

- [ ] etat-A : 4 snapshots + tag + VERSION.txt
- [ ] etat-B : idem
- [ ] Une restauration complète de etat-A a été chronométrée (durée notée pour la démo)

**À livrer.** Message à tous : « etat-A figé (commit …) ». Données de campagne archivées par le rôle 5 sous `resultats/A/<date>/`, tag `campagne-A`.

### Étape 8 — Intégrer le travail des rôles aux jalons

**Quand :** 16–26 nov. · **Prérequis :** modules 0, 11

**But.** Chaque jalon de la section 12 est vérifié par un test de bout en bout, pas seulement annoncé.

**Pourquoi.** Section 12 : dates fixes ; en cas de retard, on réduit le périmètre (N, scénarios), jamais la comparaison A/B. Vous êtes le garant des interfaces entre rôles.

**Configuration et commandes**

*Contrôle de bout en bout (depuis le collecteur), à chaque jalon*

```
# 19 nov. : socle A
cd /opt/projet6
python3 politique/c2.py --etat A && scp politique/tests.csv politique/hosts.csv root@10.99.0.20:/opt/projet6/politique/
ssh root@10.99.0.20 'cd /opt/projet6 && ./politique/test_flux.sh politique/tests.csv politique/hosts.csv'   # rôle 2 : 0 écart
python3 campagne/orchestrateur.py --etat A --scenario S-01 --n 1 --dossier resultats/A/essai              # rôle 5
python3 analyse/parse.py --dossier resultats/A/essai                                                      # rôle 6
ls resultats/A/essai/runs.csv resultats/A/essai/events.csv
# 24 nov. : état B — mêmes commandes avec --etat B, plus :
python3 analyse/fp.py resultats/B/reglage/events.csv --duree-h 2                     # rôles 3 et 6 : ≤ 1 FP/h
```

*Point quotidien (15 min)*

```
Chaque rôle : fait hier · prévu aujourd'hui · bloqué par qui ?
Le rôle 1 met à jour l'état des jalons et décide d'une réduction de périmètre si un jalon glisse :
  1) N = 5 au lieu de 10 (section 11.2)   2) retirer S-06   3) jamais supprimer l'état A ou l'état B
```

**Tester**

**

```
(lecture du tableau des jalons)
```
Sortie attendue :
```
19 nov. ✔ test_flux A 100 % · ✔ runs.csv + events.csv pour S-01
21 nov. ✔ resultats/A/2026-11-20 archivés · ✔ tag etat-A
```
- `✔` : jalon vérifié par une commande, pas seulement déclaré

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Le rôle 5 attend les services | Tâche t42 en retard sur le Gantt | Aider le rôle 4 (le rôle 3, qui n'a que la sonde à écrire du 16 au 19, a de la marge) ; décaler l'essai, pas la campagne. |
| Un fichier ne respecte pas son contrat | Colonnes différentes de la page Contrats | Refuser la fusion ; le producteur corrige. |

**C'est fini quand**

- [ ] Chaque jalon de la section 12 a sa trace de vérification
- [ ] Aucun contrat de données modifié sans accord

**À livrer.** Journal des jalons dans `docs/jalons.md`, partagé avec le groupe chaque soir.

### Étape 9 — Assembler le rapport final et préparer la démonstration

**Quand :** 27–30 nov. · **Prérequis :** étapes analyse-6 ; modules 12

**But.** Un rapport final relu qui confirme ou infirme chaque hypothèse avec les mesures, et une démonstration de 10 minutes qui ne peut pas échouer.

**Pourquoi.** Section 12 : rapport + configurations + scripts + résultats le 30 novembre. Section 11.2 : snapshots A et B et vidéo de secours.

**Configuration et commandes**

*Plan du rapport final*

```
1. Résumé (résultats chiffrés A vs B)
2. Rappel de la conception et écarts appliqués (ECARTS.md)
3. Réalisation : plateforme (rôle 1), pare-feu (2), services (4), IDS/IPS (3)
4. Protocole et campagnes (rôle 5)
5. Résultats et analyse (rôle 6) : chaque hypothèse « attendu » confirmée ou infirmée
6. Limites, risques réalisés, pistes
Annexes : preuve de confinement, test_flux A et B, jeu de règles (hash), captures commentées
```

*Démonstration (10 min, deux états)*

```
Avant la séance : VM en etat-B démarrées ; vidéo de S-05 en état A prête (enregistrée le 26).
1. Carte de l'architecture (guide, accueil)                                  1 min
2. Vidéo : S-05 en état A — psql réussit, filterlog vide, PCAP du conteneur   2 min
3. Direct : S-05 en état B — délai dépassé, filterlog F99, alerte sid 9000020 2 min
4. Direct : S-01 en état B — alerte puis blocage temporaire                   2 min
5. Tableaux et graphiques A vs B ; une limite assumée                        3 min
Secours : vidéo complète des deux états, enregistrée le 29.
```

**Tester**

**

```
(répétition générale chronométrée le 29)
```
Sortie attendue :
```
Durée : 9 min 40 · aucune commande tapée à la main · vidéo de secours testée sur le vidéoprojecteur
```
- `aucune commande tapée` : les commandes de la démo sont dans des scripts : pas de faute de frappe en direct

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Une VM ne démarre pas pendant la démo | — | Passer immédiatement à la vidéo de secours, ne pas dépanner en direct. |
| Les chiffres du rapport ne correspondent pas aux graphiques | Graphiques régénérés après correction | Tout produire depuis resultats/synthese/ (rôle 6), jamais à la main. |

**C'est fini quand**

- [ ] Rapport relu par au moins deux membres
- [ ] Chaque « attendu » de la section 10.1 a son verdict mesuré
- [ ] Démo répétée deux fois, vidéo de secours testée
- [ ] Tag final `remise` poussé

**À livrer.** Dépôt final (tag `remise`) : rapport, configurations, scripts, résultats. Envoi selon les consignes du sujet le 30 novembre.

### Questions probables du jury

1. **Comment prouvez-vous que le laboratoire est isolé ?**
   Trois preuves : les réseaux du laboratoire sont des segments LAN VMware et un réseau « hôte uniquement », aucune carte n'est en mode Pont ; toutes les cartes NAT sont débranchées et ne se reconnectent pas au démarrage (`startConnected = "FALSE"` dans chaque .vmx) ; et depuis Kali, 19 tests sur 19 échouent comme prévu vers la box, Internet, Windows et MGMT (sorties dans `docs/preuve-confinement.txt`). Les téléchargements se sont faits pendant une fenêtre d'approvisionnement documentée.

2. **Pourquoi un pare-feu local sur chaque VM alors que vous avez OPNsense ?**
   Parce que les VM d'un même réseau se parlent sans passer par OPNsense : c'est exactement le problème du LAN plat de l'état A. Le rapport promet que MGMT ne peut pas servir à joindre une autre machine ; seul un filtrage sur chaque VM le garantit.

3. **Pourquoi VMware plutôt que Proxmox, prévu dans la conception ?**
   L'installateur de Proxmox efface tout le disque du PC, ce qui n'était pas acceptable sur un PC personnel. VMware Workstation Pro est gratuit, tourne sous Windows et offre ce que le projet demandait : réseaux isolés, trunk 802.1Q, instantanés, pilotage en ligne de commande (`vmrun`). Ce qui manquait (accès web pour l'équipe, consoles) a été remplacé par Tailscale, un tunnel SSH, un panneau de contrôle et des consoles VNC (écarts E31 et E33).

4. **Comment garantissez-vous que l'état mesuré est bien celui décrit ?**
   Chaque campagne commence par la restauration du snapshot et un test_flux à 100 % (section 10.3). Le snapshot et le tag Git citent le même commit ; VERSION.txt est archivé avec les résultats.

5. **Que se passe-t-il si la RAM manque ?**
   L'hôte échange sur disque et la latence mesurée devient fausse. Les 4 VM utilisent 7,5 Go sur 16 (écart E32, au lieu des 11 Go prévus), ce qui laisse environ 8 Go à Windows ; aucune autre VM ne tourne pendant les campagnes.

### Démonstration finale (2 minutes)

Ce que vous montrez en 2 minutes : la plateforme est isolée et reproductible.

1. Panneau de contrôle (par le tunnel) : les 4 VM, leurs instantanés et le journal des actions ; dans VMware, aucune carte en mode Pont.
2. Depuis Kali : `sudo bash tests-cloisonnement.sh` → 19/19 (box, Internet, Windows, MGMT injoignables ; seuls FA1 et FA2 passent).
3. Liste des instantanés (panneau ou `vmrun listSnapshots`) et `git tag -n` : etat-A et etat-B liés au même contenu.
4. Annoncer la durée mesurée d'une restauration.

**Plan de secours :** captures d'écran des trois commandes dans le rapport.

## Rôle 2 — Pare-feu et segmentation (C1, C2)

**OPNsense 26.7 a changé plusieurs pages** : affectation des interfaces (nouvelle page), règles (nouvelle interface, anciennes pages déplacées dans un greffon), « Port Forward » renommé « Destination NAT ». Les chemins marqués « à vérifier » doivent être confirmés sur votre écran. Si un menu n'existe pas, cherchez l'option par son nom dans la barre de recherche de l'interface.

**Mise à jour du 17 novembre.** Les étapes 1 à 5 (installation, WAN/MGMT, VLAN 10 et 20, NAT, règles FA, journalisation) ont été réalisées par le rôle 1 pendant la préparation de l'hôte : vérifie-les, prends tes captures, puis enchaîne sur l'étape 6 (moteur C2). Cartes d'OPNsense sous VMware : `em0` = WAN 203.0.113.1, `em1` = trunk (porte `vlan01` LAN et `vlan02` DMZ), `em2` = MGMT 10.99.0.1, `em3` = carte NAT débranchée. Il n'y a **pas** d'interface CONF : l'interface web s'ouvre sur `https://10.99.0.1` depuis Windows, ou sur `https://127.0.0.1:8443` par le tunnel. Demande au rôle 1 ton propre compte OPNsense (le mot de passe root n'est pas partagé).

### Étape 1 — Installer OPNsense 26.7 et affecter les interfaces

**Quand :** 14 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes chef-3 ; modules 5, 8

> **Mise à jour :** Fait par le rôle 1 le 14 novembre, dans VMware. Ce qui diffère du texte d'origine : console de la VM dans VMware (ou VNC 5901 par le tunnel) au lieu de noVNC ; cartes `em0…em3` au lieu de `vtnet0…vtnet3` ; pas de carte CONF : le « LAN » d'OPNsense a été placé sur `em2` (MGMT, 10.99.0.1) puis renommé MGMT, ce qui n'ouvre l'interface web que du côté gestion.

**But.** OPNsense 26.7 installé dans p6-opnsense, chaque carte affectée à la bonne interface, l'interface web joignable depuis le réseau de gestion seulement.

**Pourquoi.** Section 4.1 : le pare-feu est le seul point de passage entre zones (C1). L'interface web ne doit jamais être joignable depuis le WAN ni depuis les zones évaluées.

**Dans les interfaces**

- **Console de p6-opnsense (VMware, ou TightVNC sur 127.0.0.1::5901)** — Ce qui a été fait : démarrage sur l'ISO, connexion `installer` / `opnsense`, clavier français, installation **UFS** sur le disque de 20 Go (da0), mot de passe root (connu du rôle 1 seulement), arrêt, ISO éjectée dans VMware.
- **Console OPNsense › 1) Assign interfaces** — Pas de LAGG, pas de VLAN à ce stade. WAN = `em0`, LAN = `em2` (devenu MGMT).
- **Console OPNsense › 2) Set interface IP address** — LAN (MGMT) : `10.99.0.1/24`, sans DHCP ni IPv6. WAN : `203.0.113.1/24`.
- **Assistant web (https://10.99.0.1)** — Nom `fw-p6`, domaine `p6.lab`, fuseau Africa/Tunis, Override DNS décoché ; WAN statique (passerelle fictive 203.0.113.254), « Block private » et « Block bogon » décochés ; DHCP du LAN désactivé.

**Configuration et commandes**

*Vérifier une carte par sa MAC (shell OPNsense, option 8)*

```
ifconfig em2 | grep ether      # 00:0c:29:57:1f:b1 = carte 3 de VMware, reliée à VMnet9
```

**Tester**

**

```
# Console OPNsense, option 8 (shell)
ifconfig -l
```
Sortie attendue :
```
em0 em1 em2 em3 lo0 pflog0 pfsync0 vlan01 vlan02
```
- `em0 … em3` : les 4 cartes VMware sont reconnues
- `vlan01 vlan02` : LAN et DMZ (étape 3)
- `pflog0` : l'interface interne qui alimente filterlog

*Depuis Windows (PC du laboratoire)*

```
Test-NetConnection 10.99.0.1 -Port 443
```
Sortie attendue :
```
TcpTestSucceeded : True
```
- `True` : l'interface web répond sur MGMT ; depuis Kali, le même test échoue (tests-cloisonnement.sh)

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| L'interface web ne répond plus depuis Windows | Règle « Admin du pare-feu » (port source ≠ any) ou anti-lockout | Console › 8 Shell › `pfctl -d`, corriger la règle, Apply, `pfctl -e` (rôle 1, étape 5). |
| Les cartes ne sont pas dans l'ordre attendu | Comparer les MAC (`ifconfig em0`) avec le fichier .vmx | Réaffecter à la console (option 1). |

**C'est fini quand**

- [ ] OPNsense 26.7 installé, ISO retirée
- [ ] em0 = WAN, em2 = MGMT, em1 porte les VLAN
- [ ] Interface web accessible depuis Windows (10.99.0.2) et par le tunnel, pas depuis Kali

**À livrer.** Version exacte (System › Firmware) notée dans `docs/versions.md`. Capture `parefeu-01-version.png`.

### Étape 2 — Configurer WAN, MGMT, offloading, SSH et l'heure

**Quand :** 14–15 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes parefeu-1 ; modules 4, 7

> **Mise à jour :** Fait par le rôle 1 (14-16 novembre). Différences : MGMT est sur `em2` (ancien « LAN » renommé), le trunk sur `em1`. Le **SSH d'OPNsense est désactivé** : l'administration passe par l'interface web. Sur MGMT, une seule règle : « Admin du pare-feu », source alias `ADMINS` (10.99.0.2 Windows, 10.99.0.10 collecteur), destination This Firewall, TCP 22 et 443, **port source any**. Les règles « Default allow LAN » de MGMT sont désactivées et la règle anti-lockout aussi (elle ouvrait la console à Kali). Heure : serveur 10.99.0.10 seul. Déchargements CRC, TSO, LRO et VLAN Hardware Filtering désactivés, redémarrage fait. Test à faire : `Test-NetConnection 10.99.0.1 -Port 443` depuis Windows = True ; depuis Kali, `tests-cloisonnement.sh` le prouve bloqué.

**But.** WAN en 203.0.113.1/24, MGMT en 10.99.0.1/24, offloading désactivé, SSH accessible depuis le collecteur seulement, heure synchronisée.

**Pourquoi.** Section 4.1 (adresses), section 5.3 (offloading désactivé pour le mode IPS), section 2.3 (MGMT : SSH depuis le collecteur, syslog et NTP vers lui). Le WAN simulé utilise une plage réservée : l'option bogons le bloquerait (écart E04).

**Dans les interfaces**

- **Interfaces › [WAN]** — IPv4 : statique, `203.0.113.1/24`. Passerelle amont : aucune. **Décocher** « Block private networks » et « Block bogon networks » (203.0.113.0/24 est une plage réservée, présente dans les listes bogons). IPv6 : aucun.
- **Interfaces › Assignments** — Activer l'interface de `em2`, description `MGMT`, IPv4 statique `10.99.0.1/24`, sans passerelle. Activer celle de `em1`, description `TRUNK`, IPv4 : aucun (elle servira au rôle 3 pour l'IDS).
- **Interfaces › Settings** — Désactiver le déchargement matériel : cocher « Disable hardware CRC », « Disable hardware TSO », « Disable hardware LRO », et désactiver « VLAN Hardware Filtering ». Enregistrer puis redémarrer (manuel IPS d'OPNsense).
- **System › Settings › Administration** — Secure Shell : activé, connexion root autorisée par clé seulement, interface d'écoute : MGMT (et CONF si besoin). Ajouter la clé publique du collecteur au compte root (System › Access › Users).
- **Services › Network Time › General** — Serveur de temps : `10.99.0.10` (le collecteur, rôle 6). Supprimer les serveurs publics.
- **Firewall › Rules › MGMT** — Une seule règle : pass, TCP, source `10.99.0.10`, destination « This Firewall », port 22, journalisée. Rien d'autre : syslog et NTP sont émis par OPNsense lui-même (trafic sortant du pare-feu autorisé).

**Tester**

*Depuis le collecteur*

```
ssh root@10.99.0.1 'ifconfig em0 | grep -E "inet|options"; ntpq -p 2>/dev/null | tail -1'
```
Sortie attendue :
```
	options=…<…,LINKSTATE>   (sans TXCSUM, TSO4, LRO)
	inet 203.0.113.1 netmask 0xffffff00 broadcast 203.0.113.255
*10.99.0.10      .LOCL.  …
```
- `options sans TXCSUM, TSO4, LRO` : offloading bien désactivé
- `inet 203.0.113.1` : adresse WAN correcte
- `*10.99.0.10` : l'heure vient du collecteur

*Depuis Kali*

```
nc -zv -w 2 10.99.0.1 22
nc -zv -w 2 10.99.0.1 443
```
Sortie attendue :
```
nc: connect to 10.99.0.1 port 22 (tcp) timed out
nc: connect to 10.99.0.1 port 443 (tcp) timed out
```
- `timed out` : OPNsense a jeté le paquet : seuls Windows (10.99.0.2) et le collecteur (10.99.0.10) peuvent administrer le pare-feu. Le filtre local de Kali (table p6_ssh) ne bloque que l'entrée : il ne fausse pas ce test

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Kali ne joint rien sur 203.0.113.1 | Firewall › Log Files › Live View : lignes « bogon » ou « private » | Décocher les deux options sur WAN (écart E04). |
| SSH refusé depuis le collecteur | Clé absente ou SSH n'écoute pas sur MGMT | Ajouter la clé, choisir MGMT comme interface d'écoute. |
| Les options TXCSUM restent visibles | Redémarrage non fait | Redémarrer p6-opnsense après le changement. |

**C'est fini quand**

- [ ] WAN 203.0.113.1/24, sans bogons, sans passerelle
- [ ] MGMT 10.99.0.1/24 ; TRUNK affecté sans adresse
- [ ] Offloading désactivé (vérifié par ifconfig)
- [ ] SSH : collecteur oui, Kali non
- [ ] Heure synchronisée sur 10.99.0.10

**À livrer.** Message au rôle 3 : « OPNsense prêt pour le test netmap : WAN et TRUNK affectés, offloading désactivé ». Export de la configuration (System › Configuration › Backups) commité dans `opnsense/config-base.xml`.

### Étape 3 — État A : créer les VLAN 10 et 20

**Quand :** 16 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes parefeu-2, chef-2 ; modules 3, 2

> **Mise à jour :** Fait par le rôle 1. `vlan01` (étiquette 10, LAN 10.10.0.1/24) et `vlan02` (étiquette 20, DMZ 10.20.0.1/24) sur le parent `em1`. Côté hôte Docker, les sous-interfaces sont `ens33.10` et `ens33.20`. Pas de « pont VLAN-aware » à vérifier : le segment LAN `p6-trunk` de VMware laisse passer les étiquettes.

**But.** Deux sous-interfaces VLAN sur em1 : LAN (10.10.0.1/24, VLAN 10) et DMZ (10.20.0.1/24, VLAN 20).

**Pourquoi.** Section 4.1 : DMZ 10.20.0.0/24 VLAN 20, LAN 10.10.0.0/24 VLAN 10, passerelles en .1. Section 6 : un trunk 802.1Q transporte tous les VLAN internes.

**Dans les interfaces**

- **Interfaces › Devices › VLAN** — Ajouter : parent `em1`, tag `10`, description `LAN-A`. Puis parent `em1`, tag `20`, description `DMZ-A`.
- **Interfaces › Assignments** — Ajouter les deux périphériques VLAN, les activer. Description `LAN` → IPv4 statique `10.10.0.1/24`. Description `DMZ` → IPv4 statique `10.20.0.1/24`. Pas de passerelle.

**Tester**

*Shell OPNsense*

```
ifconfig -l
ifconfig | grep -A3 'vlan: 10'
```
Sortie attendue :
```
em0 em1 em2 em3 lo0 pflog0 pfsync0 vlan01 vlan02
	vlan: 10 vlanproto: 802.1q vlanpcp: 0 parent interface: em1
	inet 10.10.0.1 netmask 0xffffff00 …
```
- `vlan0.10` : nom du périphérique VLAN (le format exact dépend de la version)
- `parent interface: em1` : la sous-interface s'appuie bien sur le trunk

*Avec un conteneur de test du rôle 4*

```
# sur l'hôte Docker
docker run --rm --network lan --ip 10.10.0.50 projet6/outils ping -c 2 10.10.0.1
# sur OPNsense, en même temps (les options avant le filtre)
tcpdump -nei em1 -c 2 vlan 10 and icmp
```
Sortie attendue :
```
64 bytes from 10.10.0.1: icmp_seq=1 ttl=64 time=0.4 ms
… ethertype 802.1Q (0x8100), length 102: vlan 10, p 0, ethertype IPv4, 10.10.0.50 > 10.10.0.1: ICMP echo request
```
- `vlan 10` : la trame arrive étiquetée sur le trunk
- `ttl=64` : la passerelle répond (en état A, FA4 autorise le LAN vers le pare-feu)

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Pas de réponse, et tcpdump ne voit rien | Paramètres de la VM dans VMware : la carte du trunk est-elle sur le segment `p6-trunk` ? | Mauvais segment : rôle 1, étape 2. |
| tcpdump voit la requête mais pas de réponse | Interface VLAN non activée ou sans adresse | Activer l'interface, vérifier 10.10.0.1/24. |
| « overlapping networks » à l'enregistrement | Une autre interface porte déjà ce réseau | Interfaces › Overview : aucune autre interface ne doit être en 10.10.0.0/24 ou 10.20.0.0/24. |

**C'est fini quand**

- [ ] VLAN 10 et 20 sur em1
- [ ] LAN 10.10.0.1/24 et DMZ 10.20.0.1/24 actifs
- [ ] Un conteneur de test ping sa passerelle, trame vue étiquetée sur em1

**À livrer.** Message au rôle 4 : « VLAN 10 et 20 actifs, passerelles 10.10.0.1 et 10.20.0.1 ». Export de la configuration commité.

### Étape 4 — Configurer le NAT de destination (FA1, FA2, FA6)

**Quand :** 16–17 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes parefeu-3 ; modules 4

> **Mise à jour :** Fait par le rôle 1 : FA1 HTTP, FA1 HTTPS et FA2 DNS actifs et testés, FA6 iperf3 désactivée (alias `CLIENT_EXT` et `PORT_IPERF3`). Choix final pour FA1 et FA2 : « Firewall rule » = **Pass**. Avec « Register rule », la règle de filtrage associée autorisait « any → 10.20.0.10:80 » : Kali, dont la passerelle est le pare-feu, joignait le serveur directement, sans traduction (trouvaille à citer dans ta section du rapport).

**But.** Les ports publiés sur 203.0.113.1 redirigés vers nginx et bind9 ; la redirection iperf3 prête mais désactivée.

**Pourquoi.** Section 4.2 : FA1 (TCP 80, 443 → nginx, NAT depuis 203.0.113.1), FA2 (UDP/TCP 53 → bind9), FA6 (TCP 5201 depuis client-ext, activée seulement pendant les mesures). Identiques en état B (F01, F02, F09).

**Dans les interfaces**

- **Firewall › NAT › Destination NAT** — Ajouter quatre règles sur l'interface WAN, destination « WAN address » :
· TCP 80 → `10.20.0.10` port 80, description « FA1 / F01 web »
· TCP 443 → `10.20.0.10` port 443, « FA1 / F01 web »
· TCP/UDP 53 → `10.20.0.40` port 53, « FA2 / F02 dns »
· TCP 5201, source `203.0.113.10` → `10.20.0.10` port 5201, « FA6 / F09 iperf3 », **désactivée**
Règle de filtrage associée : **aucune**. Les règles de filtrage sont écrites à l'étape 5 et générées par C2 : on garde la maîtrise de leur nom, de leur ordre et de leur journalisation.

**Tester**

*Shell OPNsense*

```
pfctl -s nat | grep rdr
```
Sortie attendue :
```
rdr on em0 inet proto tcp from any to 203.0.113.1 port = http -> 10.20.0.10
rdr on em0 inet proto tcp from any to 203.0.113.1 port = https -> 10.20.0.10
rdr on em0 inet proto { tcp udp } from any to 203.0.113.1 port = domain -> 10.20.0.40
```
- `rdr on em0` : redirection appliquée aux paquets entrant par le WAN
- `-> 10.20.0.10` : adresse interne de nginx ; la règle de filtrage devra viser cette adresse
- `pas de ligne 5201` : FA6 est bien désactivée

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Kali obtient un délai dépassé sur 443 | Rdr présent mais aucune règle de filtrage | Normal avant l'étape 5 : la règle FA1 doit autoriser 10.20.0.10:443. |
| La règle WAN autorise 203.0.113.1 et rien ne passe | La redirection est appliquée avant le filtrage | Écrire la règle vers l'adresse interne (module 4). |
| La règle iperf3 est active hors mesure | `pfctl -s nat \| grep 5201` | La désactiver : elle n'est allumée que pendant les mesures du rôle 3. |

**C'est fini quand**

- [ ] Trois redirections actives (80, 443, 53), une désactivée (5201)
- [ ] Aucune règle de filtrage associée automatique

**À livrer.** Export de la configuration commité. Message au rôle 3 : « redirection 5201 prête, à activer seulement pendant les mesures iperf3 ».

### Étape 5 — Écrire les règles FA et activer la journalisation

**Quand :** 17 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes parefeu-4, analyse-2 ; modules 5

> **Mise à jour :** Fait par le rôle 1 : FA3 « DMZ net → POSTGRES:PORT_PGSQL » sur DMZ, FA4 « LAN net → any » sur LAN (recréée le 16), refus par défaut journalisé, envoi des journaux vers 10.99.0.10 UDP 514 (lignes `filterlog` reçues sur le collecteur). Preuve : `tests.sh` 12/12 sur l'hôte Docker. Référence de l'état A : instantané OPNsense `07-vnc`. Pense à exporter la configuration (System › Configuration › Backups) dans `opnsense/config-etat-A.xml`.

**But.** La matrice FA appliquée, chaque règle journalisée, le refus par défaut journalisé, et filterlog envoyé au collecteur.

**Pourquoi.** Section 4.2 (matrice FA) et section 7.1 : première correspondance, pass journalisé, block final journalisé dans filterlog, export syslog vers C9.

**Dans les interfaces**

- **Firewall › Rules › WAN** — FA1 : pass, TCP, any → `10.20.0.10`, ports 80 et 443, journaliser. FA2 : pass, TCP/UDP, any → `10.20.0.40`, port 53, journaliser. FA6 : pass, TCP, `203.0.113.10` → `10.20.0.10`, port 5201, journaliser, **désactivée**.
- **Firewall › Rules › DMZ** — FA3 : pass, TCP, DMZ net → `10.10.0.70`, port 5432, journaliser.
- **Firewall › Rules › LAN** — FA4 : pass, tout protocole, LAN net → any, journaliser (la règle « LAN vers tout » de l'état A).
- **Firewall › Settings › Advanced (ou System › Settings › Logging)** — Activer la journalisation des paquets bloqués par la règle par défaut : c'est FA5 (puis F99).
- **System › Settings › Logging › Remote / Targets** — Nouvelle cible : transport UDP (ou TCP, écart E13), applications `filter (filterlog)` et `suricata`, hôte `10.99.0.10`, port 514. Garder aussi la journalisation locale si l'option est proposée.

**Tester**

*Shell OPNsense*

```
pfctl -sr | grep -c pass
tail -n 3 /var/log/filter/latest.log
```
Sortie attendue :
```
… (nombre de règles pass, dont les règles système)
<134>1 2026-11-17T15:02:11+01:00 opnsense filterlog … 77,,,fae5…,em0,match,block,in,4,0x0,,64,…,tcp,…,203.0.113.66,203.0.113.1,40001,22,0,S,…
```
- `match,block,in` : paquet bloqué par la règle par défaut (FA5), en entrée
- `203.0.113.66 … 22` : Kali vers le port 22 non publié

*Depuis Kali*

```
nc -zv -w 2 203.0.113.1 443
nc -zv -w 2 203.0.113.1 22
```
Sortie attendue :
```
Connection to 203.0.113.1 443 port [tcp/https] succeeded!   (si nginx tourne)
nc: connect to 203.0.113.1 port 22 (tcp) timed out
```
- `succeeded` : FA1 + redirection
- `timed out` : refus par défaut, en silence (block) : nmap verra « filtered »

*Sur le collecteur (rôle 6)*

```
tail -n 2 /var/log/remote/opnsense.log
```
Sortie attendue :
```
… filterlog[…]: 77,,,fae5…,em0,match,block,in,4,…
```
- `filterlog` : les journaux arrivent bien sur le collecteur

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Aucune ligne pour les paquets bloqués | Journalisation de la règle par défaut désactivée | Activer l'option (FA5 doit être journalisée). |
| Le collecteur ne reçoit rien | `tcpdump -ni em2 port 514` sur OPNsense | Vérifier la cible syslog, et le pare-feu du collecteur (rôle 1, étape 5). |
| Une règle semble ignorée | Elle est sur la mauvaise interface ou après une règle plus générale | Les règles s'écrivent sur l'interface d'entrée ; première correspondance (module 5). |

**C'est fini quand**

- [ ] FA1 à FA4 actives et journalisées, FA6 désactivée
- [ ] Le refus par défaut est journalisé
- [ ] filterlog arrive sur le collecteur

**À livrer.** Export `opnsense/config-etat-A.xml` (provisoire) commité. Message au rôle 6 : « filterlog part vers 10.99.0.10:514 ; voici une ligne d'exemple ».

### Étape 6 — Écrire le moteur de politique C2

**Quand :** 17–18 nov. · **Prérequis :** modules 5, 6

**But.** Un programme qui valide flows.csv, en déduit les règles dans le bon ordre et écrit tests.csv.

**Pourquoi.** Section 1 (décision 2 : la politique est une donnée), section 7.2 et figure 11 : valider, grouper par interface d'entrée, trier du plus spécifique au plus général, pass journalisé par flux, block final ; plan de test « doit passer », « doit être bloqué », « mauvais port ».

**Configuration et commandes**

*Programme complet* — fichier `politique/c2.py`

```
#!/usr/bin/env python3
"""C2 — moteur de politique (rapport, sections 7.2 et 9.2, figure 11).
Lit flows.csv, segments.csv, hosts.csv ; valide ; écrit regles-<etat>.json, regles-<etat>.md et tests.csv."""
import argparse, csv, json, sys

def lire(chemin):
    with open(chemin, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))

PORT_REPR = {"WAN": ("tcp", 443), "DMZ": ("tcp", 443), "LAN": ("tcp", 22), "DMZ-WEB": ("tcp", 443),
             "DMZ-DNS": ("udp", 53), "DMZ-APP": ("tcp", 5000), "LAN-USERS": ("tcp", 22),
             "LAN-DB": ("tcp", 5432), "LAN-ADMIN": ("tcp", 22)}      # port « représentatif » d'un segment
NAT_PUBLIC = "203.0.113.1"                                             # adresse vue depuis le WAN (FA1/F01, FA2/F02, FA6/F09)
IPERF = ("FA6", "F09")                                                 # flux activés seulement pendant les mesures (section 4.2)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--etat", choices=["A", "B"], required=True)
    ap.add_argument("--dossier", default="politique")
    a = ap.parse_args()
    d, E = a.dossier, a.etat
    flows = [f for f in lire(f"{d}/flows.csv") if f["etat"] == E]
    segs = {s["nom"]: s for s in lire(f"{d}/segments.csv") if E in s["etat"] and s["nom"] != "MGMT"}
    hosts = {h["hote"]: h for h in lire(f"{d}/hosts.csv")}
    seg = lambda x: hosts[x]["segment_" + E] if x in hosts else x       # hôte -> segment ; segment -> lui-même

    # ---------- validation ----------
    defaut = [f for f in flows if f["src"] == "any" and f["dst"] == "any"]    # F99 ou FA5
    autorises = [f for f in flows if f not in defaut]
    erreurs, avert = [], []
    for f in autorises:
        for x in [f["src"]] + f["dst"].split(";"):
            if x not in ("any", "opnsense") and seg(x) not in segs:
                erreurs.append(f"{f['id']} : « {x} » n'est ni un segment ni un hôte de l'état {E}")
        if not f["justification"].strip():
            erreurs.append(f"{f['id']} : justification absente")
        if "any" in (f["dst"], f["port"], f["proto"]):
            avert.append(f"{f['id']} : « any » contraire au moindre privilège")
    for i, f in enumerate(autorises):
        for g in autorises[i + 1:]:
            if seg(f["src"]) == seg(g["src"]) and set(f["dst"].split(";")) & set(g["dst"].split(";")) \
               and (f["port"] == "any" or g["port"] == "any" or set(f["port"].split(";")) & set(g["port"].split(";"))):
                avert.append(f"{f['id']} et {g['id']} se recouvrent")
    if len(defaut) != 1:
        erreurs.append("il faut exactement une ligne de refus par défaut (any,any)")
    for e in avert: print("AVERTISSEMENT", e)
    if erreurs:
        for e in erreurs: print("ERREUR", e)
        sys.exit(1)

    # ---------- génération des règles ----------
    def adresse(x, src_seg):
        if x == "any": return "any"
        if x == "opnsense": return segs[src_seg]["passerelle"]            # console : passerelle du segment source
        if x in hosts: return hosts[x]["ip"]
        return segs[x]["reseau"]
    def specificite(f):                                                   # du plus spécifique au plus général
        s = 0 if f["src"] in hosts else 1
        dd = 0 if all(x in hosts or x == "opnsense" for x in f["dst"].split(";")) else (2 if f["dst"] == "any" else 1)
        return (s, dd, f["port"] == "any")
    regles = {}
    for f in sorted(autorises, key=specificite):
        iface = seg(f["src"])                                             # interface d'entrée = segment source
        regles.setdefault(iface, []).append({
            "flux": f["id"], "action": "pass", "log": True, "interface": iface,
            "actif": f["id"] not in IPERF,                                # FA6 / F09 : créées mais désactivées
            "protocole": f["proto"], "source": adresse(f["src"], iface) if f["src"] in hosts else ("any" if iface == "WAN" else segs[iface]["reseau"]),
            "destination": [adresse(x, iface) for x in f["dst"].split(";")],
            "ports": f["port"].split(";"), "description": f"{f['id']} {f['justification']}"})
    for iface in segs:                                                    # refus par défaut, en dernier, journalisé
        regles.setdefault(iface, []).append({"flux": defaut[0]["id"], "action": "block", "log": True, "actif": True, "interface": iface,
            "protocole": "any", "source": "any", "destination": ["any"], "ports": ["any"], "description": defaut[0]["justification"]})
    with open(f"{d}/regles-{E}.json", "w", encoding="utf-8") as o:
        json.dump(regles, o, ensure_ascii=False, indent=2)
    with open(f"{d}/regles-{E}.md", "w", encoding="utf-8") as o:              # pour la saisie guidée dans l'interface
        o.write("| Interface | Ordre | Flux | Action | Proto | Source | Destination | Ports | Journal | Active |\n|---|---|---|---|---|---|---|---|---|---|\n")
        for iface, rs in regles.items():
            for n, r in enumerate(rs, 1):
                actif = "oui" if r["actif"] else "NON (seulement pendant les mesures iperf3)"
                o.write(f"| {iface} | {n} | {r['flux']} | {r['action']} | {r['protocole']} | {r['source']} | {', '.join(r['destination'])} | {', '.join(r['ports'])} | oui | {actif} |\n")

    # ---------- génération du plan de test ----------
    repr_hote = {}
    for h, v in hosts.items():
        if v["conteneur"] != "-": repr_hote.setdefault(v["segment_" + E], h)   # 1er hôte avec conteneur
    tests, n = [], 0
    def ajoute(src, ip, proto, port, attendu, ref):
        nonlocal n; n += 1
        tests.append([f"T{n:03d}", src, ip, proto, port, attendu, ref])
    def cible(src_seg, x):
        if seg(src_seg) == "WAN" and x in hosts and seg(x) != "WAN": return NAT_PUBLIC
        return adresse(x, src_seg)
    for f in autorises:
        s_seg = seg(f["src"]); src = f["src"] if f["src"] in hosts else repr_hote.get(s_seg)
        dsts = f["dst"].split(";")
        d0 = dsts[0] if dsts[0] != "any" else next(h for h in hosts if seg(h) not in (s_seg, "WAN") and hosts[h]["conteneur"] != "-")
        proto = "tcp" if f["proto"] in ("tcp", "tcp/udp", "any") else "udp"
        port = int(f["port"].split(";")[0]) if f["port"] != "any" else 443
        if f["id"] in IPERF:
            continue                                                        # flux iperf3 désactivés hors mesures
        ajoute(src, cible(s_seg, d0), proto, port, "pass", f["id"])
        if f["port"] != "any":
            mauvais = 22 if 22 not in [int(p) for p in f["port"].split(";")] else 23
            ajoute(src, cible(s_seg, d0), "tcp", mauvais, "block", f["id"])      # test « mauvais port »
    for a_ in segs:
        for b in segs:
            if a_ == b or b == "WAN" or a_ not in repr_hote or b not in repr_hote: continue
            if not any(seg(f["src"]) == a_ and any(x == "any" or seg(x) == b for x in f["dst"].split(";")) for f in autorises):
                proto, port = PORT_REPR[b]
                ajoute(repr_hote[a_], hosts[repr_hote[b]]["ip"], proto, port, "block", defaut[0]["id"])
    with open(f"{d}/tests.csv", "w", newline="", encoding="utf-8") as o:
        w = csv.writer(o); w.writerow(["id_test", "src_host", "dst_ip", "proto", "port", "attendu", "flux_ref"]); w.writerows(tests)
    print(f"État {E} : {sum(len(r) for r in regles.values())} règles sur {len(regles)} interfaces, {len(tests)} tests, {len(avert)} avertissement(s).")

if __name__ == "__main__":
    main()
```

*Application des règles*

```
# Méthode sûre (saisie guidée) : C2 écrit regles-B.md, un tableau ligne par ligne à reproduire
# dans Firewall › Rules, interface par interface, dans l'ordre indiqué. Comptez 30 min pour l'état B.
#
# Méthode automatique (facultative) : l'API d'OPNsense 26.7 gère les règles de la nouvelle page.
# Documentation : https://docs.opnsense.org (section « API », module firewall/filter).
# Créez une clé API (System › Access › Users › API keys) et testez d'abord UNE règle à la main
# avant d'automatiser. Le collecteur n'ayant que SSH vers OPNsense, appelez l'API en local :
#   ssh root@10.99.0.1 "curl -sk -u CLE:SECRET https://127.0.0.1/api/firewall/filter/searchRule"
```

**Tester**

**

```
python3 politique/c2.py --etat A
python3 politique/c2.py --etat B
head -5 politique/tests.csv
```
Sortie attendue :
```
AVERTISSEMENT FA4 : « any » contraire au moindre privilège
État A : 8 règles sur 3 interfaces, 8 tests, 1 avertissement(s).
État B : 16 règles sur 7 interfaces, 42 tests, 0 avertissement(s).
id_test,src_host,dst_ip,proto,port,attendu,flux_ref
T001,client-ext,203.0.113.1,tcp,80,pass,F01
T002,client-ext,203.0.113.1,tcp,22,block,F01
T003,client-ext,203.0.113.1,tcp,53,pass,F02
…
```
- `AVERTISSEMENT FA4` : attendu : l'état A est volontairement large ; c'est la faiblesse mesurée
- `16 règles sur 7 interfaces` : 9 règles pass (F01 à F09) et un block final par interface ; ces nombres ont été obtenus avec les fichiers de la page Contrats
- `T002 … 22,block` : test « mauvais port » de F01 : le port 22 de 203.0.113.1 doit rester fermé
- `tests.csv` : C2 réécrit tests.csv pour l'état demandé : relancez-le avec --etat avant chaque test_flux

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « n'est ni un segment ni un hôte » | Faute de frappe dans flows.csv | Le nom doit exister dans segments.csv ou hosts.csv (respecter les majuscules). |
| Le test d'un flux WAN vise 10.20.0.10 | Source WAN mal reconnue | La source doit être le segment « WAN » : C2 met alors l'adresse publique 203.0.113.1 dans le test. |
| Un flux autorisé est bloqué dans l'interface | Ordre de saisie différent de regles-*.md | Respecter l'ordre ; le block final toujours en dernier. |

**C'est fini quand**

- [ ] c2.py tourne pour A et B sans erreur
- [ ] regles-A.md, regles-B.md, tests.csv produits et commités
- [ ] Chaque interface a un block final journalisé

**À livrer.** Commit de `politique/` (c2.py, CSV, regles-*.md, tests.csv). Message aux rôles 1 et 5 : « tests.csv disponible ».

### Étape 7 — Prouver l'état A avec test_flux

**Quand :** 18–19 nov. · **Prérequis :** étapes parefeu-6, services-6 ; modules 5, 11

**But.** 100 % des tests de l'état A conformes, et la preuve que le mouvement latéral S-05 ne laisse aucune trace dans filterlog.

**Pourquoi.** Section 10.3, étape 1 : restaurer l'état et vérifier test_flux (100 % conforme) avant toute campagne. Jalon du 19 novembre.

**Configuration et commandes**

*Script à installer sur l'hôte Docker (exécuté en root)* — fichier `politique/test_flux.sh`

```
#!/bin/bash
# test_flux — exécute tests.csv depuis l'espace réseau de chaque conteneur source (écart E23).
# À lancer sur l'hôte Docker, en root :  ./test_flux.sh politique/tests.csv politique/hosts.csv
set -u
TESTS=${1:-politique/tests.csv}; HOSTS=${2:-politique/hosts.csv}
printf "%-6s %-11s %-15s %-4s %-5s %-6s %-6s %s\n" ID SOURCE DESTINATION PROTO PORT ATTENDU OBTENU VERDICT
# tr -d '\r' : un CSV enregistré sous Windows (fin de ligne CRLF) fausserait la dernière colonne
tail -n +2 "$TESTS" | tr -d '\r' | while IFS=, read -r id src dst proto port attendu ref; do
  ctr=$(tr -d '\r' < "$HOSTS" | awk -F, -v h="$src" '$1==h{print $5}')   # nom du conteneur source (hosts.csv)
  pid=$(docker inspect -f '{{.State.Pid}}' "$ctr" 2>/dev/null)      # processus du conteneur
  if [ -z "$pid" ] || [ "$pid" = "0" ]; then                        # conteneur absent ou arrêté : c'est un écart
    printf "%-6s %-11s %-15s %-4s %-5s %-6s %-6s %s\n" "$id" "$src" "$dst" "$proto" "$port" "$attendu" "-" "ÉCART (conteneur introuvable)"
    continue
  fi
  if [ "$proto" = "udp" ] && [ "$port" = "53" ]; then
    nsenter -t "$pid" -n dig @"$dst" www.labo.test +time=2 +tries=1 >/dev/null 2>&1 && r=pass || r=block
  else
    out=$(nsenter -t "$pid" -n nc -zv -w 2 "$dst" "$port" 2>&1); rc=$?
    if [ $rc -eq 0 ]; then r=pass                                   # connexion établie
    elif echo "$out" | grep -qi refused; then r=pass               # RST reçu : le pare-feu a laissé passer (port fermé)
    else r=block; fi                                               # délai dépassé : paquet jeté par le pare-feu
  fi
  [ "$r" = "$attendu" ] && v=CONFORME || v=ÉCART
  printf "%-6s %-11s %-15s %-4s %-5s %-6s %-6s %s\n" "$id" "$src" "$dst" "$proto" "$port" "$attendu" "$r" "$v"
done | tee /tmp/test_flux.txt
echo "Conformes : $(grep -c CONFORME /tmp/test_flux.txt) · Écarts : $(grep -c ÉCART /tmp/test_flux.txt)"
! grep -q ÉCART /tmp/test_flux.txt
```

> Dépendances sur l'hôte Docker : `apt install netcat-openbsd dnsutils` (pendant une fenêtre de configuration).

**Tester**

*Depuis le collecteur (tests générés ici, exécutés sur l'hôte Docker)*

```
cd /opt/projet6 && python3 politique/c2.py --etat A
scp politique/tests.csv politique/hosts.csv root@10.99.0.20:/opt/projet6/politique/
ssh root@10.99.0.20 'cd /opt/projet6 && ./politique/test_flux.sh politique/tests.csv politique/hosts.csv'
```
Sortie attendue :
```
ID     SOURCE      DESTINATION     PROTO PORT  ATTENDU OBTENU VERDICT
T001   client-ext  203.0.113.1     tcp   443   pass   pass   CONFORME
T002   client-ext  203.0.113.1     tcp   22    block  block  CONFORME
…
Conformes : 8 · Écarts : 0
```
- `CONFORME` : le pare-feu applique la matrice pour ce test ; un « pass » peut aussi venir d'un refus du service (RST), qui prouve que le pare-feu a laissé passer
- `Écarts : 0` : condition pour lancer la campagne A

*Preuve S-05 (état A)*

```
# hôte Docker
docker exec client-03 nc -zv -w 2 10.10.0.70 5432
# OPNsense : couple source,destination tel qu'il apparaît dans la ligne filterlog
grep -c '10.10.0.12,10.10.0.70' /var/log/filter/latest.log
```
Sortie attendue :
```
Connection to 10.10.0.70 5432 port [tcp/postgresql] succeeded!
0
```
- `succeeded` : la connexion aboutit
- `0` : aucune ligne filterlog : le mouvement latéral est invisible pour pf (section 4.3)

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Un test « pass » échoue | `tail /var/log/filter/latest.log` pendant le test | Règle manquante ou mal placée ; ou service non démarré (rôle 4). |
| Un test « block » passe | Règle trop large, ou état existant | Vider les états de la source (`pfctl -k <ip>`) et relancer. |
| « ÉCART (conteneur introuvable) » | Conteneur arrêté, ou colonne conteneur de hosts.csv | `docker ps` ; aligner les noms sur compose.yml (rôle 4). |

**C'est fini quand**

- [ ] test_flux A : 0 écart
- [ ] Preuve S-05 sans filterlog sauvegardée dans docs/
- [ ] config-etat-A.xml définitif commité

**À livrer.** Commit de `opnsense/config-etat-A.xml` et du rapport de conformité `resultats/A/conformite.txt`. Message aux rôles 1 et 5 : « état A prêt, test_flux 100 % ».

### Étape 8 — État B : six segments et règles F01–F99

**Quand :** 22–23 nov. · **Prérequis :** étapes chef-7, attaques-5 ; modules 2, 3, 5

**But.** Les VLAN 11, 12, 13, 21, 22, 23 avec leurs passerelles, et la matrice F appliquée interface par interface.

**Pourquoi.** Sections 5.1 et 5.2 : hôtes inchangés, seuls masque, passerelle et VLAN changent ; un flux autorisé = une justification ; tout le reste refusé et journalisé. Passer de A à B ne change jamais le câblage (section 6).

**Dans les interfaces**

- **Interfaces › Assignments** — Désactiver les interfaces LAN et DMZ de l'état A (ou retirer leur adresse) : sinon 10.10.0.1/26 chevaucherait 10.10.0.1/24.
- **Interfaces › Devices › VLAN** — Créer sur `em1` les tags 11, 12, 13, 21, 22, 23.
- **Interfaces › Assignments** — Affecter et activer, descriptions et adresses :
· LAN-USERS (11) `10.10.0.1/26` · LAN-DB (12) `10.10.0.65/27` · LAN-ADMIN (13) `10.10.0.97/27`
· DMZ-WEB (21) `10.20.0.1/27` · DMZ-DNS (22) `10.20.0.33/27` · DMZ-APP (23) `10.20.0.65/27`
- **Firewall › Rules › chaque interface** — Supprimer FA3 et FA4. Saisir les règles de `regles-B.md` dans l'ordre (F01–F09 ; F09 désactivée). Vérifier qu'aucune règle « net → any » ne subsiste.

**Configuration et commandes**

*Après l'application : vider les états (console de p6-opnsense, pas en SSH)*

```
pfctl -F states     # supprime toutes les connexions en cours : les anciennes connexions de l'état A
                    # continueraient sinon à passer malgré les nouvelles règles
# Depuis SSH, cette commande couperait aussi votre propre session : utilisez la console de la VM (VMware, ou TightVNC sur 127.0.0.1::5901 par le tunnel).
```

**Tester**

*Shell OPNsense*

```
ifconfig | grep -E 'vlan: (11|12|13|21|22|23)' | wc -l
netstat -rn -f inet | grep -E '10\.(10|20)\.0'
```
Sortie attendue :
```
6
10.10.0.0/26        link#7   U   vlan0.11
10.10.0.64/27       link#8   U   vlan0.12
10.10.0.96/27       link#9   U   vlan0.13
10.20.0.0/27        link#10  U   vlan0.21
10.20.0.32/27       link#11  U   vlan0.22
10.20.0.64/27       link#12  U   vlan0.23
```
- `6` : six sous-interfaces
- `routes connectées` : OPNsense connaît les six réseaux sans route statique (module 4)

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « overlapping » à l'enregistrement de 10.10.0.1/26 | L'interface LAN de l'état A porte encore 10.10.0.1/24 | La désactiver d'abord. |
| Les conteneurs ne joignent plus leur passerelle | Réseaux Docker encore en /24 (rôle 4 pas encore basculé) | Coordonner : le rôle 4 bascule juste après vous (étape 8 du rôle 4). |
| Un ancien flux passe encore | États hérités | `pfctl -F states` depuis la console. |

**C'est fini quand**

- [ ] Six interfaces VLAN avec les passerelles de la section 5.1
- [ ] Règles F01–F08 actives, F09 désactivée, refus par défaut journalisé sur chaque interface
- [ ] Plus aucune règle de l'état A
- [ ] Table d'états vidée

**À livrer.** Message au rôle 4 : « VLAN B actifs, à vous pour les réseaux Docker » ; au rôle 3 : « règles B en place ».

### Étape 9 — Prouver l'état B avec test_flux et livrer

**Quand :** 23–24 nov. · **Prérequis :** étapes parefeu-8, services-8 ; modules 5

**But.** 100 % des tests de l'état B conformes, S-05 désormais bloqué et journalisé, configuration B commitée.

**Pourquoi.** Jalon du 24 novembre (section 12) : test_flux conforme en B. Section 8.3 : S-05 doit être refusé et journalisé.

**Tester**

*Depuis le collecteur*

```
cd /opt/projet6 && python3 politique/c2.py --etat B
scp politique/tests.csv politique/hosts.csv root@10.99.0.20:/opt/projet6/politique/
ssh root@10.99.0.20 'cd /opt/projet6 && ./politique/test_flux.sh politique/tests.csv politique/hosts.csv' | tail -1
```
Sortie attendue :
```
État B : 16 règles sur 7 interfaces, 42 tests, 0 avertissement(s).
Conformes : 42 · Écarts : 0
```
- `Écarts : 0` : condition de la campagne B

*Preuve S-05 (état B)*

```
# hôte Docker
docker exec client-03 nc -zv -w 2 10.10.0.70 5432
# OPNsense
grep '10.10.0.12,10.10.0.70' /var/log/filter/latest.log | tail -1
```
Sortie attendue :
```
nc: connect to 10.10.0.70 port 5432 (tcp) timed out
… vlan0.11,match,block,in,4,…,tcp,60,10.10.0.12,10.10.0.70,…,5432,0,S,…
```
- `timed out` : la connexion n'aboutit plus
- `vlan0.11,match,block` : bloqué par le refus par défaut sur l'interface LAN-USERS : c'est F99

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| T0xx « admin → console » échoue | F08 doit viser 10.10.0.97, la passerelle de LAN-ADMIN | Corriger la destination de F08. |
| Le DNS de client-01 échoue | F06 en TCP au lieu d'UDP | F06 : UDP 53 (écart E16). |
| Tous les tests WAN échouent | Rôle 3 a activé Suricata en IPS sans réglage | Vérifier EVE (« blocked ») ; repasser en alert pendant test_flux. |

**C'est fini quand**

- [ ] test_flux B : 0 écart
- [ ] Preuve S-05 bloqué + filterlog sauvegardée
- [ ] `opnsense/config-etat-B.xml` commité

**À livrer.** Commit de la configuration B et de `resultats/B/conformite.txt`. Message aux rôles 1, 3 et 5 : « état B conforme ».

### Questions probables du jury

1. **Pourquoi la règle WAN vise-t-elle 10.20.0.10 et non 203.0.113.1 ?**
   Parce que pf applique la redirection (Destination NAT) avant d'évaluer les règles de filtrage : la règle voit déjà l'adresse interne. Une règle vers 203.0.113.1 ne correspondrait jamais au trafic redirigé.

2. **Comment savez-vous que le pare-feu applique bien la matrice ?**
   test_flux rejoue tests.csv, généré par C2 à partir de flows.csv, depuis l'espace réseau de chaque conteneur source. Nous exigeons 0 écart avant chaque campagne, et nous vidons les états après chaque changement de règles.

3. **Pourquoi les règles sont-elles écrites sur l'interface d'entrée ?**
   Les règles d'OPNsense filtrent en entrée de l'interface. F04 (API → base) s'écrit donc sur DMZ-APP. C'est aussi pourquoi C2 groupe les flux par segment source.

4. **Que se passe-t-il pour une connexion ouverte quand vous changez une règle ?**
   Elle continue grâce à son état. C'est pourquoi on vide la table d'états après le passage en état B, et pourquoi l'orchestrateur exécute pfctl -k entre deux exécutions.

5. **Pourquoi désactiver « Block bogon networks » sur le WAN ?**
   203.0.113.0/24 est une plage réservée à la documentation, présente dans les listes bogons. Cochée, l'option jetterait tout le trafic du WAN simulé avant nos règles. C'est une adaptation au laboratoire, documentée (écart E04).

### Démonstration finale (2 minutes)

Ce que vous montrez en 2 minutes : la politique est une donnée, et le pare-feu l'applique.

1. Ouvrir `flows.csv` : une ligne F04, sa justification.
2. Lancer `c2.py --etat B` : règles et tests générés.
3. Lancer `test_flux` : 0 écart.
4. Montrer la ligne filterlog du blocage de S-05 sur l'interface LAN-USERS.

**Plan de secours :** capture de la sortie de test_flux et de la ligne filterlog.

## Rôle 3 — IDS/IPS Suricata et trafic légitime (C3, C4, C8)

**Votre premier livrable est une décision** : netmap fonctionne-t-il sur les cartes réseau d'OPNsense sous VMware (pilote `em`) ? Le rapport l'a marquée « à valider au J1 » (section 5.3), avec deux replis : le mode Divert (proposé par OPNsense 26.7) ou l'IDS seul avec blocage par pf. Tout le reste en dépend.

Vous portez aussi C8, la sonde de trafic légitime : c'est elle qui mesure les faux positifs et la disponibilité dont vous avez besoin pour régler l'IPS.

**Mise à jour du 17 novembre.** La plateforme est VMware, pas Proxmox : les cartes d'OPNsense sont `em0` (WAN, 203.0.113.1), `em1` (trunk, parent de `vlan01` LAN et `vlan02` DMZ), `em2` (MGMT). Les déchargements CRC, TSO, LRO et VLAN Hardware Filtering sont déjà désactivés. Le test J1 porte donc sur netmap avec `em`. Avant d'activer l'IPS, demande au rôle 1 un instantané d'OPNsense (référence actuelle : `07-vnc`) ; si l'interface web devient injoignable, la console VNC (`127.0.0.1::5901`) reste disponible.

**Comptes et SSH réels.** Les VM Debian et Kali ont l'utilisateur `karim` avec `sudo`, pas de connexion `root`. SSH n'est accepté sur les VM que depuis Windows (10.99.0.2) et le collecteur (10.99.0.10) ; le SSH d'OPNsense est désactivé. Avant que l'orchestrateur tourne depuis le collecteur, le rôle 1 doit : installer la clé du collecteur sur p6-docker et p6-kali (pour `root`, ou pour `karim` avec un `sudo` sans mot de passe limité aux commandes utiles), et réactiver le SSH d'OPNsense sur MGMT, par clé seulement (la règle « Admin du pare-feu » autorise déjà le port 22 depuis 10.99.0.10).

### Étape 0 (préparation) — Préparer et tester ses règles sur son PC (avant l'hôte)

**Quand :** 13–21 nov. · **Prérequis :** modules 7, 9

**But.** Le fichier `ids/local.rules` de l'étape 4, testé hors ligne : chaque capture de scénario déclenche ses sid attendus, et une capture de trafic légitime n'en déclenche aucun.

**Pourquoi.** Les étapes 4 et 5 ont lieu sur l'hôte, pendant des séances partagées. En arrivant avec des règles déjà validées, on n'y perd pas de temps sur la syntaxe (document « Travail en parallèle », rôle 3). Suricata analyse un fichier de capture (option `-r`) avec le même moteur que le trafic en direct : seule la partie netmap/IPS reste à tester sur l'hôte.

**Dans les interfaces**

- **Windows › PowerShell (administrateur)** — `wsl -l -v` doit afficher Ubuntu en VERSION 2. Sinon : `wsl --install -d Ubuntu`, puis redémarrer. Dans le terminal Ubuntu, coller avec un clic droit (ou Ctrl+Shift+V).
- **Mini-laboratoire Docker** — Quatre conteneurs sur trois réseaux **isolés** (`internal: true`, aucun accès à Internet), avec les **mêmes adresses que le vrai laboratoire** : les règles de l'étape 4 s'appliquent donc sans aucune modification. L'attaquant a une patte dans chaque réseau : 203.0.113.66 (Kali), 10.20.0.80 (DMZ) et 10.10.0.12 (client-03).

**Configuration et commandes**

*1. Installer les outils (terminal Ubuntu)*

```
sudo apt update && sudo apt upgrade -y
sudo add-apt-repository -y ppa:oisf/suricata-stable
sudo apt update
sudo apt install -y suricata jq tcpdump nmap docker.io docker-compose-v2
sudo usermod -aG docker $USER        # puis fermer et rouvrir le terminal Ubuntu
suricata -V                          # noter la version (8.0.x) pour le rapport
docker run --rm hello-world
```

*2. Arborescence*

```
mkdir -p ~/projet6/ids/{labo,rules,pcap,out} && cd ~/projet6/ids
```

*3. Mini-laboratoire* — fichier `labo/Dockerfile`

```
FROM debian:stable-slim
RUN apt-get update && apt-get install -y --no-install-recommends nmap hping3 curl iproute2 \
    && rm -rf /var/lib/apt/lists/*
CMD ["sleep", "infinity"]
```

** — fichier `labo/docker-compose.yml`

```
# Mêmes adresses que le rapport. La passerelle Docker est déplacée en fin de plage (.254 / .94)
# pour libérer 203.0.113.1, l'adresse publique visée par les règles S-01, S-02 et S-06.
services:
  web:                                   # joue l'adresse publique d'OPNsense (em0)
    image: nginx:alpine
    networks: { wan: { ipv4_address: 203.0.113.1 } }
  api:                                   # API Flask (DMZ-APP), simulée par un serveur HTTP
    image: python:3.12-alpine
    command: python -m http.server 5000
    networks: { dmz: { ipv4_address: 10.20.0.70 } }
  db:                                    # PostgreSQL (LAN-DB), simulé : seul le port 5432 compte
    image: python:3.12-alpine
    command: python -m http.server 5432
    networks: { lan: { ipv4_address: 10.10.0.70 } }
  attaquant:
    build: .
    networks:
      wan: { ipv4_address: 203.0.113.66 }   # Kali
      dmz: { ipv4_address: 10.20.0.80 }     # position DMZ
      lan: { ipv4_address: 10.10.0.12 }     # client-03
networks:
  wan:
    internal: true
    driver_opts: { com.docker.network.bridge.name: br-wan }
    ipam: { config: [ { subnet: 203.0.113.0/24, gateway: 203.0.113.254 } ] }
  dmz:
    internal: true
    driver_opts: { com.docker.network.bridge.name: br-dmz }
    ipam: { config: [ { subnet: 10.20.0.64/27, gateway: 10.20.0.94 } ] }
  lan:
    internal: true
    driver_opts: { com.docker.network.bridge.name: br-lan }
    ipam: { config: [ { subnet: 10.10.0.0/24, gateway: 10.10.0.254 } ] }
```

> Lancer : `cd labo && docker compose up -d --build && cd ..` puis `docker ps` (4 conteneurs).

*4. Les règles* — fichier `rules/local.rules`

```
# Copier ici, sans rien changer, le bloc « ids/local.rules » de l'étape 4 (bouton Copier).
nano rules/local.rules
suricata -T -S rules/local.rules -l /tmp     # -T : vérifie la syntaxe sans rien analyser
```

*5. Capturer chaque scénario (deux terminaux)*

```
# Terminal 1 : capture sur le pont du réseau visé (WAN pour S-01, S-02, S-06 ; DMZ pour S-03 ; LAN pour S-05)
cd ~/projet6/ids && sudo tcpdump -i br-wan -w pcap/s01.pcap      # Ctrl+C quand l'attaque est finie

# Terminal 2 : lancer l'attaque depuis le conteneur attaquant
cd ~/projet6/ids/labo
docker compose exec attaquant nmap -sS -Pn -p 1-1024 203.0.113.1                     # s01.pcap  (br-wan)
docker compose exec attaquant timeout 3 hping3 -S --flood -p 443 203.0.113.1        # s02.pcap  (br-wan)
docker compose exec attaquant curl -s "http://10.20.0.70:5000/api/produit?id=1'%20OR%20'1'='1"   # s03.pcap (br-dmz)
docker compose exec attaquant nmap -sS -Pn -p 5432 10.10.0.70                        # s05.pcap  (br-lan)
docker compose exec attaquant nmap -f -sS -Pn -p 443 203.0.113.1                     # s06.pcap  (br-wan)
# Trafic légitime (2 min, br-wan) : 1 requête web par seconde, comme la sonde C8
docker compose exec attaquant sh -c 'for i in $(seq 120); do curl -s -o /dev/null http://203.0.113.1/; sleep 1; done'   # legitime.pcap
```

*6. Script de vérification* — fichier `tester.sh`

```
#!/usr/bin/env bash
# Rejoue chaque capture dans Suricata et compare les sid obtenus aux sid attendus.
cd ~/projet6/ids
declare -A ATTENDU=([s01]="9000001 9000002" [s02]="9000003 9000004" [s03]="9000010" [s05]="9000020" [s06]="9000030" [legitime]="")
for cap in s01 s02 s03 s05 s06 legitime; do
  [ -f pcap/${cap}.pcap ] || { echo "${cap} : capture absente"; continue; }
  rm -rf out/${cap} && mkdir -p out/${cap}
  suricata -r pcap/${cap}.pcap -S rules/local.rules -k none -l out/${cap} \
    --set vars.address-groups.HOME_NET="[10.0.0.0/8,203.0.113.1/32]" > /dev/null 2>&1
  obtenu=$(jq -r 'select(.event_type=="alert") | .alert.signature_id' out/${cap}/eve.json 2>/dev/null | sort -u | xargs)
  [ "${obtenu}" = "${ATTENDU[${cap}]}" ] && etat="OK" || etat="ÉCART"
  printf '%-9s attendu [%s]  obtenu [%s]  %s\n' "${cap}" "${ATTENDU[${cap}]}" "${obtenu}" "${etat}"
done
```

> Rendre exécutable puis lancer : `chmod +x tester.sh && ./tester.sh`.

**Tester**

**

```
./tester.sh
```
Sortie attendue :
```
s01       attendu [9000001 9000002]  obtenu [9000001 9000002]  OK
s02       attendu [9000003 9000004]  obtenu [9000003 9000004]  OK
s03       attendu [9000010]  obtenu [9000010]  OK
s05       attendu [9000020]  obtenu [9000020]  OK
s06       attendu [9000030]  obtenu []  ÉCART
legitime  attendu []  obtenu []  OK
```
- `OK` : la capture déclenche exactement les règles prévues pour ce scénario
- `s06 ÉCART` : exemple réaliste : la règle de fragments est marquée « À VALIDER EN LABORATOIRE » ; noter le résultat, essayer une variante, ou la signaler comme limite
- `legitime OK` : aucune alerte sur du trafic normal : premier indice de zéro faux positif (la vraie mesure de 2 h se fait à l'étape 6)

*Lire une alerte en détail*

```
jq -c 'select(.event_type=="alert") | [.timestamp, .src_ip, .dest_ip, .dest_port, .alert.signature_id, .alert.signature]' out/s01/eve.json | head -3
```
Sortie attendue :
```
["2026-11-14T10:02:11.481+0000","203.0.113.66","203.0.113.1",113,9000001,"LOCAL S-01 scan SYN > 20 en 5 s"]
```
- `src_ip` : l'attaquant (Kali) : c'est la source suivie par track by_src
- `dest_port` : le port sondé au moment où le seuil de 20 SYN est franchi
- `signature_id` : le sid de la règle : c'est lui que C11 compte pour le taux de détection

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| `suricata -T` signale une erreur de règle | Le message donne le numéro de ligne et le mot-clé fautif | Corriger la ligne (souvent un guillemet ou un point-virgule manquant après un copier-coller), relancer `-T`. |
| Aucune alerte, `eve.json` vide | `tcpdump -nr pcap/s01.pcap \| head` : la capture est vide, ou prise sur le mauvais pont | Capturer sur `br-wan`, `br-dmz` ou `br-lan` selon la cible. Jamais `-i any` : les paquets apparaissent en double et faussent les seuils. |
| `docker compose up` : « address already in use » ou conflit de passerelle | Une adresse du compose est prise par la passerelle Docker | Vérifier les lignes `gateway:` (.254 et .94), puis `docker compose down` et relancer. |
| `tcpdump: permission denied` | tcpdump lancé sans sudo | Relancer avec `sudo`. |
| Le flood S-02 produit une capture énorme | hping3 en mode flood envoie des centaines de milliers de paquets | Garder `timeout 3` ; ne pas pousser ce PCAP dans Git s'il dépasse 50 Mo. |

**C'est fini quand**

- [ ] Mini-laboratoire lancé : 4 conteneurs, 3 réseaux isolés
- [ ] `suricata -T` accepte `local.rules` sans erreur
- [ ] Captures s01, s02, s03, s05, s06 et legitime enregistrées
- [ ] `tester.sh` : OK partout, ou écart expliqué et noté (S-06)
- [ ] Règles, script et tableau des résultats poussés dans Git

**À livrer.** Dans le dépôt Git, dossier `ids/` : `local.rules` testé, `tester.sh`, captures de moins de 50 Mo, et `docs/tests-regles.md` (sortie de `tester.sh` commentée). Message au rôle 5 avec les commandes d'attaque utilisées, pour aligner `scenarios.yaml`.

### Étape 1 — Test de plateforme : netmap sur les cartes VMware (em)

**Quand :** 14–15 nov. · **Prérequis :** étapes parefeu-2 ; modules 7

> **Mise à jour :** Les cartes sont des cartes Intel e1000 émulées par VMware (pilote `em`), et non VirtIO. Le test reste le même : activer l'IPS sur `em0`, vérifier le trafic et `dmesg | grep netmap`. Si `em` pose problème, une option à décider avec le rôle 1 est de passer les cartes d'OPNsense en `vmxnet3` (pilote `vmx`) dans le .vmx, VM éteinte et après un instantané. Les interfaces changent alors de nom et doivent être réaffectées. Sinon, appliquer les replis prévus (Divert, ou IDS seul + pf).

**But.** Une décision écrite : IPS en ligne avec netmap, ou repli (Divert, ou IDS seul + blocage pf), avec la version d'OPNsense testée.

**Pourquoi.** Section 5.3 : Suricata en mode IPS utilise netmap ; « repli prévu si netmap est instable sur les cartes em de VMware ». Section 11.2 : risque n° 1, test au J1. Jalon du 15 novembre.

**Dans les interfaces**

- **Services › Intrusion Detection › Administration** — Activer, cocher « IPS mode », interface : WAN seulement pour ce test. Onglet Download : ET Open, télécharger. Activer une seule famille simple (par exemple « scan »).
- **System › Settings › Tunables** — Si netmap refuse la carte em, ajouter `dev.netmap.admode` = `2` (mode émulé, recommandé par le manuel IPS pour les cartes non prises en charge), redémarrer, recommencer.

**Configuration et commandes**

*Protocole de test (10 minutes)*

```
# Le 15 novembre, les services (rôle 4) ne sont pas encore publiés : le test se fait depuis Kali, vers OPNsense lui-même.
# 0. Rôle 2 : règle WAN TEMPORAIRE « pass ICMP, source 203.0.113.66, destination 203.0.113.1 » (à supprimer après le test)
# 1. Depuis Kali : un ping par seconde pendant tout le test (10 minutes)
ping -i 1 -c 600 203.0.113.1 > /tmp/j1.txt &
# 2. Depuis Kali : un scan court
nmap -sS -Pn -p 1-200 203.0.113.1
# 3. Sur OPNsense : erreurs netmap et alertes
dmesg | grep -i netmap | tail
tail -n 5 /var/log/suricata/suricata.log
grep -c '"event_type":"alert"' /var/log/suricata/eve.json
# 4. Bilan, sur Kali, après les 10 minutes
tail -2 /tmp/j1.txt
```

**Tester**

**

```
(bilan du test)
```
Sortie attendue :
```
600 packets transmitted, 597 received, 0.5% packet loss, time 599812ms
rtt min/avg/max/mdev = 0.310/0.620/4.100/0.200 ms
(dmesg : aucune erreur netmap répétée ; suricata.log : « engine started »)
```
- `597 received` : le trafic passe à travers Suricata en ligne : netmap fonctionne
- `aucune erreur répétée` : pas de blocage de l'interface
- `alertes > 0` : Suricata voit bien le trafic du WAN

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Plus aucun trafic après activation de l'IPS | `dmesg \| grep netmap` : erreurs sur em0 | Essayer `dev.netmap.admode=2` ; sinon repli Divert (mode IPS « Divert » dans l'administration IDS) ou IDS seul. |
| Le trafic passe mais aucune alerte | Home networks ne contient pas 203.0.113.1 (écart E05) | Étape 2 ; pour ce test, utiliser une règle qui vise l'adresse explicitement. |
| Latence très élevée ou pertes | Offloading encore actif | Rôle 2, étape 2 : désactiver CRC, TSO, LRO, redémarrer. |

**C'est fini quand**

- [ ] Test de 10 minutes mené (ping continu + scan), règle ICMP temporaire supprimée ensuite
- [ ] Décision écrite : netmap / Divert / IDS seul, version d'OPNsense notée
- [ ] Décision reportée dans le rapport (jalon du 15 novembre)

**À livrer.** Fichier `docs/decision-ips.md` (version, mode retenu, résultats du test) et message à tous, en particulier aux rôles 1 et 5.

### Étape 2 — Configurer l'IDS : interfaces, réseaux maison, journaux

**Quand :** 16 nov. puis 22 nov. · **Prérequis :** étapes ids-1, parefeu-2 ; modules 7, 3

**But.** Suricata configuré sur em0 et sur l'interface parente du trunk, avec des réseaux maison corrects et des alertes EVE exportées.

**Pourquoi.** Section 5.3 : IPS sur em0 (WAN) et sur em1 (parent du trunk) ; avec des VLAN, l'IPS s'active sur l'interface parente. Le manuel IPS d'OPNsense : désactiver l'offloading, mode promiscuous pour les VLAN. Écart E05 : 203.0.113.1 doit être un réseau maison.

**Dans les interfaces**

- **Services › Intrusion Detection › Administration › Settings** — Enabled ; IPS mode selon la décision du J1 ; **Promiscuous mode** coché (VLAN) ; Interfaces : `WAN` et `TRUNK` (em1, affectée sans adresse par le rôle 2) ; Pattern matcher : Hyperscan si proposé, sinon Aho-Corasick.
- **… › Administration › Advanced (Home networks)** — Réseaux maison : `10.0.0.0/8` et `203.0.113.1/32`. Pas tout 203.0.113.0/24 : Kali deviendrait « maison » et les règles « depuis l'extérieur » ne la verraient plus.
- **… › Administration › Logging** — Activer la sortie EVE en syslog (alertes) pour que C9 la reçoive ; garder aussi eve.json local. Rotation : quotidienne, 7 jours.
- **Jusqu'au 22 novembre** — En état A, l'IDS reste **désactivé** : l'état A n'a pas d'inspection (section 3). Ne l'activez qu'après le passage en état B.

**Tester**

*Shell OPNsense (état B)*

```
grep -A3 'HOME_NET' /usr/local/etc/suricata/suricata.yaml | head -4
pgrep -l suricata
```
Sortie attendue :
```
    HOME_NET: "[10.0.0.0/8,203.0.113.1/32]"
…
12345 suricata
```
- `HOME_NET` : la configuration générée contient bien l'adresse publique
- `suricata` : le moteur tourne

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Aucune alerte sur em1 | TRUNK non sélectionné, ou mode promiscuous absent | Sélectionner l'interface parente, cocher promiscuous, redémarrer le service. |
| Les alertes n'arrivent pas au collecteur | Cible syslog sans l'application suricata | Rôle 2, étape 5 : ajouter suricata à la cible distante. |
| L'IDS bloque déjà du trafic en état B | Une politique drop est active | Toutes les familles en alert jusqu'à l'étape 6. |

**C'est fini quand**

- [ ] WAN et TRUNK sélectionnés, promiscuous coché
- [ ] Réseaux maison : 10.0.0.0/8 et 203.0.113.1/32
- [ ] Alertes visibles dans eve.json et sur le collecteur

**À livrer.** Export de la configuration (avec le rôle 2) et message au rôle 6 : « les alertes EVE partent en syslog ; eve.json local dans /var/log/suricata/ ».

### Étape 3 — C8 : sonde SLA et trafic légitime

**Quand :** 16–19 nov. · **Prérequis :** étapes services-6 ; modules 10, 1

**But.** Une sonde qui produit sla.csv pendant toutes les mesures, pilotable par l'orchestrateur, et les mesures de débit iperf3.

**Pourquoi.** Section 7.8 : 1 requête HTTPS par seconde depuis client-ext et client-01, 1 requête DNS toutes les 5 s, chaque requête enregistrée ; débit iperf3 par FA6 / F09 hors campagnes. Section 9.3 : 2 h de trafic légitime seul pour mesurer les faux positifs. ENF4 : disponibilité ≥ 99 %.

**Configuration et commandes**

*Sonde* — fichier `sonde/sonde.py`

```
#!/usr/bin/env python3
"""C8 — sonde SLA (section 7.8) : 1 requête HTTPS par seconde ; si un serveur DNS est donné, 1 requête DNS toutes les 5 s.
Usage : sonde.py <client> <url> [serveur_dns]   — écrit des lignes sla.csv sur la sortie standard."""
import csv, datetime as dt, sys, time
import dns.resolver, requests, urllib3

urllib3.disable_warnings()                          # certificat auto-signé de nginx : vérification désactivée
client, url = sys.argv[1], sys.argv[2]
serveur = sys.argv[3] if len(sys.argv) > 3 else None
res = dns.resolver.Resolver(configure=False)
if serveur:
    res.nameservers, res.lifetime = [serveur], 2
w = csv.writer(sys.stdout)

def horodatage():
    return dt.datetime.now(dt.timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")

n = 0
while True:
    debut = time.monotonic()
    ts = horodatage()
    try:
        t = time.monotonic()
        ok = requests.get(url, timeout=2, verify=False).status_code == 200
        w.writerow([ts, client, url, "https", int(ok), round((time.monotonic() - t) * 1000) if ok else ""])
    except requests.RequestException:
        w.writerow([ts, client, url, "https", 0, ""])          # délai dépassé ou connexion refusée
    if serveur and n % 5 == 0:
        ts = horodatage()
        try:
            t = time.monotonic()
            res.resolve("www.labo.test", "A")
            w.writerow([ts, client, serveur, "dns", 1, round((time.monotonic() - t) * 1000)])
        except Exception:
            w.writerow([ts, client, serveur, "dns", 0, ""])
    sys.stdout.flush()
    n += 1
    time.sleep(max(0, 1 - (time.monotonic() - debut)))   # une itération par seconde
```

*Pilotage* — fichier `sonde/sonde.sh (sur l'hôte Docker, dans /opt/projet6/sonde/)`

```
#!/bin/bash
# sonde/sonde.sh start | stop [dossier]   — sur l'hôte Docker ; appelé par l'orchestrateur (rôle 5)
set -eu
D=/opt/projet6/sonde
case "$1" in
start)
  for c in client-ext client-01; do docker cp "$D/sonde.py" "$c:/tmp/sonde.py"; done
  docker exec -d client-ext sh -c 'python3 /tmp/sonde.py client-ext https://203.0.113.1/api/produits > /tmp/sla.csv 2>/dev/null'
  docker exec -d client-01  sh -c 'python3 /tmp/sonde.py client-01 https://10.20.0.10/api/produits 10.20.0.40 > /tmp/sla.csv 2>/dev/null'
  echo "sonde démarrée" ;;
stop)
  for c in client-ext client-01; do docker exec "$c" pkill -f sonde.py || true; done
  OUT=${2:-/srv/sla}; mkdir -p "$OUT"
  { echo "ts_utc,client,cible,type,succes,duree_ms"
    for c in client-ext client-01; do docker exec "$c" cat /tmp/sla.csv; done | sort; } > "$OUT/sla.csv"
  echo "$(($(wc -l < "$OUT/sla.csv") - 1)) mesures dans $OUT/sla.csv" ;;
esac
```

*Débit iperf3 (hors campagnes : A, B avec IPS, B sans IPS)*

```
# Le rôle 2 active la redirection et la règle FA6 / F09 le temps de la mesure, puis les désactive.
docker exec client-ext iperf3 -c 203.0.113.1 -p 5201 -t 30 -J > /srv/sla/iperf-A.json
#   -J : sortie JSON (débit moyen dans end.sum_received.bits_per_second), lue par le rôle 6
#   état B : iperf-B-avec-ips.json, puis Suricata désactivé : iperf-B-sans-ips.json
```

**Tester**

**

```
/opt/projet6/sonde/sonde.sh start; sleep 20; /opt/projet6/sonde/sonde.sh stop /tmp/essai
head -4 /tmp/essai/sla.csv
```
Sortie attendue :
```
40 mesures dans /tmp/essai/sla.csv
ts_utc,client,cible,type,succes,duree_ms
2026-11-18T14:02:11.003Z,client-01,10.20.0.40,dns,1,3
2026-11-18T14:02:11.004Z,client-01,https://10.20.0.10/api/produits,https,1,41
2026-11-18T14:02:11.010Z,client-ext,https://203.0.113.1/api/produits,https,1,48
```
- `≈ 40 mesures en 20 s` : 2 clients × 1 requête/s, plus le DNS toutes les 5 s
- `succes 1, duree_ms 41` : requête servie en 41 ms
- `horodatage …Z` : UTC, à la milliseconde : prêt pour la corrélation

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « pkill: not found » | procps absent de l'image des postes | Ajouter procps au Dockerfile des postes (rôle 4) et reconstruire. |
| Toutes les requêtes HTTPS échouent depuis client-01 en état B | F05 (rôle 2) ou passerelle (rôle 4) | test_flux T009 doit être conforme. |
| Des secondes manquent dans sla.csv | Requêtes en délai dépassé (2 s) : la boucle prend du retard | Normal pendant un flood : c'est justement l'indisponibilité mesurée. |

**C'est fini quand**

- [ ] sla.csv produit avec les colonnes du contrat
- [ ] start / stop fonctionnent depuis le collecteur par SSH
- [ ] Une mesure iperf3 de référence (état A) enregistrée

**À livrer.** Commit de `sonde/`. Message aux rôles 5 et 6 : « sonde.sh start | stop ; sla.csv dans /srv/sla/ sur l'hôte Docker ».

### Étape 4 — Écrire les règles locales et le blocage temporaire

**Quand :** 22 nov. · **Prérequis :** étapes ids-2, parefeu-8 ; modules 7

**But.** Le jeu de règles C4 : ET Open + règles locales sid 9000001 à 9000099, et le rate_filter de S-01, chargés par Suricata.

**Pourquoi.** Section 7.4 : seuil de scan SYN (> 20 SYN en 5 s, track by_src), seuil de SYN flood, motifs d'injection SQL sur /api/, fragments anormaux, LAN-USERS → 5432. Figure 6 : blocage temporaire de la source (rate_filter). Écart E07 : règle ajoutée pour S-04.

**Configuration et commandes**

*Règles locales* — fichier `ids/local.rules`

```
# ids/local.rules — règles locales du projet (sid 9000001 à 9000099, section 7.4). Syntaxe Suricata 8.
# Toutes sont écrites en « alert ». Le passage en drop se fait famille par famille après le réglage (étape 6).

# S-01 · scan SYN vers l'adresse publique (em0), hors ports publiés : les SYN légitimes vers 53, 80, 443
# (sonde, clients) ne comptent pas. Détection : une alerte par fenêtre de 5 s et par source.
alert tcp any any -> 203.0.113.1 ![53,80,443] (msg:"LOCAL S-01 scan SYN > 20 en 5 s"; flow:stateless; flags:S,12; threshold:type both, track by_src, count 20, seconds 5; classtype:attempted-recon; sid:9000001; rev:2;)
# S-01 · même trafic : compteur du rate_filter (blocage temporaire, figure 6). « threshold limit » limite
# les alertes à une par 20 s ; le rate_filter compte, lui, chaque correspondance (À VALIDER EN LABORATOIRE).
alert tcp any any -> 203.0.113.1 ![53,80,443] (msg:"LOCAL S-01 compteur de blocage"; flow:stateless; flags:S,12; threshold:type limit, track by_src, count 1, seconds 20; classtype:attempted-recon; sid:9000002; rev:2;)

# S-02 · SYN flood vers le web : seuil par source (un vrai client fait ~1 SYN/s). Détection : une alerte par seconde au plus.
alert tcp any any -> 203.0.113.1 443 (msg:"LOCAL S-02 SYN flood > 100/s"; flow:stateless; flags:S,12; threshold:type both, track by_src, count 100, seconds 1; classtype:attempted-dos; sid:9000003; rev:2;)
# S-02 · compteur du rate_filter de blocage (même principe que 9000002)
alert tcp any any -> 203.0.113.1 443 (msg:"LOCAL S-02 compteur de blocage"; flow:stateless; flags:S,12; threshold:type limit, track by_src, count 1, seconds 20; classtype:attempted-dos; sid:9000004; rev:1;)

# S-03 · injection SQL sur le saut nginx -> API, en clair (em1, section 11.3). http.uri est décodée (%20 -> espace),
# mais un « + » reste un « + » : le motif accepte les deux séparateurs.
alert http any any -> 10.20.0.70 5000 (msg:"LOCAL S-03 injection SQL sur /api/"; flow:established,to_server; http.uri; content:"/api/"; startswith; pcre:"/'(\s|\+)*or(\s|\+)*'?\d+'?(\s|\+)*=(\s|\+)*'?\d+/i"; classtype:web-application-attack; sid:9000010; rev:2;)

# S-05 · un poste utilisateur tente de joindre la base (em1)
alert tcp 10.10.0.0/26 any -> 10.10.0.70 5432 (msg:"LOCAL S-05 LAN-USERS vers PostgreSQL"; flow:stateless; flags:S,12; classtype:policy-violation; sid:9000020; rev:1;)
# S-04 · un hôte de la DMZ autre que l'API tente de joindre la base (écart E07)
alert tcp [10.20.0.0/24,!10.20.0.70] any -> 10.10.0.70 5432 (msg:"LOCAL S-04 DMZ hors API vers PostgreSQL"; flow:stateless; flags:S,12; classtype:policy-violation; sid:9000021; rev:1;)

# S-06 · fragments IP minuscules (nmap -f découpe en morceaux de 8 octets) — À VALIDER EN LABORATOIRE
alert ip any any -> 203.0.113.1 any (msg:"LOCAL S-06 petits fragments IP"; fragbits:M; dsize:<24; threshold:type limit, track by_src, count 1, seconds 60; classtype:misc-activity; sid:9000030; rev:1;)
```

*Blocage temporaire de S-01 et S-02* — fichier `ids/threshold.config`

```
# ids/threshold.config — blocage temporaire de la source (figure 6 : « rate_filter »)
# Après 20 correspondances de 9000002 en 5 s depuis une même source (S-01), ou 100 de 9000004 en 1 s (S-02),
# l'action devient « drop » pour les paquets qui correspondent à cette règle, pendant 20 s.
# timeout 20 < pause de 30 s entre deux exécutions : la source est libérée avant l'exécution suivante.
# À VALIDER EN LABORATOIRE : le scan doit s'interrompre, et le compteur de paquets rejetés de stats.log
# doit augmenter (nom du compteur : à vérifier sur votre version, par exemple « ips.blocked »).
# Repli : règle compteur en « drop » avec detection_filter (blocage paquet par paquet, sans durée).

# --- Phase de réglage (mode IDS, étapes 4 et 5) : ne rien bloquer, utiliser ces deux lignes ---
# rate_filter gen_id 1, sig_id 9000002, track by_src, count 20, seconds 5, new_action alert, timeout 20
# rate_filter gen_id 1, sig_id 9000004, track by_src, count 100, seconds 1, new_action alert, timeout 20

# --- Phase IPS (étape 6, campagne B) ---
rate_filter gen_id 1, sig_id 9000002, track by_src, count 20, seconds 5, new_action drop, timeout 20
rate_filter gen_id 1, sig_id 9000004, track by_src, count 100, seconds 1, new_action drop, timeout 20
```

*Charger les fichiers dans OPNsense*

```
# OPNsense n'a pas d'éditeur de règles Suricata. Deux méthodes connues (forum OPNsense), à valider :
# A) Fichier de règles personnalisées lu par la configuration générée :
grep -n "rule-files" -A20 /usr/local/etc/suricata/suricata.yaml      # repérer les fichiers chargés
scp ids/local.rules root@10.99.0.1:/usr/local/etc/suricata/rules/custom.rules
# B) Ensemble de règles « maison » déclaré par un fichier XML de métadonnées dans
#    /usr/local/opnsense/scripts/suricata/metadata/rules/ (voir le tutoriel « Adding custom rules » du forum),
#    puis Services › Intrusion Detection › Download : l'ensemble apparaît, l'activer.
#
# Fichier de seuils : vérifier qu'un « threshold-file » est déclaré
grep -n "threshold" /usr/local/etc/suricata/suricata.yaml
#   S'il ne l'est pas : le manuel IPS décrit un gabarit custom.yaml pour ajouter des options à suricata.yaml.
#
# Recharger et vérifier
configctl ids reload          # nom de l'action à vérifier ; sinon « Apply » dans l'interface
grep -c 9000 /var/log/suricata/suricata.log; tail -n 3 /var/log/suricata/suricata.log
```

**Tester**

*Shell OPNsense*

```
suricata -T -c /usr/local/etc/suricata/suricata.yaml 2>&1 | tail -2
grep -E 'rules successfully loaded|signatures processed' /var/log/suricata/suricata.log | tail -1
```
Sortie attendue :
```
… Configuration provided was successfully loaded. Exiting.
… 1 rule files processed. 48211 rules successfully loaded, 0 rules failed
```
- `successfully loaded` : la syntaxe des règles et du fichier de seuils est valide
- `0 rules failed` : aucune règle locale rejetée ; sinon le journal donne le sid fautif

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « failed to parse signature » pour un sid 90000xx | Faute de syntaxe (point-virgule, guillemet) | Corriger la règle ; tester avec `suricata -T` avant de recharger. |
| Les règles locales disparaissent après une mise à jour des règles | Fichier écrasé par la régénération | Utiliser la méthode B (ensemble déclaré) ; garder le fichier dans Git. |
| rate_filter sans effet | threshold-file absent de la configuration, ou interaction avec « threshold limit » de la règle | Déclarer le fichier (gabarit custom.yaml) ; vérifier dans suricata.log ; essayer la règle compteur sans threshold. |

**C'est fini quand**

- [ ] ET Open téléchargé (fenêtre de configuration)
- [ ] Règles 9000001–9000030 chargées, 0 échec
- [ ] rate_filter de 9000002 et 9000004 chargés, en variante new_action alert
- [ ] Tout est en action alert

**À livrer.** Commit de `ids/local.rules` et `ids/threshold.config` avec le hash du jeu de règles (`sha256sum`) dans `ids/VERSION.txt`.

### Étape 5 — Mode IDS : vérifier que chaque scénario est détecté

**Quand :** 23 nov. · **Prérequis :** étapes ids-4, attaques-3 ; modules 7, 9

**But.** Chaque scénario produit l'alerte attendue en mode IDS, sans rien bloquer.

**Pourquoi.** Section 7.3 : IDS (alert uniquement) pendant le réglage. Figure 12 : rejouer trafic légitime et scénarios S-01 à S-06, mesurer détection et faux positifs.

**Configuration et commandes**

*Une exécution par scénario (collecteur)*

```
python3 campagne/orchestrateur.py --etat B --n 1     # IPS en alert seulement à ce stade
ssh root@10.99.0.1 "grep '\"event_type\":\"alert\"' /var/log/suricata/eve.json | tail -n 50" > /tmp/alertes.json
python3 -c "import json,collections;c=collections.Counter(json.loads(l)['alert']['signature_id'] for l in open('/tmp/alertes.json'));print(c)"
```

**Tester**

**

```
(compte des alertes par sid)
```
Sortie attendue :
```
Counter({9000001: 3, 9000003: 6, 9000002: 1, 9000004: 1, 9000010: 1, 9000020: 2, 9000021: 1, 9000030: 1, 2010937: 4, …})
```
- `9000001` : S-01 détecté (une alerte par fenêtre de 5 s)
- `9000002, 9000004` : compteurs du blocage : une alerte au plus par 20 s ; en mode IDS, le rate_filter est en new_action alert
- `9000010` : S-03 : injection vue sur em1
- `9000020, 9000021` : S-05 et S-04 détectés
- `9000030` : S-06 : à valider ; si absent, ajuster la règle
- `sid à 7 chiffres` : règles ET Open : candidates aux faux positifs, à mesurer à l'étape 6

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Pas d'alerte S-03 | La requête n'arrive pas en clair sur em1, ou URI encodée | Vérifier sur em1 avec tcpdump ; http.uri est normalisée, le motif doit viser la forme décodée. |
| Pas d'alerte S-01 | HOME_NET ou règle visant 203.0.113.1 | Étape 2 ; vérifier que la règle vise l'adresse publique (em0 voit l'adresse avant NAT). |
| Des centaines d'alertes S-01 | threshold absent sur 9000001 | Une alerte par fenêtre : `threshold:type both`. |

**C'est fini quand**

- [ ] Chaque scénario a au moins une alerte locale (S-06 : à valider)
- [ ] Aucun paquet bloqué en mode IDS

**À livrer.** Tableau « scénario → sid observés » dans `docs/detection-ids.md`, envoyé au rôle 6.

### Étape 6 — Régler pendant 2 h, puis passer en IPS

**Quand :** 23–24 nov. · **Prérequis :** étapes ids-5, analyse-3 ; modules 7, 10

**But.** Les familles validées en drop, les autres en alert, avec au plus 1 faux positif par heure et une disponibilité d'au moins 99 %.

**Pourquoi.** Section 9.3 et figure 12 : 2 h de trafic légitime seul (une fenêtre plus courte ne permet pas de vérifier « 1 par heure ») ; drop seulement pour les familles validées ; critère d'arrêt : ≤ 1 FP/h et disponibilité ≥ 99 %. Jalon du 24 novembre.

**Dans les interfaces**

- **Services › Intrusion Detection › Policy** — Créer une politique : action « drop » pour 9000010 et pour les familles ET validées. Les blocages de S-01 et S-02 ne passent pas par la politique : ils viennent du rate_filter (lignes « Phase IPS » de threshold.config, new_action drop). Laisser 9000001, 9000003, 9000020, 9000021, 9000030 en alert : ce sont les règles de détection ; pf bloque déjà S-04 et S-05.

**Configuration et commandes**

*Boucle de réglage*

```
# 1. 2 h de trafic légitime seul, IDS en alert (collecteur)
DEBUT=$(date -u +%FT%TZ)
ssh root@10.99.0.20 /opt/projet6/sonde/sonde.sh start ; sleep 7200 ; ssh root@10.99.0.20 /opt/projet6/sonde/sonde.sh stop /srv/sla/reglage
FIN=$(date -u +%FT%TZ)
# 2. Faux positifs par sid (avec le rôle 6) : toute alerte pendant ces 2 h est un faux positif
mkdir -p resultats/B/reglage && scp root@10.99.0.20:/srv/sla/reglage/sla.csv resultats/B/reglage/
python3 analyse/parse.py --dossier resultats/B/reglage
python3 analyse/fp.py resultats/B/reglage/events.csv --debut $DEBUT --fin $FIN > resultats/B/reglage/fp_par_sid.csv
# 3. Pour chaque sid > 0 : ajuster le seuil, restreindre la portée, ou supprimer (figure 12), puis rejouer
# 4. Passer en IPS : politique drop des familles validées, lignes « Phase IPS » de threshold.config, recharger.
#    Une exécution de S-01, puis sur OPNsense :
grep '"signature_id":9000002' /var/log/suricata/eve.json | tail -1   # attendu : "action":"blocked"
grep -c '"action":"blocked"' /var/log/suricata/eve.json
tail -n 60 /var/log/suricata/stats.log | grep -iE 'drop|block'        # compteurs de rejet (noms : à vérifier sur votre version)
```

**Tester**

**

```
column -s, -t resultats/B/reglage/fp_par_sid.csv | head -5
```
Sortie attendue :
```
sid       alertes  fp_par_heure
2013504   6        3.0
2027390   1        0.5
9000030   1        0.5
```
- `3.0 > 1` : famille à laisser en alert (ou à supprimer avec justification)
- `total des familles en drop ≤ 1` : critère de la section 9.3

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| La sonde échoue après le passage en drop | Une règle en drop touche le trafic légitime | Remettre la famille en alert (figure 12, « remettre en alert les règles fautives »). |
| Aucun « blocked » dans EVE | Politique non appliquée, ou mode IDS pcap | Vérifier le mode IPS et la politique ; relancer le service. |
| Kali reste bloquée longtemps | timeout du rate_filter trop long | timeout 20 s, inférieur à la pause de 30 s. |

**C'est fini quand**

- [ ] fp_par_sid.csv sur 2 h, et décision par famille consignée
- [ ] Familles en drop : ≤ 1 FP/h au total
- [ ] Disponibilité de la sonde ≥ 99 % sur les 2 h
- [ ] Jeu de règles figé : hash dans ids/VERSION.txt, commit

**À livrer.** Message aux rôles 1 et 5 : « IPS prêt, jeu de règles figé (hash …) ». Le rôle 1 peut préparer etat-B.

### Étape 7 — Préparer la panne P-01

**Quand :** 24 nov. · **Prérequis :** étapes ids-6 ; modules 7, 9

**But.** Les commandes d'arrêt et de redémarrage de Suricata testées, et le protocole de mesure prêt pour l'orchestrateur.

**Pourquoi.** Section 8.5 et figure 9 : arrêter Suricata pendant un SYN flood ; deux comportements possibles ; on ne suppose pas, on mesure (disponibilité et alertes avant, pendant, après, temps de reprise). Section 10.3 : P-01 en état B, 5 répétitions.

**Configuration et commandes**

*Commandes à valider sur OPNsense (en dehors de toute campagne)*

```
configctl ids stop      # arrêt du moteur (figure 9 : « service suricata stop »)
pgrep -l suricata || echo "Suricata arrêté"
configctl ids start
sleep 5; pgrep -l suricata
# Si configctl n'a pas ces actions sur votre version : service suricata onestop / onestart,
# ou le bouton d'arrêt de Services › Intrusion Detection. Reporter la commande validée
# dans campagne/scenarios.yaml (P-01, champ actions).
```

**Tester**

*Essai à blanc avec la sonde*

```
python3 campagne/orchestrateur.py --etat B --scenario P-01 --n 1
```
Sortie attendue :
```
B-P01-01 · Panne de l'IPS pendant un SYN flood
Terminé : resultats/B/2026-11-24/runs.csv
```
- `une exécution complète` : arrêt à t0 + 10 s, redémarrage à t0 + 40 s, sonde active : la mesure est prête

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Suricata ne redémarre pas | `tail /var/log/suricata/suricata.log` | Démarrer depuis l'interface ; ajuster la commande dans scenarios.yaml. |
| Toute l'interface WAN reste coupée après le redémarrage | netmap n'a pas repris l'interface | Noter ce comportement : c'est un résultat ; redémarrer l'interface et documenter la procédure de reprise. |
| Pas de mesure pendant la panne | Sonde arrêtée | La sonde doit tourner avant t0 et après t1 + 30 s. |

**C'est fini quand**

- [ ] Arrêt et redémarrage testés à la main
- [ ] Une exécution à blanc de P-01 complète
- [ ] Commande définitive reportée dans scenarios.yaml

**À livrer.** Fichier `docs/p01.md` (commandes, durée de redémarrage observée) et message au rôle 5 : « P-01 prêt pour la campagne B ».

### Questions probables du jury

1. **Pourquoi Suricata est-il avant pf ?**
   Avec netmap, Suricata intercepte les paquets entre la carte et la pile du noyau : pour le trafic entrant, il les voit avant pf. Il détecte donc aussi les sondes vers des ports fermés, et un paquet qu'il rejette n'atteint jamais pf. Nous l'avons vérifié au test J1 : un paquet rejeté apparaît dans EVE (« blocked ») et pas dans filterlog.

2. **Pourquoi ne pas tout passer en drop directement ?**
   Des règles génériques se déclenchent sur du trafic légitime : en drop, elles feraient chuter la disponibilité. Nous mesurons d'abord 2 h de trafic légitime en alert, règle par règle, et ne passons en drop que les familles à moins d'1 faux positif par heure (section 9.3).

3. **Comment fonctionne le blocage de S-01 ?**
   La règle 9000002 compte les SYN de chaque source vers les ports non publiés ; un rate_filter change son action en drop pendant 20 s dès 20 SYN en 5 s. S-02 suit le même principe avec 9000004 (100 SYN en 1 s). Les premières sondes passent donc : un port publié peut être découvert s'il est sondé tôt. Nous l'écrivons tel quel dans les résultats.

4. **Pourquoi l'injection n'est-elle pas vue sur le WAN ?**
   Elle est chiffrée en TLS jusqu'à nginx. Le saut nginx → API est en HTTP clair et passe par em1 : c'est là que la règle 9000010 la voit. C'est une limite assumée (section 11.3).

5. **Que se passe-t-il si Suricata tombe ?**
   C'est P-01 : nous arrêtons Suricata pendant un SYN flood et mesurons la disponibilité et les alertes avant, pendant et après, puis le temps de reprise. Le rapport ne suppose pas le comportement : [résultat mesuré].

### Démonstration finale (2 minutes)

Ce que vous montrez en 2 minutes : Suricata est en ligne avant pf, et il bloque sans gêner les vrais clients.

1. Le schéma netmap : où Suricata prend le paquet.
2. Un scan S-01 depuis Kali : alerte 9000001, puis alerte 9000002 « blocked » dans EVE et compteur de rejet en hausse dans stats.log ; aucune ligne filterlog pour les paquets rejetés.
3. fp_par_sid.csv : ≤ 1 FP/h pour les familles en drop.
4. La disponibilité de la sonde pendant ce temps (≥ 99 %).

**Plan de secours :** extraits d'EVE et de sla.csv dans le rapport.

## Rôle 4 — Services applicatifs et capture (C5, C10)

**Vos conteneurs sont la cible de tout le monde** : test_flux (rôle 2), sonde (rôle 3), attaques (rôle 5), captures (rôle 6). Leur nom de conteneur et leur adresse doivent correspondre exactement à `hosts.csv` (page Contrats).

**Mise à jour du 17 novembre.** Les étapes 1 à 6 ont été réalisées par le rôle 1 : l'état A tourne sur `p6-docker` (10.99.0.20) avec 10 conteneurs, et `tests.sh` donne 12/12. Les fichiers sont dans `~/p6-etat-a` sur la VM (copie de `D:\Labo-P6\docker\p6-etat-a` sur l'hôte) : un seul `docker-compose.yml` (projet `p6`), `setup.sh` relançable, `tests.sh`. Vérifie-les, reprends-les dans le dépôt Git, puis enchaîne sur l'étape 7 (outil de capture C10).

Cartes réelles de `p6-docker` : `ens32` (MGMT), `ens33` (trunk, « eth1 » du rapport), `ens34` (WAN, « eth2 »), `ens35` (NAT, débranchée). Réseaux Docker réels : `p6_lan`, `p6_dmz`, `p6_wan` (le nom du projet Compose est ajouté devant). Docker installé : **26.1.5** avec Compose 2.26.1 (paquets Debian `docker.io` et `docker-compose`), pas Docker Engine 29. La VM n'a pas Internet : pour un `apt` ou un `docker pull`, demande au rôle 1 de rebrancher sa carte NAT le temps du téléchargement.

### Étape 1 — Installer Docker et les outils sur l'hôte Docker

**Quand :** 14 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes chef-4 ; modules 8

> **Mise à jour :** Fait par le rôle 1 : Docker **26.1.5** et Compose 2.26.1 (paquets Debian), tcpdump installé, `karim` dans le groupe `docker`. Le filtre SSH du rôle 1 est la table `inet p6_ssh` (pas `inet mgmt`) : `sudo nft list table inet p6_ssh` doit toujours l'afficher à côté des tables de Docker.

**But.** Docker (26.1.5, paquets Debian) et les outils de test (nc, dig, tcpdump) installés sur p6-docker, sans casser le pare-feu MGMT.

**Pourquoi.** Section 6 : les services tournent en conteneurs Docker rattachés aux VLAN par macvlan (4 VM au lieu de 9, section 11.1). test_flux et les captures s'exécutent depuis cet hôte.

**Configuration et commandes**

*Dépôt officiel Docker pour Debian 13*

```
apt-get update && apt-get install -y ca-certificates curl
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
tee /etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/debian
Suites: $(. /etc/os-release && echo "$VERSION_CODENAME")
Components: stable
Signed-By: /etc/apt/keyrings/docker.asc
EOF
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
apt-get install -y netcat-openbsd dnsutils tcpdump      # test_flux (rôle 2) et captures (étape 7)
```

*Images à télécharger pendant la même fenêtre*

```
docker pull postgres:17
docker pull nginx:stable
docker pull python:3.13-slim
docker pull internetsystemsconsortium/bind9:9.20
docker pull debian:13-slim
```

*Image commune des postes, construite pendant la même fenêtre* — fichier `services/outils/Dockerfile`

```
# services/outils/Dockerfile — image commune des postes (clients, dmz-pivot, admin, client-ext, iperf3)
FROM debian:13-slim
RUN apt-get update && apt-get install -y --no-install-recommends \
      curl ca-certificates dnsutils nmap netcat-openbsd postgresql-client iperf3 \
      iproute2 iputils-ping procps openssh-client python3 python3-requests python3-dnspython \
    && rm -rf /var/lib/apt/lists/*
#   nmap, psql : nécessaires aux scénarios S-04 et S-05 lancés dans dmz-pivot et client-03
#   python3-requests, python3-dnspython, procps (pkill) : sonde SLA du rôle 3
# Construction (pendant une fenêtre de configuration) :  docker build -t projet6/outils services/outils
```

*Construire*

```
mkdir -p /opt/projet6/services/outils     # ou le dépôt, s'il est déjà cloné (rôle 1, étape 7)
# y placer le Dockerfile ci-dessus, puis :
docker build -t projet6/outils /opt/projet6/services/outils     # apt-get dans l'image : carte NAT branchée
```

> Après cela, la carte NAT peut être débranchée. Seule l'image de l'API (étape 3) demandera de la rebrancher, le temps de son `build`.

**Tester**

**

```
docker version --format '{{.Server.Version}}'
docker compose version
nft list tables
```
Sortie attendue :
```
26.1.5
Docker Compose version 2.26.1
table inet p6_ssh
table ip nat
table ip filter
…
```
- `29.x.y` : Docker Engine 29
- `table inet p6_ssh` : le filtre SSH du rôle 1 est toujours là
- `table ip nat / filter` : tables créées par Docker (selon la version, ip ou inet)

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| `apt-get update` échoue | Carte NAT débranchée | Rebrancher le temps de l'installation (rôle 1, étape 4). |
| Plus de SSH depuis le collecteur après l'installation | `nft list ruleset` | Recharger `/etc/nftables.d/mgmt.nft` ; ne jamais utiliser `flush ruleset` sur cet hôte. |
| « permission denied » sur docker | Utilisateur hors du groupe docker | Travailler en root, ou `usermod -aG docker <vous>` puis reconnexion. |

**C'est fini quand**

- [ ] Docker 26.1.5 et Compose 2.26.1 installés
- [ ] nc, dig, tcpdump présents sur l'hôte
- [ ] Les 5 images de base téléchargées, projet6/outils construite
- [ ] Le pare-feu MGMT est intact

**À livrer.** Mettre la version exacte de Docker dans `docs/versions.md`. Message au rôle 2 : « hôte Docker prêt, nsenter disponible pour test_flux ».

### Étape 2 — Créer les réseaux macvlan de l'état A

**Quand :** 15–16 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes services-1, parefeu-3 ; modules 3, 8, 2

> **Mise à jour :** Fait par le rôle 1, avec des noms différents : `p6_lan` (parent `ens33.10`), `p6_dmz` (`ens33.20`), `p6_wan` (`ens34`), créés par le `docker-compose.yml` du projet `p6`. Vérifier : `docker network ls` et `docker network inspect p6_dmz`.

**But.** Trois réseaux Docker : lan (VLAN 10), dmz (VLAN 20) et wan (eth2), avec les passerelles d'OPNsense.

**Pourquoi.** Section 4.1 (adresses, VLAN, passerelles) et section 6 (macvlan par VLAN). macvlan donne à chaque conteneur sa propre adresse : sans cela, OPNsense ne verrait que l'adresse de l'hôte et la matrice serait inapplicable (module 8).

**Configuration et commandes**

*Script* — fichier `services/reseaux-A.sh`

```
#!/bin/bash
# services/reseaux-A.sh — réseaux Docker de l'état A (section 4.1). À lancer en root sur l'hôte Docker.
set -eu
TRUNK=ens33        # carte reliée à p6-trunk (« eth1 » du rapport)
WAN=ens34          # carte reliée à p6-wan (« eth2 » du rapport)
docker network create -d macvlan -o parent=$TRUNK.10 --subnet 10.10.0.0/24 --gateway 10.10.0.1 lan
docker network create -d macvlan -o parent=$TRUNK.20 --subnet 10.20.0.0/24 --gateway 10.20.0.1 dmz
docker network create -d macvlan -o parent=$WAN      --subnet 203.0.113.0/24 --gateway 203.0.113.1 wan
#   parent=ens33.10 : Docker crée lui-même la sous-interface VLAN 10 (le point dans le nom le lui indique)
#   --gateway : devient la route par défaut des conteneurs (la passerelle OPNsense du segment)
docker network ls --filter driver=macvlan
```

**Tester**

**

```
ip -d link show ens33.10 | head -3
docker run --rm --network lan --ip 10.10.0.50 projet6/outils ping -c 2 10.10.0.1
```
Sortie attendue :
```
5: ens33.10@ens33: <BROADCAST,MULTICAST,UP,LOWER_UP> …
    vlan protocol 802.1Q id 10 <REORDER_HDR>
64 bytes from 10.10.0.1: icmp_seq=1 ttl=64 time=0.41 ms
```
- `ens33.10@ens33` : sous-interface VLAN créée par Docker
- `id 10` : elle étiquette en VLAN 10
- `64 bytes from 10.10.0.1` : la passerelle OPNsense répond (FA4 l'autorise en état A)

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « invalid subinterface vlan name » | Nom de carte faux | Utiliser le vrai nom (`ip -br link`), par exemple ens33.10. |
| Pas de réponse de 10.10.0.1 | Sur OPNsense : `tcpdump -nei em1 vlan 10` voit-il l'ARP ? | Rien vu : carte du trunk sur le mauvais segment VMware (rôle 1). Vu sans réponse : VLAN 10 non activé (rôle 2). |
| Le réseau existe déjà | `docker network ls` | `docker network rm lan dmz` puis relancer le script. |

**C'est fini quand**

- [ ] Réseaux lan, dmz, wan créés
- [ ] Un conteneur de test joint 10.10.0.1 et 10.20.0.1

**À livrer.** Commit de `services/reseaux-A.sh`. Message au rôle 2 : « vous pouvez tester vos VLAN avec un conteneur ».

### Étape 3 — Déployer PostgreSQL et l'API Flask

**Quand :** 17 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes services-2 ; modules 4, 9

> **Mise à jour :** Fait par le rôle 1 : PostgreSQL 17 (10.10.0.70, base `catalogue`, rôle `api_catalogue`, `pg_hba` limité à 10.20.0.70 et 10.10.0.100) et l'API Flask + gunicorn (10.20.0.70:5000, `/api/produit` volontairement injectable). Mots de passe aléatoires dans `.env` (droits 600, jamais affichés).

**But.** La base « catalogue » sur 10.10.0.70 et l'API sur 10.20.0.70:5000, avec un point d'entrée sûr et un point d'entrée volontairement vulnérable.

**Pourquoi.** Section 7.5 : API « catalogue » avec un point d'entrée vulnérable à l'injection SQL pour S-03 (laboratoire uniquement) ; PostgreSQL seule cible de F04. Figure 5 (`/api/produits`) et section 10.1 (`/api/produit?id=`) : deux chemins (écart E17).

**Configuration et commandes**

*Création de la base* — fichier `services/postgres/init.sh`

```
#!/bin/bash
# services/postgres/init.sh — exécuté une seule fois, à la création de la base
set -e
psql -v ON_ERROR_STOP=1 -U postgres <<SQL
CREATE DATABASE catalogue;
CREATE ROLE app     LOGIN PASSWORD '$APP_PWD';       -- compte de l'API (F04)
CREATE ROLE lecteur LOGIN PASSWORD '$LECTEUR_PWD';   -- identifiants « volés » utilisés par S-04 et S-05
SQL
psql -v ON_ERROR_STOP=1 -U postgres -d catalogue <<'SQL'
CREATE TABLE produits (id integer PRIMARY KEY, nom text NOT NULL, prix numeric(8,2) NOT NULL, secret text);
INSERT INTO produits VALUES
  (1, 'Routeur', 120.00, 'remise-interne-15'),
  (2, 'Commutateur', 80.00, 'stock-bas'),
  (3, 'Câble RJ45', 4.50, 'fournisseur-X');
GRANT SELECT ON produits TO app, lecteur;
SQL
#   La colonne « secret » ne doit jamais sortir par /api/produits : elle sort par l'injection (S-03).
```

*API* — fichier `services/api/app.py`

```
# services/api/app.py — API « catalogue » (C5). LABORATOIRE UNIQUEMENT : /api/produit est volontairement vulnérable.
import os
import psycopg
from flask import Flask, jsonify, request

app = Flask(__name__)
DSN = os.environ["DSN"]                       # host=10.10.0.70 dbname=catalogue user=app … (compose.yml)

@app.get("/api/sante")
def sante():                                  # test de vie, utile à la sonde et à test_flux
    return {"ok": True}

@app.get("/api/produits")
def produits():                               # point d'entrée sûr : requête fixe
    with psycopg.connect(DSN) as c:
        rows = c.execute("SELECT id, nom, prix FROM produits ORDER BY id").fetchall()
    return jsonify([{"id": r[0], "nom": r[1], "prix": float(r[2])} for r in rows])

@app.get("/api/produit")
def produit():                                # VULNÉRABLE (S-03) : la valeur de « id » est collée dans le SQL
    ident = request.args.get("id", "")
    sql = f"SELECT id, nom, prix, secret FROM produits WHERE id = '{ident}'"   # NE JAMAIS FAIRE CELA EN VRAI
    try:
        with psycopg.connect(DSN) as c:
            rows = c.execute(sql).fetchall()
    except psycopg.Error as e:
        return {"erreur": str(e).splitlines()[0]}, 500
    return jsonify([{"id": r[0], "nom": r[1], "prix": float(r[2]), "secret": r[3]} for r in rows])

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, threaded=True)
```

*Image de l'API* — fichier `services/api/Dockerfile`

```
# services/api/Dockerfile — la construction télécharge Flask et psycopg (pip) : carte NAT branchée
FROM python:3.13-slim
RUN pip install --no-cache-dir flask "psycopg[binary]"
COPY app.py /app/app.py
CMD ["python", "/app/app.py"]
```

*Fichier de lancement complet (utilisé jusqu'à l'étape 6)* — fichier `services/compose.yml`

```
# services/compose.yml — mêmes conteneurs, mêmes adresses en A et en B ; seuls les réseaux changent.
# Lancement :  docker compose --env-file etat-A.env up -d   (ou etat-B.env)
name: projet6
networks:                                  # réseaux créés par reseaux-A.sh / reseaux-B.sh
  web:   {external: true, name: ${NET_WEB}}     # A : dmz       B : dmz-web
  dns:   {external: true, name: ${NET_DNS}}     # A : dmz       B : dmz-dns
  app:   {external: true, name: ${NET_APP}}     # A : dmz       B : dmz-app
  users: {external: true, name: ${NET_USERS}}   # A : lan       B : lan-users
  db:    {external: true, name: ${NET_DB}}      # A : lan       B : lan-db
  admin: {external: true, name: ${NET_ADMIN}}   # A : lan       B : lan-admin
  wan:   {external: true, name: wan}
services:
  postgres:                                # C5 — base de l'application (LAN-DB)
    image: postgres:17
    container_name: postgres
    environment: {POSTGRES_PASSWORD: ${PG_ADMIN_PWD}, APP_PWD: ${PG_APP_PWD}, LECTEUR_PWD: ${PG_LECTEUR_PWD}}
    volumes: ["./postgres/init.sh:/docker-entrypoint-initdb.d/init.sh:ro"]
    networks: {db: {ipv4_address: 10.10.0.70}}
  api:                                     # C5 — API Flask (DMZ-APP), point d'entrée vulnérable : labo uniquement
    build: ./api
    container_name: api
    environment: {DSN: "host=10.10.0.70 dbname=catalogue user=app password=${PG_APP_PWD} connect_timeout=3"}
    networks: {app: {ipv4_address: 10.20.0.70}}
  nginx:                                   # C5 — TLS + reverse proxy (DMZ-WEB)
    image: nginx:stable
    container_name: nginx
    volumes: ["./nginx/site.conf:/etc/nginx/conf.d/default.conf:ro", "./nginx/tls:/etc/nginx/tls:ro", "/srv/logs/nginx:/var/log/nginx"]   # journaux visibles sur l'hôte (rôle 6)
    networks: {web: {ipv4_address: 10.20.0.10}}
  iperf3:                                  # serveur iperf3 dans l'espace réseau de nginx (FA6 / F09 : 10.20.0.10:5201)
    image: projet6/outils
    container_name: iperf3
    network_mode: "service:nginx"
    command: ["iperf3", "-s"]
  bind9:                                   # C5 — DNS (DMZ-DNS)
    image: internetsystemsconsortium/bind9:9.20
    container_name: bind9
    volumes: ["./bind9:/etc/bind:ro"]
    networks: {dns: {ipv4_address: 10.20.0.40}}
  dmz-pivot:                               # C7 interne — hôte DMZ supposé compromis (S-04)
    image: projet6/outils
    container_name: dmz-pivot
    command: ["sleep", "infinity"]
    environment: {PGPASSWORD: ${PG_LECTEUR_PWD}}
    networks: {web: {ipv4_address: 10.20.0.20}}
  client-01:                               # C8 — poste légitime (sonde du rôle 3)
    image: projet6/outils
    container_name: client-01
    command: ["sleep", "infinity"]
    networks: {users: {ipv4_address: 10.10.0.10}}
  client-03:                               # C7 interne — poste supposé compromis (S-05)
    image: projet6/outils
    container_name: client-03
    command: ["sleep", "infinity"]
    environment: {PGPASSWORD: ${PG_LECTEUR_PWD}}
    networks: {users: {ipv4_address: 10.10.0.12}}
  admin:                                   # poste d'administration (F07, F08)
    image: projet6/outils
    container_name: admin
    command: ["sleep", "infinity"]
    networks: {admin: {ipv4_address: 10.10.0.100}}
  client-ext:                              # C8 — client externe légitime, sur le WAN (eth2)
    image: projet6/outils
    container_name: client-ext
    command: ["sleep", "infinity"]
    networks: {wan: {ipv4_address: 203.0.113.10}}
```

*Choix des réseaux par état* — fichier `services/etat-A.env, etat-B.env`

```
# services/etat-A.env
NET_WEB=dmz
NET_DNS=dmz
NET_APP=dmz
NET_USERS=lan
NET_DB=lan
NET_ADMIN=lan
# mots de passe : dans services/.env.secrets (non versionné), copiés ici au moment du lancement
PG_ADMIN_PWD=changez-moi
PG_APP_PWD=changez-moi
PG_LECTEUR_PWD=changez-moi

# services/etat-B.env : mêmes mots de passe, et
NET_WEB=dmz-web
NET_DNS=dmz-dns
NET_APP=dmz-app
NET_USERS=lan-users
NET_DB=lan-db
NET_ADMIN=lan-admin
```

*Construire l'image de l'API (carte NAT branchée le temps du téléchargement), puis lancer la base et l'API*

```
cd /opt/projet6/services
docker compose --env-file etat-A.env build api          # pip télécharge Flask et psycopg : carte NAT branchée
# Rôle 1 : débrancher la carte NAT, puis :
docker compose --env-file etat-A.env up -d postgres api
```

**Tester**

*Dans le conteneur de l'API (la requête part de l'API vers la base, à travers FA3)*

```
docker exec api python -c "import urllib.request;print(urllib.request.urlopen('http://127.0.0.1:5000/api/produits').read())"
```
Sortie attendue :
```
b'[{"id":1,"nom":"Routeur","prix":120.0},{"id":2,"nom":"Commutateur","prix":80.0},{"id":3,"nom":"C\\u00e2ble RJ45","prix":4.5}]'
```
- `trois produits, pas de « secret »` : l'API joint la base (FA3 l'autorise en état A) et le point d'entrée sûr ne divulgue rien

*Vérifier la vulnérabilité (labo uniquement)*

```
docker exec api python -c "import urllib.request,urllib.parse;q=urllib.parse.quote(\"1' OR '1'='1\");print(urllib.request.urlopen('http://127.0.0.1:5000/api/produit?id='+q).read())"
```
Sortie attendue :
```
b'[{"id":1,…,"secret":"remise-interne-15"},{"id":2,…},{"id":3,…}]'
```
- `trois lignes avec « secret »` : l'injection fonctionne : c'est l'effet que le rôle 5 cherchera dans S-03

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| L'API répond 500 « connection timeout » | `docker exec api python -c "import socket;socket.create_connection(('10.10.0.70',5432),3)"` | En état A, vérifier FA3 (rôle 2) ; en état B, F04. |
| « password authentication failed for user app » | Mot de passe différent entre .env et init.sh | Recréer la base : `docker compose down -v` (efface le volume) puis `up`. |
| init.sh ne s'exécute pas | Le volume de données existe déjà | Les scripts d'initialisation ne tournent qu'à la création : supprimer le volume. |

**C'est fini quand**

- [ ] postgres sur 10.10.0.70, base catalogue avec 3 produits
- [ ] api sur 10.20.0.70:5000 ; /api/produits sans « secret »
- [ ] /api/produit?id=… vulnérable, vérifié une fois

**À livrer.** Commit de `services/` (sans les mots de passe : `.env.secrets` non versionné). Message aux rôles 3 et 5 : « API prête : /api/produits (sûr), /api/produit?id= (vulnérable, S-03) ».

### Étape 4 — Configurer nginx : TLS et reverse proxy

**Quand :** 17 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes services-3 ; modules 4, 7

> **Mise à jour :** Fait par le rôle 1 : nginx 10.20.0.10, HTTP et HTTPS (certificat auto-signé), relais `/api/` vers l'API. Testé par FA1 depuis client-ext à travers 203.0.113.1.

**But.** nginx sur 10.20.0.10 sert HTTPS (certificat auto-signé), redirige HTTP vers HTTPS et relaie `/api/` vers l'API en HTTP clair.

**Pourquoi.** Section 7.5 : nginx (DMZ-WEB) : TLS et reverse proxy vers l'API. Section 11.3 : le TLS s'arrête à nginx, l'injection est détectée sur le saut nginx → API, en clair (F03).

**Configuration et commandes**

*Certificat auto-signé (sur l'hôte Docker)*

```
mkdir -p /opt/projet6/services/nginx/tls && cd /opt/projet6/services/nginx/tls
openssl req -x509 -newkey rsa:2048 -nodes -days 60 \
  -keyout site.key -out site.crt \
  -subj "/CN=www.labo.test" -addext "subjectAltName=DNS:www.labo.test,IP:203.0.113.1,IP:10.20.0.10"
#   -nodes : clé sans mot de passe (nginx démarre sans intervention)
#   subjectAltName : le certificat vaut pour le nom et les deux adresses (publique et interne)
```

*Configuration* — fichier `services/nginx/site.conf`

```
# services/nginx/site.conf — TLS et reverse proxy vers l'API (C5)
server {
    listen 80;                                        # FA1 / F01 publient aussi le port 80
    return 301 https://$host$request_uri;             # tout est renvoyé vers HTTPS
}
server {
    listen 443 ssl;
    ssl_certificate     /etc/nginx/tls/site.crt;      # certificat auto-signé (commande ci-dessous)
    ssl_certificate_key /etc/nginx/tls/site.key;
    access_log /var/log/nginx/projet6.log combined;   # vrai fichier (access.log est un lien vers la sortie standard) ; format lu par le rôle 6
    location /api/ {
        proxy_pass http://10.20.0.70:5000;            # F03 : saut nginx → API, en HTTP clair (Suricata le lit en B)
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $remote_addr;
        proxy_connect_timeout 3s;
    }
    location / {
        default_type text/plain;
        return 200 "Catalogue Projet 6\n";            # page d'accueil statique, servie par nginx lui-même
    }
}
```

*Lancer*

```
docker compose --env-file etat-A.env up -d nginx iperf3
```

**Tester**

*Depuis Kali (WAN), après les règles FA du rôle 2*

```
curl -sk https://203.0.113.1/api/produits | head -c 120; echo
curl -sI http://203.0.113.1 | head -2
```
Sortie attendue :
```
[{"id":1,"nom":"Routeur","prix":120.0},{"id":2,…
HTTP/1.1 301 Moved Permanently
Location: https://203.0.113.1/
```
- `[{"id":1 …` : toute la chaîne fonctionne : FA1 + NAT, nginx, API, FA3, PostgreSQL
- `301` : le port 80 renvoie vers HTTPS
- `-k` : accepte le certificat auto-signé

*Journal d'accès (lu par le rôle 6, sur l'hôte Docker)*

```
tail -n 1 /srv/logs/nginx/projet6.log
```
Sortie attendue :
```
203.0.113.66 - - [17/Nov/2026:16:02:11 +0000] "GET /api/produits HTTP/1.1" 200 112 "-" "curl/8.x"
```
- `203.0.113.66` : nginx voit la vraie adresse du client : pas de NAT de source
- `200 112` : code et taille de la réponse : preuve d'effet réel

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| 502 Bad Gateway | nginx ne joint pas 10.20.0.70:5000 | État A : vérifier que l'API tourne. État B : F03 (rôle 2). |
| Le journal d'accès est vide | `ls -l /srv/logs/nginx/` ; la directive access_log pointe-t-elle vers projet6.log ? | Dans l'image officielle, access.log est un lien vers la sortie standard : écrire dans projet6.log (fichier ci-dessus) et monter /srv/logs/nginx. |
| « SSL: certificate verify failed » | Client sans -k | Normal avec un certificat auto-signé ; la sonde utilise `verify=False`. |

**C'est fini quand**

- [ ] HTTPS répond sur 10.20.0.10 et via 203.0.113.1
- [ ] HTTP renvoie vers HTTPS
- [ ] /api/ relayé vers l'API
- [ ] Le journal d'accès est lisible

**À livrer.** Message au rôle 6 : le journal d'accès nginx est `/srv/logs/nginx/projet6.log` sur l'hôte Docker, format combined. Message au rôle 3 : « URL de la sonde : https://203.0.113.1/api/produits (externe), https://10.20.0.10/api/produits (interne) ».

### Étape 5 — Configurer bind9

**Quand :** 18 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes services-2 ; modules 1, 4

> **Mise à jour :** Fait par le rôle 1 : bind9 10.20.0.40, zone `p6.lab` faisant autorité, sans récursion. Testé par FA2 (réponse exacte vérifiée).

**But.** bind9 sur 10.20.0.40 fait autorité sur `labo.test` : réponse interne (10.20.0.10) pour le LAN et la DMZ, publique (203.0.113.1) pour le WAN.

**Pourquoi.** Section 7.5 : bind9 (DMZ-DNS) : zone publique et résolution interne. Flux FA2 / F02 (WAN, UDP/TCP 53) et F06 (LAN-USERS, UDP 53). Sonde DNS toutes les 5 s (section 7.8).

**Configuration et commandes**

*Configuration et zone* — fichier `services/bind9/`

```
// services/bind9/named.conf — zone publique et résolution interne (C5)
options {
    directory "/var/cache/bind";
    listen-on { any; };
    allow-query { any; };
    recursion no;                       // serveur faisant autorité seulement : pas de résolution vers Internet
};
view "interne" {
    match-clients { 10.10.0.0/24; 10.20.0.0/24; };   // couvre aussi les /26 et /27 de l'état B
    zone "labo.test" { type primary; file "/etc/bind/db.labo.test.interne"; };
};
view "externe" {
    match-clients { any; };
    zone "labo.test" { type primary; file "/etc/bind/db.labo.test.externe"; };
};

; services/bind9/db.labo.test.interne   (db.labo.test.externe : mêmes lignes avec 203.0.113.1)
$TTL 300
@     IN SOA ns1.labo.test. admin.labo.test. ( 2026111701 3600 600 86400 300 )
      IN NS  ns1.labo.test.
ns1   IN A   10.20.0.40
www   IN A   10.20.0.10
;  « .test » est réservé aux essais (RFC 2606) : aucun risque de collision avec un vrai domaine
```

*Lancer et vérifier la syntaxe*

```
docker compose --env-file etat-A.env up -d bind9
docker exec bind9 named-checkconf /etc/bind/named.conf
docker exec bind9 named-checkzone labo.test /etc/bind/db.labo.test.interne
```

**Tester**

**

```
docker compose --env-file etat-A.env up -d client-01 client-ext
docker exec client-01  dig +short @10.20.0.40 www.labo.test
docker exec client-ext dig +short @203.0.113.1 www.labo.test
```
Sortie attendue :
```
10.20.0.10
203.0.113.1
```
- `10.20.0.10` : vue interne : le LAN joint nginx directement (F05 en état B)
- `203.0.113.1` : vue externe, via la redirection FA2 / F02

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « connection timed out; no servers could be reached » | bind9 tourne-t-il ? `docker logs bind9` | Erreur de syntaxe dans named.conf ou la zone : `named-checkconf`. |
| client-ext reçoit 10.20.0.10 | Mauvaise vue choisie | Vérifier `match-clients` : 203.0.113.0/24 ne doit pas être dans la vue interne. |
| Le DNS marche en A mais pas en B pour client-01 | F06 en TCP au lieu d'UDP | Rôle 2 (écart E16). |

**C'est fini quand**

- [ ] named-checkconf et named-checkzone sans erreur
- [ ] Réponse interne pour le LAN, publique pour le WAN

**À livrer.** Commit de `services/bind9/`. Message au rôle 3 : « sonde DNS : dig @10.20.0.40 www.labo.test ».

### Étape 6 — Lancer les postes : clients, dmz-pivot, admin, client-ext, iperf3

**Quand :** 18 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes services-3, services-4, services-5 ; modules 2, 8

> **Mise à jour :** Fait par le rôle 1 : client-01 (10.10.0.10), client-03 (10.10.0.12), admin (10.10.0.100), dmz-pivot (10.20.0.20), client-ext (203.0.113.10), iperf3 (partage la pile réseau de nginx, port 5201). Image des postes : `p6/outils:1`. Les 10 conteneurs redémarrent seuls après un redémarrage de la VM.

**But.** Tous les conteneurs de hosts.csv en marche à leur adresse, avec les outils dont ont besoin la sonde et les scénarios.

**Pourquoi.** Section 7.7 : client-03 (S-05) et dmz-pivot (S-04) sont des positions d'attaque internes. Section 7.8 : client-ext (conteneur de l'hôte Docker relié au WAN, distinct de Kali) et client-01 portent la sonde. FA6 / F09 : serveur iperf3 sur 10.20.0.10:5201.

**Configuration et commandes**

*Tout lancer*

```
cd /opt/projet6/services
docker compose --env-file etat-A.env up -d
docker compose ps --format 'table {{.Name}}\t{{.State}}'
for c in nginx dmz-pivot bind9 api client-01 client-03 postgres admin client-ext; do
  printf "%-11s %s\n" "$c" "$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $c)"
done
```

> iperf3 n'a pas d'adresse propre : il partage l'espace réseau de nginx (10.20.0.10).

**Tester**

**

```
(sortie de la boucle ci-dessus)
```
Sortie attendue :
```
nginx       10.20.0.10
dmz-pivot   10.20.0.20
bind9       10.20.0.40
api         10.20.0.70
client-01   10.10.0.10
client-03   10.10.0.12
postgres    10.10.0.70
admin       10.10.0.100
client-ext  203.0.113.10
```
- `chaque ligne` : doit correspondre exactement à hosts.csv (page Contrats)

*Les outils des scénarios sont là*

```
docker exec client-03 sh -c 'nmap --version | head -1; psql --version'
```
Sortie attendue :
```
Nmap version 7.9x …
psql (PostgreSQL) 17.x
```
- `Nmap, psql` : S-05 pourra s'exécuter dans client-03

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « Address already in use » | Un conteneur de test a gardé l'adresse | `docker ps -a`, supprimer le conteneur de test. |
| client-ext ne joint pas 203.0.113.1 | Réseau wan sur la mauvaise carte | Le parent doit être la carte reliée à p6-wan (ens34). |
| iperf3 ne démarre pas | nginx arrêté (espace réseau partagé) | Démarrer nginx d'abord. |

**C'est fini quand**

- [ ] Les 9 conteneurs de hosts.csv tournent à leur adresse
- [ ] iperf3 écoute sur 10.20.0.10:5201
- [ ] test_flux du rôle 2 peut s'exécuter

**À livrer.** Message aux rôles 2, 3 et 5 : « tous les postes sont en place ». Fichier `docs/acces-attaquants.md` : les scénarios internes s'exécutent par `ssh root@10.99.0.20 docker exec client-03 …` et `… dmz-pivot …`.

### Étape 7 — Écrire l'outil de capture C10

**Quand :** 18–19 nov. · **Prérequis :** étapes services-6 ; modules 10, 3

**But.** Un script que l'orchestrateur appelle par SSH pour démarrer et arrêter une capture dans l'espace réseau d'un conteneur cible.

**Pourquoi.** Section 7.10 : un tcpdump par exécution, lancé dans l'espace réseau du conteneur cible ; le trafic entre deux conteneurs d'un même VLAN macvlan ne passe pas par l'interface parente. Les PCAP prouvent le blocage (section 9.4).

**Configuration et commandes**

*Script* — fichier `capture/capture.sh (installé dans /opt/projet6/capture/)`

```
#!/bin/bash
# capture/capture.sh — C10 : une capture par exécution, dans l'espace réseau du conteneur cible (section 7.10)
# Usage (appelé par l'orchestrateur du rôle 5, par SSH) :
#   capture.sh start <run_id> <point>     capture.sh stop <run_id> <point>
#   <point> : nginx | api | postgres | bind9   (le point kali-wan est capturé sur Kali par le rôle 5)
set -eu
ACTION=$1; RUN=$2; POINT=$3
DIR=/srv/captures; mkdir -p "$DIR"
PIDF="$DIR/.$RUN-$POINT.pid"
case "$POINT" in nginx|api|postgres|bind9) CTR=$POINT ;; *) echo "point inconnu : $POINT" >&2; exit 2 ;; esac
if [ "$ACTION" = start ]; then
  PID=$(docker inspect -f '{{.State.Pid}}' "$CTR")                  # processus du conteneur
  # -Z root : tcpdump garde les droits root pour écrire dans /srv/captures (sinon il passe sous l'utilisateur tcpdump)
  # Les messages de tcpdump sont gardés dans un fichier caché, à lire si le PCAP est vide.
  nsenter -t "$PID" -n tcpdump -i eth0 -U -s 0 -Z root -w "$DIR/${RUN}_${POINT}.pcap" >/dev/null 2>"$DIR/.$RUN-$POINT.err" &
  echo $! > "$PIDF"                                                  # mémorise tcpdump pour l'arrêter
  sleep 1                                                            # laisse tcpdump démarrer avant t0
elif [ "$ACTION" = stop ]; then
  kill -INT "$(cat "$PIDF")" && rm -f "$PIDF"                        # INT : tcpdump ferme proprement le fichier
  sleep 1; ls -l "$DIR/${RUN}_${POINT}.pcap"
fi
```

**Tester**

**

```
/opt/projet6/capture/capture.sh start TEST-S05-01 postgres
docker exec client-03 nc -z -w 2 10.10.0.70 5432
/opt/projet6/capture/capture.sh stop TEST-S05-01 postgres
tcpdump -nr /srv/captures/TEST-S05-01_postgres.pcap 'tcp[tcpflags] & tcp-syn != 0' | head -2
```
Sortie attendue :
```
-rw-r--r-- 1 root root 1024 … /srv/captures/TEST-S05-01_postgres.pcap
16:20:01.102 IP 10.10.0.12.43120 > 10.10.0.70.5432: Flags [S], …
16:20:01.102 IP 10.10.0.70.5432 > 10.10.0.12.43120: Flags [S.], …
```
- `Flags [S] puis [S.]` : SYN de client-03 puis SYN-ACK de la base : en état A, la connexion aboutit et seule cette capture le prouve

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Fichier vide ou absent | `cat /srv/captures/.<run_id>-<point>.err` | Message de tcpdump (droits, interface). Arrêt par KILL : toujours `kill -INT`, tcpdump vide son tampon. |
| « nsenter: cannot open … » | Lancé sans root | L'orchestrateur se connecte en root (clé du collecteur). |
| Capture sur eth1 vide en état A | Normal : trafic interne au macvlan | Capturer dans le conteneur (ce script). |

**C'est fini quand**

- [ ] start / stop fonctionnent pour nginx, api, postgres, bind9
- [ ] Le PCAP de test montre le SYN de client-03

**À livrer.** Commit de `capture/capture.sh`. Message aux rôles 5 et 6 : « capture.sh prêt ; fichiers dans /srv/captures/<run_id>_<point>.pcap, à rapatrier dans resultats/<etat>/<date>/pcap/ ».

### Étape 8 — Basculer les services en état B

**Quand :** 22–23 nov. · **Prérequis :** étapes parefeu-8, chef-7 ; modules 2, 3

> **Mise à jour :** Adapter les commandes à l'état A réel : arrêter d'abord l'état A avec `docker compose -p p6 down` (dans `~/p6-etat-a`), ce qui supprime `p6_lan`, `p6_dmz` et `p6_wan` ; les parents des réseaux de l'état B sont `ens33.11`…`ens33.23` et `ens34`. Prendre un instantané de `p6-docker` avant (panneau, « Prendre un instantané »).

**But.** Mêmes conteneurs, mêmes adresses, sur les six réseaux /26 et /27 de l'état B.

**Pourquoi.** Section 5.1 : les hôtes gardent leur adresse, seuls le masque et la passerelle changent. Section 6 : on ne change que des sous-interfaces et des règles, jamais le câblage.

**Configuration et commandes**

*Script* — fichier `services/reseaux-B.sh`

```
#!/bin/bash
# services/reseaux-B.sh — réseaux Docker de l'état B (section 5.1). Les adresses des hôtes ne changent pas.
set -eu
TRUNK=ens33; WAN=ens34
docker network create -d macvlan -o parent=$TRUNK.11 --subnet 10.10.0.0/26  --gateway 10.10.0.1  lan-users
docker network create -d macvlan -o parent=$TRUNK.12 --subnet 10.10.0.64/27 --gateway 10.10.0.65 lan-db
docker network create -d macvlan -o parent=$TRUNK.13 --subnet 10.10.0.96/27 --gateway 10.10.0.97 lan-admin
docker network create -d macvlan -o parent=$TRUNK.21 --subnet 10.20.0.0/27  --gateway 10.20.0.1  dmz-web
docker network create -d macvlan -o parent=$TRUNK.22 --subnet 10.20.0.32/27 --gateway 10.20.0.33 dmz-dns
docker network create -d macvlan -o parent=$TRUNK.23 --subnet 10.20.0.64/27 --gateway 10.20.0.65 dmz-app
docker network inspect wan >/dev/null 2>&1 || docker network create -d macvlan -o parent=$WAN --subnet 203.0.113.0/24 --gateway 203.0.113.1 wan
docker network ls --filter driver=macvlan
```

*Bascule*

```
cd /opt/projet6/services
docker compose --env-file etat-A.env down          # arrête les conteneurs ; la base est dans un volume anonyme créé par l'image postgres, conservé (seul « down -v » l'efface)
docker network rm lan dmz                          # supprime aussi les sous-interfaces ens33.10 et ens33.20
./reseaux-B.sh
docker compose --env-file etat-B.env up -d
```

**Tester**

**

```
docker exec postgres sh -c 'ip -br addr show eth0; ip route'
docker exec client-03 ip route
```
Sortie attendue :
```
eth0  UP  10.10.0.70/27
default via 10.10.0.65 dev eth0
10.10.0.64/27 dev eth0 proto kernel scope link src 10.10.0.70
default via 10.10.0.1 dev eth0
10.10.0.0/26 dev eth0 proto kernel scope link src 10.10.0.12
```
- `10.10.0.70/27 · via 10.10.0.65` : PostgreSQL dans LAN-DB, passerelle de LAN-DB (section 5.1)
- `10.10.0.0/26` : client-03 ne voit plus la base comme voisine : S-05 passera par pf

*Chaîne complète en état B*

```
docker exec client-ext curl -sk https://203.0.113.1/api/produits | head -c 60
```
Sortie attendue :
```
[{"id":1,"nom":"Routeur","prix":120.0},…
```
- `réponse JSON` : F01, F03 et F04 sont en place (rôle 2)

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « network dmz-web not found » | reseaux-B.sh non exécuté | Le lancer avant `up`. |
| PostgreSQL injoignable depuis l'API | Passerelle de LAN-DB | Vérifier `--gateway 10.10.0.65` ; sinon voir module 2, exercice 2. |
| Les données de la base ont disparu | `down -v` utilisé par erreur | Relancer : init.sh recrée la base à l'identique. |

**C'est fini quand**

- [ ] Six réseaux B créés, réseaux A supprimés
- [ ] Les 9 conteneurs à leur adresse, masques et passerelles de la section 5.1
- [ ] La chaîne client-ext → nginx → API → base fonctionne

**À livrer.** Commit de `services/reseaux-B.sh`. Message au rôle 2 : « services en état B, vous pouvez lancer test_flux ».

### Questions probables du jury

1. **Pourquoi macvlan et pas le réseau Docker par défaut ?**
   Le réseau par défaut cache les conteneurs derrière l'adresse de l'hôte (NAT) : OPNsense ne verrait qu'une source et ne pourrait pas appliquer F03 ou F04. Avec macvlan, chaque conteneur a sa MAC et son adresse dans son VLAN : c'est une machine du segment.

2. **Pourquoi l'API est-elle volontairement vulnérable ?**
   Pour que S-03 ait un effet mesurable : en état A, l'injection renvoie la colonne « secret » ; en état B, Suricata doit la bloquer sur le saut nginx → API. Le point d'entrée n'existe que dans le laboratoire isolé ; /api/produits montre la bonne pratique.

3. **Pourquoi la capture se fait-elle dans le conteneur ?**
   Deux conteneurs d'un même réseau macvlan se parlent dans le noyau de l'hôte, sans passer par eth1 : une capture sur l'hôte ou sur OPNsense ne voit pas S-05 en état A. Seule la capture dans l'espace réseau de la cible prouve l'effet réel (section 7.10).

4. **Qu'est-ce qui change pour un conteneur entre A et B ?**
   Seulement le réseau Docker : masque, passerelle et VLAN. L'adresse, l'image et la configuration restent identiques ; c'est ce qui rend la comparaison A/B honnête (ENF3).

5. **Pourquoi un serveur iperf3 dans l'espace réseau de nginx ?**
   FA6 et F09 ouvrent 10.20.0.10:5201. Partager l'espace réseau de nginx donne cette adresse au serveur iperf3 sans créer d'hôte supplémentaire ; la mesure de débit traverse exactement le même chemin que le trafic web.

### Démonstration finale (2 minutes)

Ce que vous montrez en 2 minutes : les services sont de vraies machines de leur segment.

1. `docker network ls` et `ip -d link show ens33.12` : un réseau macvlan par VLAN.
2. `docker exec postgres ip route` en état B : /27 et passerelle 10.10.0.65, même adresse qu'en A.
3. `curl -k https://203.0.113.1/api/produits` depuis client-ext : la chaîne complète.
4. Le PCAP d'une exécution capturé par capture.sh.

**Plan de secours :** captures des sorties dans le rapport.

## Rôle 5 — Validation et attaques (C6, C7)

**Confinement.** Toutes les commandes de ce rôle visent uniquement les adresses du laboratoire isolé (203.0.113.0/24, 10.10.0.0/24, 10.20.0.0/24). Avant chaque campagne, le rôle 1 vérifie que les cartes NAT des VM sont débranchées et relance `tests-cloisonnement.sh` depuis Kali (19/19 au 17 novembre) : aucune VM n'a de route vers l'extérieur (ENF1). N'exécutez jamais ces outils hors du laboratoire.Vous êtes le seul à « savoir » quand une attaque a lieu : `runs.csv` est la vérité terrain dont dépendent tous les indicateurs (section 8.4).

**Mise à jour du 17 novembre.** Kali tourne déjà : `p6-kali`, 203.0.113.66 sur le WAN (eth0, passerelle 203.0.113.1) et 10.99.0.66 sur MGMT (eth1), sans bureau. Les outils nmap, hping3, nikto, curl, psql, dig, nc et tcpdump sont installés. Console : TightVNC sur `127.0.0.1::5904` par le tunnel.

**Comptes et SSH réels.** Les VM Debian et Kali ont l'utilisateur `karim` avec `sudo`, pas de connexion `root`. SSH n'est accepté sur les VM que depuis Windows (10.99.0.2) et le collecteur (10.99.0.10) ; le SSH d'OPNsense est désactivé. Avant que l'orchestrateur tourne depuis le collecteur, le rôle 1 doit : installer la clé du collecteur sur p6-docker et p6-kali (pour `root`, ou pour `karim` avec un `sudo` sans mot de passe limité aux commandes utiles), et réactiver le SSH d'OPNsense sur MGMT, par clé seulement (la règle « Admin du pare-feu » autorise déjà le port 22 depuis 10.99.0.10).

### Étape 1 — Préparer Kali sans interface graphique

**Quand :** 14–15 nov. · **État :** en partie fait · **Prérequis :** étapes chef-4, chef-5 ; modules 9, 1

> **Mise à jour :** En grande partie fait par le rôle 1 (script `setup-kali.sh`) : adresses, outils, heure prise sur 10.99.0.10 (avec `systemctl enable --now chrony` : Kali désactive les services au démarrage), SSH qui n'écoute que sur 10.99.0.66, démarrage en mode texte. Le filtre SSH de Kali est la table `inet p6_ssh` (SSH accepté depuis 10.99.0.2 et 10.99.0.10). Reste à faire : la clé de l'orchestrateur (voir l'encadré du rôle) et tes propres outils Python.

**But.** Kali 2026.3 avec nmap, hping3, nikto, curl et tcpdump, pilotable en root par le collecteur, sans aucune route vers l'extérieur.

**Pourquoi.** Section 6 : Kali sans interface graphique, eth0 WAN, eth1 MGMT ; section 7.7 : positions d'attaque. ENF1 : aucun trafic d'attaque ne sort du laboratoire.

**Configuration et commandes**

*Outils (pendant une fenêtre de configuration)*

```
apt update
apt install -y nmap hping3 nikto curl tcpdump openssh-server
systemctl enable --now ssh
# Clé publique du collecteur pour root (connexion par clé uniquement)
mkdir -p /root/.ssh && chmod 700 /root/.ssh
echo "ssh-ed25519 AAAA… collecteur" >> /root/.ssh/authorized_keys
```

*Contrôle du confinement, à refaire avant chaque campagne*

```
ip route                         # attendu : seulement 203.0.113.0/24 et 10.99.0.0/24, AUCUNE ligne « default »
ping -c 1 -W 1 1.1.1.1           # attendu : « Network is unreachable »
nmap --version | head -1; hping3 --version | head -1; nikto -Version | head -2
```

**Tester**

*Depuis le collecteur*

```
ssh -o BatchMode=yes root@10.99.0.66 'nmap -sn -Pn 203.0.113.1 | tail -1'
```
Sortie attendue :
```
Nmap done: 1 IP address (1 host up) scanned in 0.05 seconds
```
- `BatchMode=yes` : aucune question interactive : c'est ainsi que l'orchestrateur se connectera
- `1 host up` : Kali voit OPNsense sur le WAN simulé

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « Permission denied (publickey) » | Clé absente de /root/.ssh/authorized_keys | Ajouter la clé du collecteur ; vérifier `PermitRootLogin prohibit-password`. |
| Une route par défaut existe | `ip route` | La supprimer (carte de configuration encore active) : `ip route del default`, et prévenir le rôle 1. |
| nmap -sS : « requires root » | Connexion avec un utilisateur normal | L'orchestrateur se connecte en root. |

**C'est fini quand**

- [ ] Outils installés, versions notées dans docs/versions.md
- [ ] SSH en root par clé depuis 10.99.0.10
- [ ] Aucune route par défaut sur Kali

**À livrer.** Message au rôle 1 : « Kali prête ; contrôle de confinement OK ».

### Étape 2 — Écrire scenarios.yaml

**Quand :** 15–16 nov. · **Prérequis :** étapes attaques-1, services-3 ; modules 9

**But.** Le fichier qui décrit les 6 scénarios et P-01 : machine, commande exacte, cible, durée maximale, critère « objectif atteint », points de capture.

**Pourquoi.** Section 10.1 (scénarios) et section 9.4 : le blocage se juge sur l'effet réel, donc chaque scénario doit dire ce qu'est « objectif atteint ». Corrections appliquées : commande curl de S-03 (écart E10), timeout de hping3.

**Configuration et commandes**

*Fichier complet* — fichier `campagne/scenarios.yaml`

```
# campagne/scenarios.yaml — scénarios de la section 10.1, rejoués à l'identique en A et en B
graine: 2026          # même graine dans les deux états : même ordre de passage (section 10.3)
n: 10                 # exécutions par scénario et par état (ENF2) ; réduire à 5 si le planning glisse (section 11.2)
pause_s: 30           # pause entre deux exécutions (section 7.6)
scenarios:
  - id: S-01
    description: Scan SYN depuis le WAN
    machine: kali                       # SSH sur la VM Kali
    commande: nmap -sS -Pn -p 1-1024 -T4 -oG - 203.0.113.1
    cible: 203.0.113.1
    duree_max_s: 240
    objectif_atteint: "au moins une ligne '/open/' dans la sortie"
    captures: [kali-wan, nginx]
  - id: S-02
    description: SYN flood sur le web
    machine: kali
    commande: timeout 30 hping3 -S --flood -p 443 203.0.113.1   # timeout : hping3 --flood ne s'arrête pas seul (code 124 attendu)
    cible: 203.0.113.1
    duree_max_s: 45
    objectif_atteint: "disponibilité de la sonde < 99 % entre t0 et t1 (sla.csv)"
    captures: [kali-wan]                # capturer nginx pendant un flood produirait un fichier énorme
  - id: S-03
    description: Scan de vulnérabilités web et injection SQL
    machine: kali
    commande: |
      nikto -h https://203.0.113.1 -maxtime 60s -nointeractive
      curl -sk -G https://203.0.113.1/api/produit --data-urlencode "id=1' OR '1'='1"
    cible: 203.0.113.1
    duree_max_s: 120
    objectif_atteint: "la sortie de curl contient « secret »"
    captures: [kali-wan, api]
  - id: S-04
    description: Pivot de la DMZ vers la base
    machine: dmz-pivot                  # docker exec sur l'hôte Docker
    commande: |
      nmap -sT -Pn -p 5432 -oG - 10.10.0.70
      PGCONNECT_TIMEOUT=5 psql -h 10.10.0.70 -U lecteur -d catalogue -c "select count(*) from produits"
    cible: 10.10.0.70
    duree_max_s: 60
    objectif_atteint: "psql renvoie une ligne (connexion ouverte et authentifiée)"
    captures: [postgres]
  - id: S-05
    description: Mouvement latéral du LAN vers la base
    machine: client-03
    commande: |
      nmap -sT -Pn -p 22,5432 -oG - 10.10.0.64/27
      PGCONNECT_TIMEOUT=5 psql -h 10.10.0.70 -U lecteur -d catalogue -c "select count(*) from produits"
    cible: 10.10.0.70
    duree_max_s: 120
    objectif_atteint: "psql renvoie une ligne (connexion ouverte et authentifiée)"
    captures: [postgres]
  - id: S-06
    description: Trafic fragmenté anormal
    machine: kali
    commande: |
      nmap -f -sS -Pn -p 443 -oG - 203.0.113.1
      hping3 -f -S -p 443 -c 20 203.0.113.1
    cible: 203.0.113.1
    duree_max_s: 60
    objectif_atteint: "aucun objectif de blocage : on mesure la détection"
    captures: [kali-wan]
  - id: P-01                            # état B seulement, 5 répétitions (section 10.3, étape 4)
    description: Panne de l'IPS pendant un SYN flood
    machine: kali
    commande: timeout 60 hping3 -S --flood -p 443 203.0.113.1
    cible: 203.0.113.1
    duree_max_s: 75
    objectif_atteint: "mesure : disponibilité avant, pendant, après ; temps de reprise"
    captures: [kali-wan]
    actions:                            # exécutées sur OPNsense, en secondes après t0 (section 8.5)
      - {a_s: 10, commande: "configctl ids stop"}     # à vérifier sur votre version (rôle 3, étape 7)
      - {a_s: 40, commande: "configctl ids start"}
```

**Tester**

*Sur le collecteur*

```
python3 -c "import yaml;d=yaml.safe_load(open('campagne/scenarios.yaml'));print(len(d['scenarios']),[s['id'] for s in d['scenarios']])"
```
Sortie attendue :
```
7 ['S-01', 'S-02', 'S-03', 'S-04', 'S-05', 'S-06', 'P-01']
```
- `7` : six scénarios et la panne

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « mapping values are not allowed here » | Deux-points non protégé dans une commande | Mettre la commande en bloc `\|` (comme S-03) ou entre guillemets. |
| S-03 : curl renvoie « URL using bad/illegal format » | Commande du rapport sans encodage | Utiliser `-G --data-urlencode` (écart E10). |
| psql demande un mot de passe | PGPASSWORD absent du conteneur | Il est fourni par compose.yml (rôle 4) pour dmz-pivot et client-03. |

**C'est fini quand**

- [ ] 7 entrées, chacune avec un critère objectif_atteint vérifiable
- [ ] Commandes testées une fois à la main (état A)

**À livrer.** Commit de `campagne/scenarios.yaml`. Message au rôle 6 : « critères objectif_atteint disponibles pour le calcul du blocage ».

### Étape 3 — Écrire l'orchestrateur C6

**Quand :** 16–18 nov. · **Prérequis :** étapes attaques-2, services-7, parefeu-2 ; modules 10, 11

**But.** Un programme qui rejoue les scénarios dans un ordre aléatoire reproductible, avec capture, t0, t1, sortie, code de retour, réinitialisation et pause.

**Pourquoi.** Section 7.6 et figure 8 : démarrer la capture, noter t0, lancer par SSH, noter t1, écrire la vérité terrain, arrêter la capture, réinitialiser (pfctl -k, levée des blocages), attendre 30 s. Section 10.3 : même graine en A et en B.

**Configuration et commandes**

*Programme complet* — fichier `campagne/orchestrateur.py`

```
#!/usr/bin/env python3
"""C6 — orchestrateur de campagnes (rapport, sections 7.6, 8.4, 10.3 ; figure 8).
Exécute chaque scénario N fois, produit la vérité terrain runs.csv, les sorties et les PCAP.
Dépendances sur le collecteur : apt install python3-yaml openssh-client"""
import argparse, csv, datetime as dt, os, random, shlex, subprocess, sys, threading, time
import yaml

SRC_IP = {"kali": "203.0.113.66", "client-03": "10.10.0.12", "dmz-pivot": "10.20.0.20"}   # hosts.csv
SSH = {"kali": "root@10.99.0.66", "docker": "root@10.99.0.20", "opnsense": "root@10.99.0.1"}  # par MGMT

def maintenant():                                   # horloge du collecteur (référence NTP), UTC, milliseconde
    return dt.datetime.now(dt.timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")

def ssh(cible, commande, simul, delai=None):
    if simul:
        print(f"    [simulation] ssh {SSH[cible]} {commande}")
        return 0, ""
    p = subprocess.run(["ssh", "-o", "BatchMode=yes", SSH[cible], commande],
                       capture_output=True, text=True, timeout=delai)
    return p.returncode, p.stdout + p.stderr

def executer(sc, simul):
    cmd = sc["commande"].strip()
    if sc["machine"] == "kali":
        return ssh("kali", f"sh -c {shlex.quote(cmd)}", simul, sc["duree_max_s"])
    return ssh("docker", f"docker exec {sc['machine']} sh -c {shlex.quote(cmd)}", simul, sc["duree_max_s"])

def capture(action, run_id, points, simul):         # C10 (rôle 4) ; le point kali-wan est sur Kali (écart E18)
    for pt in points:
        if pt == "kali-wan":
            if action == "start":
                ssh("kali", f"nohup tcpdump -i eth0 -U -w /tmp/{run_id}_kali-wan.pcap >/dev/null 2>&1 & echo $! > /tmp/{run_id}.pid; sleep 1", simul)
            else:
                ssh("kali", f"kill -INT $(cat /tmp/{run_id}.pid); sleep 1", simul)
        else:
            ssh("docker", f"/opt/projet6/capture/capture.sh {action} {run_id} {pt}", simul)

def rapatrier(run_id, points, dossier, simul):
    for pt in points:
        src = f"{SSH['kali']}:/tmp/{run_id}_kali-wan.pcap" if pt == "kali-wan" else f"{SSH['docker']}:/srv/captures/{run_id}_{pt}.pcap"
        if simul:
            print(f"    [simulation] scp {src} {dossier}/pcap/")
            continue
        subprocess.run(["scp", "-q", "-o", "BatchMode=yes", src, f"{dossier}/pcap/"], check=False)

def actions_programmees(sc, simul):                 # P-01 : arrêt puis redémarrage de Suricata
    fils = []
    for a in sc.get("actions", []):
        t = threading.Timer(0 if simul else a["a_s"], ssh, args=("opnsense", a["commande"], simul))
        t.start()
        fils.append(t)
    return fils

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--etat", choices=["A", "B"], required=True)
    ap.add_argument("--fichier", default="campagne/scenarios.yaml")
    ap.add_argument("--scenario", help="ne jouer que ce scénario, par exemple S-01 ou P-01")
    ap.add_argument("--n", type=int, help="remplace n (1 pour une répétition à blanc)")
    ap.add_argument("--simulation", action="store_true", help="affiche les commandes sans rien exécuter")
    ap.add_argument("--dossier", help="dossier de résultats, par exemple resultats/A/2026-11-20 (défaut : date du jour)")
    a = ap.parse_args()
    with open(a.fichier, encoding="utf-8") as f:
        conf = yaml.safe_load(f)
    n, pause = a.n or conf["n"], conf["pause_s"]
    scen = [s for s in conf["scenarios"] if (s["id"] == a.scenario) or (a.scenario is None and s["id"] != "P-01")]
    if not scen:
        sys.exit("aucun scénario sélectionné")
    if a.etat == "A" and any(s["id"] == "P-01" for s in scen):
        sys.exit("P-01 ne concerne que l'état B")
    random.Random(conf["graine"]).shuffle(scen)     # ordre aléatoire, identique en A et en B
    if a.simulation:
        dossier = f"resultats/{a.etat}/simulation"                  # la simulation ne pollue pas les vraies données
    else:
        dossier = a.dossier or f"resultats/{a.etat}/{dt.date.today().isoformat()}"
        # Un même dossier pour toute la campagne, même si elle passe minuit : le fixer avec --dossier.
    os.makedirs(f"{dossier}/sorties", exist_ok=True)
    os.makedirs(f"{dossier}/pcap", exist_ok=True)
    chemin = f"{dossier}/runs.csv"
    nouveau = not os.path.exists(chemin)
    with open(chemin, "a", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        if nouveau:
            w.writerow(["run_id", "scenario", "etat", "iteration", "t0_utc", "t1_utc", "src_ip", "cible_ip", "code_retour"])
        for sc in scen:
            for i in range(1, n + 1):
                run_id = f"{a.etat}-{sc['id'].replace('-', '')}-{i:02d}"
                print(f"{run_id} · {sc['description']}")
                capture("start", run_id, sc.get("captures", []), a.simulation)
                t0 = maintenant()
                fils = actions_programmees(sc, a.simulation)
                try:
                    code, sortie = executer(sc, a.simulation)
                except subprocess.TimeoutExpired:
                    code, sortie = 124, "délai maximal dépassé (duree_max_s)"
                t1 = maintenant()
                for t in fils:
                    t.join()
                capture("stop", run_id, sc.get("captures", []), a.simulation)
                rapatrier(run_id, sc.get("captures", []), dossier, a.simulation)
                with open(f"{dossier}/sorties/{run_id}.txt", "w", encoding="utf-8") as o:
                    o.write(sortie)
                w.writerow([run_id, sc["id"], a.etat, i, t0, t1, SRC_IP.get(sc["machine"], ""), sc["cible"], code])
                f.flush()
                src = SRC_IP.get(sc["machine"])
                if src:
                    ssh("opnsense", f"pfctl -k {src}", a.simulation)    # supprime les états de l'attaquant
                time.sleep(0 if a.simulation else pause)              # blocages temporaires expirés, trafic calmé
    print(f"Terminé : {chemin}")

if __name__ == "__main__":
    main()
```

**Tester**

*Simulation (n'exécute rien)*

```
python3 campagne/orchestrateur.py --etat A --scenario S-05 --n 1 --simulation
```
Sortie attendue :
```
A-S05-01 · Mouvement latéral du LAN vers la base
    [simulation] ssh root@10.99.0.20 /opt/projet6/capture/capture.sh start A-S05-01 postgres
    [simulation] ssh root@10.99.0.20 docker exec client-03 sh -c 'nmap -sT …'
    [simulation] ssh root@10.99.0.20 /opt/projet6/capture/capture.sh stop A-S05-01 postgres
    [simulation] scp root@10.99.0.20:/srv/captures/A-S05-01_postgres.pcap resultats/A/simulation/pcap/
    [simulation] ssh root@10.99.0.1 pfctl -k 10.10.0.12
Terminé : resultats/A/simulation/runs.csv
```
- `ordre des lignes` : capture, attaque, arrêt, rapatriement, réinitialisation : exactement la figure 8
- `pfctl -k 10.10.0.12` : les états de l'attaquant sont supprimés après chaque exécution

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| « Host key verification failed » | Première connexion SSH | Se connecter une fois à la main à 10.99.0.1, .20 et .66 pour accepter les clés. |
| pfctl -k ne fait rien | Le shell de root sur OPNsense est le menu console | Choisir /bin/sh comme shell de connexion de root (à vérifier sur votre version). |
| Le PCAP n'est pas rapatrié | Point de capture inconnu de capture.sh | Points valides : kali-wan, nginx, api, postgres, bind9. |

**C'est fini quand**

- [ ] La simulation affiche la séquence complète pour chaque scénario
- [ ] Une exécution réelle de S-05 produit runs.csv, sorties/…txt et pcap/…pcap

**À livrer.** Commit de `campagne/orchestrateur.py`. Message au rôle 6 : « exemple de runs.csv réel joint ».

### Étape 4 — Répétition à blanc

**Quand :** 19 nov. · **Prérequis :** étapes parefeu-7, services-7, ids-3, analyse-3 ; modules 9, 11

**But.** Une exécution réelle de chaque scénario en état A, pour valider les commandes, les critères et toute la chaîne de mesure avant la campagne.

**Pourquoi.** Jalon du 19 novembre (section 12) : test_flux conforme ; une exécution S-01 produit runs.csv + events.csv. C'est la preuve que les rôles 2, 3, 4 et 6 sont prêts.

**Configuration et commandes**

*Sur le collecteur*

```
python3 campagne/orchestrateur.py --etat A --n 1 --dossier resultats/A/essai     # 6 exécutions, environ 6 minutes
ls resultats/A/essai/
cat resultats/A/essai/runs.csv
python3 analyse/parse.py --dossier resultats/A/essai                                # rôle 6 : produit events.csv
```

**Tester**

**

```
grep -o '/open/' resultats/A/essai/sorties/A-S01-01.txt | wc -l
grep -c secret resultats/A/essai/sorties/A-S03-01.txt
tail -3 resultats/A/essai/runs.csv
```
Sortie attendue :
```
3
1
A-S04-01,S-04,A,1,2026-11-19T10:12:40.004Z,2026-11-19T10:12:47.210Z,10.20.0.20,10.10.0.70,0
A-S01-01,S-01,A,1,…,203.0.113.66,203.0.113.1,0
…
```
- `3` : trois occurrences de « /open/ » (grep -o compte chaque port, pas chaque ligne : nmap -oG met tous les ports sur une ligne) : 53, 80, 443 découverts, objectif atteint
- `secret` : S-03 en A : l'injection a réussi
- `code 0 pour S-04` : psql connecté : FA3 laisse passer dmz-pivot

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| S-02 renvoie 124 | Normal : timeout arrête hping3 | Le blocage de S-02 se juge sur sla.csv, pas sur le code. |
| Aucune alerte pour aucun scénario en A | Normal : pas d'IDS en état A | Vérifier seulement que filterlog et sla.csv arrivent. |
| events.csv vide | Collecte du rôle 6 pas prête | Escalader au rôle 1 : la campagne A ne peut pas partir. |

**C'est fini quand**

- [ ] Les 6 scénarios s'exécutent sans intervention
- [ ] Chaque objectif est jugé atteint en état A comme prévu (S-06 : port 443 vu ouvert)
- [ ] runs.csv et events.csv existent pour S-01

**À livrer.** Message au rôle 1 : « jalon du 19 atteint ». Les fichiers de l'essai restent dans `resultats/A/essai/` (pas dans les données de campagne).

### Étape 5 — Conduire la campagne A

**Quand :** 20–21 nov. · **Prérequis :** étapes parefeu-7, ids-3 ; modules 9, 10

**But.** 6 scénarios × 10 exécutions en état A, dans les conditions du protocole, archivés et marqués.

**Pourquoi.** Section 10.3 : restaurer le snapshot et vérifier test_flux ; 2 h de trafic légitime seul ; campagne de 6 × 10 exécutions, ordre aléatoire, pause de 30 s (environ 1 h 30) ; archiver ; même version d'outils et même graine dans les deux états.

**Configuration et commandes**

*Déroulé (collecteur)*

```
# 0. Rôle 1 : instantané etat-A restauré, cartes NAT débranchées (startConnected FALSE), tests-cloisonnement.sh = 19/19
# 1. Rôle 2 : test_flux A → 0 écart                (section 10.3, étape 1)
# 2. Rôle 3 : sonde démarrée, 2 h de trafic légitime seul, puis iperf3 de référence (étape 2)
CAMP=resultats/A/2026-11-20          # dossier fixé une fois pour toute la campagne (même si elle passe minuit)
mkdir -p $CAMP/iperf
ssh root@10.99.0.20 /opt/projet6/sonde/sonde.sh start
# 3. Campagne (étape 3) — ne touchez à rien pendant 1 h 30
python3 campagne/orchestrateur.py --etat A --dossier $CAMP
# 4. Fin : sonde arrêtée, sla.csv et iperf3 rapatriés, analyse, archivage (étape 5)
ssh root@10.99.0.20 /opt/projet6/sonde/sonde.sh stop /srv/sla/A
scp root@10.99.0.20:/srv/sla/A/sla.csv $CAMP/
scp root@10.99.0.20:/srv/sla/iperf-A.json $CAMP/iperf/A.json
python3 analyse/parse.py --dossier $CAMP
git add resultats/A && git commit -m "[rôle 5] Campagne A" && git tag campagne-A
```

**Tester**

**

```
wc -l resultats/A/*/runs.csv
ls resultats/A/*/pcap | wc -l
```
Sortie attendue :
```
61 resultats/A/2026-11-20/runs.csv
80
```
- `61` : 60 exécutions + l'en-tête
- `80` : 8 points de capture par tour (S-01 : 2, S-02 : 1, S-03 : 2, S-04 : 1, S-05 : 1, S-06 : 1) × 10 exécutions

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Une exécution échoue au milieu | Ligne manquante dans runs.csv | Relancer seulement ce scénario avec --scenario ; noter l'incident dans docs/journal-campagnes.md. |
| La sonde s'est arrêtée | sla.csv incomplet | Les disponibilités de la période sont invalides : relancer les exécutions concernées. |
| Retard sur le planning | — | Réduire N à 5 pour tous les scénarios, jamais pour un seul état (section 11.2). |

**C'est fini quand**

- [ ] 60 lignes dans runs.csv, toutes avec un code de retour
- [ ] PCAP et sorties présents pour chaque exécution
- [ ] Tag campagne-A poussé ; rôle 1 peut figer etat-A

**À livrer.** Message aux rôles 1 et 6 : « campagne A terminée, resultats/A/2026-11-20 » (dossier daté du jour de début).

### Étape 6 — Conduire la campagne B et la panne P-01

**Quand :** 25–26 nov. · **Prérequis :** étapes parefeu-9, ids-6, ids-7, services-8 ; modules 9, 7

**But.** La même campagne en état B, puis P-01 répété 5 fois, puis la mesure iperf3 « B sans IPS ».

**Pourquoi.** Section 10.3, étapes 3 et 4 : même protocole qu'en A ; P-01 en état B uniquement, 5 répétitions ; débit iperf3 en B avec Suricata désactivé (référence « B sans IPS »). Section 8.5 : le comportement en panne est mesuré, pas supposé.

**Configuration et commandes**

*Déroulé (collecteur)*

```
# Même préparation qu'en A : snapshot etat-B (ou état validé), test_flux B 0 écart, 2 h de trafic légitime
CAMP=resultats/B/2026-11-25
mkdir -p $CAMP/iperf
ssh root@10.99.0.20 /opt/projet6/sonde/sonde.sh start
python3 campagne/orchestrateur.py --etat B --dossier $CAMP                     # 60 exécutions, même graine qu'en A
python3 campagne/orchestrateur.py --etat B --dossier $CAMP --scenario P-01 --n 5
ssh root@10.99.0.20 /opt/projet6/sonde/sonde.sh stop /srv/sla/B
scp root@10.99.0.20:/srv/sla/B/sla.csv $CAMP/
# Puis rôle 3 : iperf3 « B avec IPS » et « B sans IPS », rapatriés ici
scp root@10.99.0.20:/srv/sla/iperf-B-avec-ips.json $CAMP/iperf/B-avec-ips.json
scp root@10.99.0.20:/srv/sla/iperf-B-sans-ips.json $CAMP/iperf/B-sans-ips.json
python3 analyse/parse.py --dossier $CAMP
git add resultats/B && git commit -m "[rôle 5] Campagne B et P-01" && git tag campagne-B
```

**Tester**

**

```
grep -c ',P-01,' resultats/B/2026-11-25/runs.csv
grep -c /open/ resultats/B/2026-11-25/sorties/B-S01-*.txt | head -3
```
Sortie attendue :
```
5
resultats/B/…/B-S01-01.txt:0
resultats/B/…/B-S01-02.txt:0
resultats/B/…/B-S01-03.txt:1
```
- `5` : les 5 répétitions de P-01
- `0 puis 1` : S-01 bloqué dans la plupart des exécutions ; une sonde passée avant le seuil peut révéler un port (écart E06) : c'est un résultat à rapporter, pas à masquer

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Tous les scénarios WAN échouent dès la 1re exécution | Suricata bloque Kali durablement | Vérifier le timeout du rate_filter (rôle 3) ; il doit être inférieur à la pause de 30 s. |
| P-01 : Suricata ne redémarre pas | Commande d'arrêt / démarrage non valide sur votre version | Tester les deux commandes à la main avec le rôle 3 avant la campagne. |
| La sonde est coupée pendant P-01 | C'est peut-être le résultat : l'interface ne transmet plus rien | Ne pas corriger : mesurer la durée et le temps de reprise (section 8.5). |

**C'est fini quand**

- [ ] 60 exécutions en B, même ordre qu'en A
- [ ] 5 exécutions de P-01
- [ ] Tag campagne-B poussé ; rôle 1 peut figer etat-B

**À livrer.** Message aux rôles 1 et 6 : « campagne B et P-01 terminées, resultats/B/2026-11-25 ».

### Questions probables du jury

1. **Comment évitez-vous qu'une exécution influence la suivante ?**
   Après chaque exécution : suppression des états pf de l'attaquant (pfctl -k), attente de 30 s pendant laquelle les blocages temporaires de Suricata expirent (timeout du rate_filter inférieur à la pause). L'ordre des scénarios est aléatoire pour ne pas favoriser un scénario par sa position.

2. **Comment savez-vous qu'une attaque est bloquée ?**
   Par son effet réel, défini dans scenarios.yaml : ports ouverts dans la sortie de nmap, connexion psql réussie, présence de « secret » dans la réponse, disponibilité de la sonde. On le vérifie aussi dans le PCAP côté cible (section 9.4).

3. **Vos scénarios ne favorisent-ils pas l'état B ?**
   Les commandes, N, la graine, le trafic de fond et les adresses sont identiques. En revanche, les règles locales de Suricata ont été écrites en connaissant les scénarios : les taux valent pour ces attaques (section 11.3).

4. **Comment garantissez-vous qu'aucune attaque ne sort du laboratoire ?**
   Toutes les cibles sont dans 203.0.113.0/24 ou les réseaux internes ; Kali n'a pas de route par défaut ; avant chaque campagne, le rôle 1 vérifie que les cartes de configuration sont débranchées. Les commandes passent par MGMT, mais les attaques partent du WAN ou des segments évalués.

5. **Pourquoi l'horloge du collecteur pour t0 et t1 ?**
   C'est la référence NTP du laboratoire : OPNsense et l'hôte Docker se synchronisent sur elle. Le temps de détection (première alerte − t0) compare deux horloges synchronisées.

### Démonstration finale (2 minutes)

Ce que vous montrez en 2 minutes : les mesures sont reproductibles et la vérité terrain est fiable.

1. `scenarios.yaml` : un scénario, sa commande et son critère « objectif atteint ».
2. Lancer `orchestrateur.py --scenario S-05 --n 1` en direct (état B) : la séquence s'affiche.
3. La ligne ajoutée à runs.csv, la sortie de psql (délai dépassé) et le PCAP vide de SYN.
4. Rappeler : même graine, même N, mêmes commandes en A et en B.

**Plan de secours :** enregistrement de l'exécution.

## Rôle 6 — Observabilité et analyse (C9, C11, NTP)

**Les chiffres du rapport final sortent de vos scripts, jamais d'un tableur rempli à la main.** Tout est rejouable à partir des fichiers bruts archivés : si une correction est faite, on relance la chaîne.

Trois corrections du pseudo-code de la section 9.4 sont appliquées : filtre sur l'adresse de l'attaquant (écart E08), relais déclaré pour S-03 (écart E09), relecture des fichiers bruts plutôt que du seul syslog UDP (écart E13).

**Mise à jour du 17 novembre.** Le collecteur tourne déjà (`p6-collecteur`, 10.99.0.10) : NTP pour tout MGMT et réception syslog UDP 514 rangée par machine dans `/var/log/p6/<adresse>.log` (par exemple `/var/log/p6/10.99.0.1.log` pour OPNsense), et non dans `/var/log/remote/`. Adapte les chemins de tes parseurs. Console : TightVNC sur `127.0.0.1::5903` par le tunnel.

**Comptes et SSH réels.** Les VM Debian et Kali ont l'utilisateur `karim` avec `sudo`, pas de connexion `root`. SSH n'est accepté sur les VM que depuis Windows (10.99.0.2) et le collecteur (10.99.0.10) ; le SSH d'OPNsense est désactivé. Avant que l'orchestrateur tourne depuis le collecteur, le rôle 1 doit : installer la clé du collecteur sur p6-docker et p6-kali (pour `root`, ou pour `karim` avec un `sudo` sans mot de passe limité aux commandes utiles), et réactiver le SSH d'OPNsense sur MGMT, par clé seulement (la règle « Admin du pare-feu » autorise déjà le port 22 depuis 10.99.0.10).

### Étape 1 — Synchroniser les horloges du laboratoire (NTP)

**Quand :** 14 nov. · **État :** ✅ déjà fait · **Prérequis :** étapes chef-4, chef-5 ; modules 10

> **Mise à jour :** Fait par le rôle 1 (script `setup-collecteur.sh`) : chrony serveur sur le collecteur (`allow 10.99.0.0/24`, `local stratum 8`), clients : OPNsense (Services › Network Time, 10.99.0.10 seul), p6-docker et Kali. OPNsense avait 17 h 35 de retard après une suspension : corrigé le 16 novembre. Contrôle : `chronyc clients` sur le collecteur. Le collecteur prend son heure de départ de Windows (horloge de VMware).

**But.** Le collecteur sert l'heure à toutes les VM, et l'écart entre horloges est mesuré avant chaque campagne.

**Pourquoi.** Section 6 : les horloges des 4 VM sont synchronisées par NTP sur le collecteur, condition nécessaire au calcul des temps de détection. Risque « horloges désynchronisées » (section 11.2) : contrôle du décalage avant chaque campagne.

**Configuration et commandes**

*Installer chrony (collecteur, hôte Docker, Kali) pendant une fenêtre de configuration*

```
apt install chrony     # remplace systemd-timesyncd, désactivé automatiquement à l'installation
systemctl is-active systemd-timesyncd chrony
```

> Résultat attendu : `inactive` puis `active`. Deux clients NTP en même temps se contrediraient (à vérifier sur votre version).

*Serveur NTP sur le collecteur* — fichier `/etc/chrony/chrony.conf (ajouts)`

```
# Supprimer ou commenter les lignes « pool … » : le laboratoire n'a pas d'accès à Internet
local stratum 10          # le collecteur fait référence même sans source externe
allow 10.99.0.0/24        # sert l'heure sur MGMT uniquement
```

> Puis `systemctl restart chrony`. Le collecteur prend son heure de départ de l'hôte Windows (horloge de VMware) : l'exactitude absolue importe peu, l'accord entre machines est ce qui compte.

*Clients : hôte Docker et Kali (OPNsense : rôle 2)* — fichier `/etc/chrony/chrony.conf`

```
# remplacer les lignes pool par :
server 10.99.0.10 iburst  # le collecteur, seule référence
```

**Tester**

*Avant chaque campagne, sur le collecteur*

```
chronyc clients
for h in 10.99.0.20 10.99.0.66; do ssh root@$h 'chronyc tracking | grep -E "Reference|System time"'; done
```
Sortie attendue :
```
Hostname         NTP   Drop Int IntL Last
10.99.0.1         12      0   6   -    31
10.99.0.20        12      0   6   -    40
10.99.0.66        12      0   6   -    18
Reference ID    : 0A63000A (10.99.0.10)
System time     : 0.000041210 seconds fast of NTP time
```
- `trois clients` : OPNsense, hôte Docker, Kali interrogent le collecteur
- `0.000041 seconds` : écart de 41 µs : négligeable devant un temps de détection en millisecondes

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Un client n'apparaît pas | Pare-feu du collecteur (UDP 123 depuis 10.99.0.0/24) | Rôle 1, étape 5. |
| « Reference ID : 00000000 » | Le client ne joint pas le serveur | Vérifier `server 10.99.0.10` et la route MGMT. |
| Écart de plusieurs secondes après un rollback | Snapshot avec RAM restauré | Attendre la resynchronisation (ou `chronyc makestep`) avant toute mesure. |

**C'est fini quand**

- [ ] Les 3 clients synchronisés sur 10.99.0.10
- [ ] Écart < 10 ms, noté dans docs/jalons.md avant chaque campagne

**À livrer.** Message à tous : « NTP en place ; contrôle à faire avant chaque campagne (commande ci-dessus) ».

### Étape 2 — Recevoir les journaux : rsyslog

**Quand :** 15–16 nov. · **État :** en partie fait · **Prérequis :** étapes chef-5 ; modules 10

> **Mise à jour :** En partie fait par le rôle 1 : rsyslog reçoit en UDP 514 et range chaque source dans `/var/log/p6/<adresse>.log` ; OPNsense envoie déjà ses journaux (System › Settings › Logging › Remote, 10.99.0.10 UDP 514), lignes `filterlog` reçues. Restent : le journal d'accès de nginx depuis p6-docker, TCP 514 si tu le gardes (écart E13), et les noms de fichiers attendus par ton parseur.

**But.** Le collecteur reçoit filterlog et les alertes EVE d'OPNsense, et le journal d'accès de nginx, dans des fichiers horodatés précisément.

**Pourquoi.** Section 7.9 : rsyslog reçoit filterlog et EVE JSON d'OPNsense, et les journaux Docker. Section 6 : syslog vers le collecteur est l'un des trois usages autorisés de MGMT.

**Configuration et commandes**

*Collecteur* — fichier `/etc/rsyslog.d/10-projet6.conf`

```
module(load="imudp")
input(type="imudp" port="514")              # syslog UDP (cible configurée dans OPNsense)
module(load="imtcp")
input(type="imtcp" port="514")              # syslog TCP : moins de pertes pendant un flood (écart E13)
# RSYSLOG_FileFormat : horodatage complet (date, microsecondes, fuseau), indispensable au calcul du TTD
if ($fromhost-ip == "10.99.0.1") then {
    action(type="omfile" file="/var/log/remote/opnsense.log" template="RSYSLOG_FileFormat")
    stop
}
if ($fromhost-ip == "10.99.0.20") then {
    action(type="omfile" file="/var/log/remote/docker.log" template="RSYSLOG_FileFormat")
    stop
}
```

> Debian 13 n'installe plus rsyslog par défaut : `apt install rsyslog` pendant une fenêtre de configuration. Puis `systemctl restart rsyslog`.

*Hôte Docker : envoyer le journal d'accès de nginx* — fichier `/etc/rsyslog.d/20-nginx.conf (p6-docker)`

```
module(load="imfile")
input(type="imfile" File="/srv/logs/nginx/projet6.log" Tag="nginx:" Facility="local6")   # fichier écrit par nginx (rôle 4)
local6.* action(type="omfwd" target="10.99.0.10" port="514" protocol="tcp")             # vers le collecteur
```

> Sur l'hôte Docker aussi : `apt install rsyslog` pendant une fenêtre de configuration (absent par défaut sous Debian 13), puis `systemctl restart rsyslog`.

**Tester**

**

```
tail -n 2 /var/log/remote/opnsense.log
tail -n 1 /var/log/remote/docker.log
```
Sortie attendue :
```
2026-11-17T15:02:11.420318+01:00 opnsense filterlog[412]: 77,,,fae5…,em0,match,block,in,4,0x0,,64,…,203.0.113.66,203.0.113.1,40001,22,0,S,…
2026-11-17T15:02:12.001+01:00 opnsense suricata[901]: … (vide en état A)
2026-11-17T16:02:11.000000+00:00 docker nginx: 203.0.113.66 - - [17/Nov/2026:16:02:11 +0000] "GET /api/produits HTTP/1.1" 200 112 …
```
- `…420318+01:00` : horodatage à la microseconde avec fuseau : converti en UTC par parse.py
- `filterlog[412]:` : l'étiquette qui permet au parseur de reconnaître la ligne

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Rien n'arrive | `tcpdump -ni ens32 port 514` sur le collecteur | Rien sur le fil : cible syslog d'OPNsense (rôle 2). Paquets vus mais pas de fichier : pare-feu du collecteur ou configuration rsyslog. |
| Horodatage sans année ni millisecondes | Modèle de fichier par défaut | Utiliser `RSYSLOG_FileFormat`. |
| Le journal nginx n'arrive pas | Fichier vide côté hôte Docker | Rôle 4, étape 4 : nginx doit écrire dans /srv/logs/nginx/projet6.log. |

**C'est fini quand**

- [ ] filterlog d'OPNsense reçu avec un horodatage complet
- [ ] Journal d'accès nginx reçu
- [ ] Alertes EVE reçues (vérifiable en état B)

**À livrer.** Commit de `analyse/rsyslog/`. Message au rôle 2 : « syslog reçu, merci de garder aussi les journaux locaux ».

### Étape 3 — Écrire le parseur : events.csv

**Quand :** 16–19 nov. · **Prérequis :** étapes analyse-2 ; modules 10, 5

**But.** Un programme qui rapatrie les fichiers bruts et écrit events.csv au schéma commun, trié dans l'ordre chronologique.

**Pourquoi.** Section 7.9 : un parseur convertit chaque ligne vers le schéma commun (ts_utc, source, src_ip, dst_ip, dst_port, proto, action, sid, message). Jalon du 19 novembre : une exécution S-01 produit runs.csv et events.csv. Les fichiers bruts d'OPNsense complètent syslog (écart E13).

**Configuration et commandes**

*Parseur* — fichier `analyse/parse.py`

```
#!/usr/bin/env python3
"""C9 — collecte et normalisation (section 7.9). Rapatrie les journaux bruts, puis écrit events.csv au schéma commun.
Usage : parse.py --etat A [--date AAAA-MM-JJ] [--sans-collecte]
        parse.py --dossier resultats/A/essai [--sans-collecte]
Les événements sont limités à la période couverte par runs.csv et sla.csv (± 1 min), s'ils existent."""
import argparse, csv, datetime as dt, glob, json, os, re, subprocess

UTC = dt.timezone.utc
def iso(d):                                                   # horodatage commun : UTC, milliseconde, suffixe Z
    return d.astimezone(UTC).isoformat(timespec="milliseconds").replace("+00:00", "Z")

def collecter(d):                                             # fichiers d'OPNsense et de l'hôte Docker (écart E13)
    os.makedirs(f"{d}/brut", exist_ok=True)
    for src, dst in [("root@10.99.0.1:/var/log/filter/*.log", "filterlog/"),
                     ("root@10.99.0.1:/var/log/suricata/eve.json*", ""),      # eve.json et ses fichiers tournés
                     ("root@10.99.0.20:/srv/logs/nginx/projet6.log", "nginx.log")]:
        os.makedirs(f"{d}/brut/filterlog", exist_ok=True)
        subprocess.run(["scp", "-q", "-o", "BatchMode=yes", src, f"{d}/brut/{dst}"], check=False)

# filterlog : format RFC 5424 d'OPNsense (« filterlog 412 - [meta …] 82,,, ») ou format de fichier rsyslog (« filterlog[412]: 82,,, »)
RX_FL = re.compile(r"(\d{4}-\d\d-\d\dT[\d:.]+(?:Z|[+-]\d\d:?\d\d)).*?filterlog\S*\s+(?:\d+\s+-\s+(?:\[[^\]]*\]|-)\s+)?(\d+,.*)$")
def filterlog(chemin, w):
    for ligne in open(chemin, encoding="utf-8", errors="replace"):
        m = RX_FL.search(ligne.strip())
        if not m: continue
        f = m.group(2).split(",")
        if len(f) < 20 or f[8] != "4": continue              # IPv4 seulement
        proto = f[16]
        dport = f[21] if proto in ("tcp", "udp") and len(f) > 21 else ""
        ts = dt.datetime.fromisoformat(m.group(1).replace("Z", "+00:00"))
        w.writerow([iso(ts), "pf", f[18], f[19], dport, proto, f[6], "", f"règle {f[0]} {f[3][:8]} {f[4]}"])

def eve(chemin, w):
    for ligne in open(chemin, encoding="utf-8", errors="replace"):
        try: e = json.loads(ligne)
        except ValueError: continue
        if e.get("event_type") != "alert": continue
        a = e["alert"]
        ts = dt.datetime.strptime(e["timestamp"], "%Y-%m-%dT%H:%M:%S.%f%z")
        w.writerow([iso(ts), "suricata", e.get("src_ip", ""), e.get("dest_ip", ""), e.get("dest_port", ""),
                    e.get("proto", "").lower(), "drop" if a.get("action") == "blocked" else "alert",
                    a.get("signature_id", ""), a.get("signature", "")])

RX_NG = re.compile(r'^(\S+) \S+ \S+ \[([^\]]+)\] "(\S+) (\S+)[^"]*" (\d{3}) ')
def nginx(chemin, w):
    for ligne in open(chemin, encoding="utf-8", errors="replace"):
        m = RX_NG.match(ligne)
        if not m: continue
        ts = dt.datetime.strptime(m.group(2), "%d/%b/%Y:%H:%M:%S %z")
        w.writerow([iso(ts), "nginx", m.group(1), "10.20.0.10", "443", "tcp", m.group(5), "", f"{m.group(3)} {m.group(4)}"])

def bornes(d):                                                # période de la campagne : runs.csv et sla.csv
    ts = []
    for nom, cols in (("runs.csv", ("t0_utc", "t1_utc")), ("sla.csv", ("ts_utc",))):
        if os.path.exists(f"{d}/{nom}"):
            for r in csv.DictReader(open(f"{d}/{nom}", encoding="utf-8")):
                ts += [dt.datetime.fromisoformat(r[c].replace("Z", "+00:00")) for c in cols if r.get(c)]
    if not ts: return None
    marge = dt.timedelta(minutes=1)
    return iso(min(ts) - marge), iso(max(ts) + marge)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--etat", choices=["A", "B"])
    ap.add_argument("--date", default=dt.date.today().isoformat())
    ap.add_argument("--dossier", help="dossier de résultats explicite (remplace --etat et --date)")
    ap.add_argument("--sans-collecte", action="store_true")
    a = ap.parse_args()
    if not a.dossier and not a.etat: ap.error("--etat ou --dossier est obligatoire")
    d = a.dossier or f"resultats/{a.etat}/{a.date}"
    if not a.sans_collecte: collecter(d)
    tmp = f"{d}/events.tmp"
    with open(tmp, "w", newline="", encoding="utf-8") as o:
        w = csv.writer(o)
        for p in sorted(glob.glob(f"{d}/brut/filterlog/*")): filterlog(p, w)
        for p in sorted(glob.glob(f"{d}/brut/eve.json*")):
            if not p.endswith((".gz", ".bz2", ".xz")): eve(p, w)           # fichiers compressés : décompresser d'abord
        for p in sorted(glob.glob(f"{d}/brut/nginx*.log")): nginx(p, w)
    lignes = sorted(csv.reader(open(tmp, encoding="utf-8")), key=lambda r: r[0])   # ordre chronologique
    b = bornes(d)
    if b: lignes = [r for r in lignes if b[0] <= r[0] <= b[1]]            # même format ISO : comparaison de textes
    with open(f"{d}/events.csv", "w", newline="", encoding="utf-8") as o:
        w = csv.writer(o)
        w.writerow(["ts_utc", "source", "src_ip", "dst_ip", "dst_port", "proto", "action", "sid", "message"])
        w.writerows(lignes)
    os.remove(tmp)
    print(f"{len(lignes)} événements dans {d}/events.csv")

if __name__ == "__main__":
    main()
```

*Faux positifs par sid (pour le rôle 3)* — fichier `analyse/fp.py`

```
#!/usr/bin/env python3
"""Faux positifs par sid sur une période de trafic légitime seul (section 9.3, utilisé par le rôle 3).
Usage : fp.py events.csv --debut 2026-11-24T08:00:00Z --fin 2026-11-24T10:00:00Z  > fp_par_sid.csv
        fp.py events.csv --duree-h 2          (si events.csv ne contient que la période de mesure)"""
import argparse, collections, csv, datetime as dt
ap = argparse.ArgumentParser(); ap.add_argument("events")
ap.add_argument("--debut"); ap.add_argument("--fin"); ap.add_argument("--duree-h", type=float)
a = ap.parse_args()
lire = lambda s: dt.datetime.fromisoformat(s.replace("Z", "+00:00"))
if a.debut and a.fin: duree = (lire(a.fin) - lire(a.debut)).total_seconds() / 3600
elif a.duree_h: duree = a.duree_h
else: ap.error("donner --debut et --fin, ou --duree-h")
def dedans(r):
    t = lire(r["ts_utc"])
    return (not a.debut or t >= lire(a.debut)) and (not a.fin or t <= lire(a.fin))
c = collections.Counter(r["sid"] for r in csv.DictReader(open(a.events, encoding="utf-8")) if r["source"] == "suricata" and dedans(r))
print("sid,alertes,fp_par_heure")
for sid, n in c.most_common():
    print(f"{sid},{n},{n / duree:.2f}")
```

**Tester**

**

```
python3 analyse/parse.py --etat A --date 2026-11-19
head -3 resultats/A/2026-11-19/events.csv
```
Sortie attendue :
```
142 événements dans resultats/A/2026-11-19/events.csv
ts_utc,source,src_ip,dst_ip,dst_port,proto,action,sid,message
2026-11-19T09:02:10.533Z,pf,203.0.113.66,203.0.113.1,22,tcp,block,,règle 77 fae55933 em0
```
- `…Z` : heure convertie en UTC
- `pf … block` : ligne filterlog normalisée : c'est le format attendu par correle.py

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| 0 événement | Fichiers absents de brut/, ou période de runs.csv / sla.csv sans rapport avec les journaux | Vérifier la collecte (scp) ou utiliser --sans-collecte avec des fichiers copiés à la main ; vérifier les dates de runs.csv. |
| Lignes filterlog ignorées | Format de ligne différent | Comparer une ligne brute à l'expression RX_FL ; ajuster la partie qui précède les champs. |
| « time data … does not match format » | Format d'horodatage EVE différent | Afficher une ligne d'eve.json ; adapter le format de strptime. |

**C'est fini quand**

- [ ] events.csv produit pour l'essai du 19 novembre (`parse.py --dossier resultats/A/essai`)
- [ ] Les trois sources présentes (pf, suricata en état B, nginx)
- [ ] Ordre chronologique vérifié

**À livrer.** Commit de `analyse/parse.py` et `analyse/fp.py`. Message aux rôles 1, 3, 5 : « parseur prêt ».

### Étape 4 — Exploiter les captures (C10)

**Quand :** 20–26 nov. · **Prérequis :** étapes services-7, attaques-3 ; modules 10, 3

**But.** Pour chaque exécution, les PCAP rangés, et une vérification automatique de l'effet réel côté cible.

**Pourquoi.** Section 7.10 : les PCAP servent à prouver si l'attaque a atteint sa cible (critère de blocage) et à fournir les captures commentées. Section 9.4 : effet vérifié dans la sortie de l'outil et dans le PCAP côté cible.

**Configuration et commandes**

*Vérifications en ligne de commande (collecteur, tshark installé)*

```
# La base a-t-elle répondu à client-03 ? (S-05)
tshark -r resultats/A/2026-11-20/pcap/A-S05-01_postgres.pcap -Y "ip.src==10.10.0.70 && ip.dst==10.10.0.12 && tcp.flags.syn==1 && tcp.flags.ack==1"
# La requête d'injection est-elle arrivée à l'API ? (S-03)
# -d : l'API écoute sur 5000, que tshark ne décode pas en HTTP par défaut
tshark -r resultats/A/2026-11-20/pcap/A-S03-01_api.pcap -d tcp.port==5000,http -Y 'http.request.uri contains "OR"' -T fields -e http.request.uri
# Fragments vus par Kali (S-06)
tshark -r resultats/A/2026-11-20/pcap/A-S06-01_kali-wan.pcap -Y "ip.flags.mf==1 || ip.frag_offset>0" | wc -l
```

**Tester**

**

```
(première commande, état A puis état B)
```
Sortie attendue :
```
  2 0.000412 10.10.0.70 → 10.10.0.12 TCP 74 5432 → 43120 [SYN, ACK] Seq=0 Ack=1 …
(état B : aucune ligne)
```
- `SYN, ACK de 10.10.0.70 vers 10.10.0.12` : état A : la base a répondu à l'attaquant ; pas bloqué au réseau. Le filtre sur la destination écarte les réponses à l'API (trafic légitime F05).
- `aucune ligne` : état B : la base n'a jamais reçu le SYN ; c'est la preuve du blocage

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| PCAP vide | Capture arrêtée par KILL, ou mauvais point | capture.sh stop envoie INT ; vérifier le point de capture dans scenarios.yaml. |
| Fichiers énormes pendant S-02 | Capture de nginx pendant un flood | S-02 ne capture que kali-wan (scenarios.yaml). |
| tshark absent | — | `apt install tshark` pendant une fenêtre de configuration. |

**C'est fini quand**

- [ ] Chaque exécution a ses PCAP au bon nom
- [ ] Les vérifications tshark donnent le résultat attendu sur l'essai du 19

**À livrer.** Archive datée des PCAP (hors Git) et message au rôle 1 : emplacement de l'archive.

### Étape 5 — Écrire la corrélation C11

**Quand :** 22–24 nov. (sur A), relancée le 27 sur B · **Prérequis :** étapes analyse-3, attaques-5 ; modules 10

**But.** Pour chaque exécution : détecté, temps de détection, bloqué ; pour chaque état : faux positifs par heure, disponibilité, latence.

**Pourquoi.** Section 9.4 et figure 13 : fenêtre [t0, t1 + 10 s] ; détecté = au moins une alerte ; TTD = première alerte − t0 ; bloqué = objectif non atteint (outil + PCAP) ; faux positifs = alertes hors de toute fenêtre, par heure de trafic légitime. Corrections E08 et E09 appliquées.

**Configuration et commandes**

*Corrélation* — fichier `analyse/correle.py`

```
#!/usr/bin/env python3
"""C11 — corrélation et indicateurs (section 9.4, figure 13), avec les corrections E08 (filtre sur l'attaquant)
et E09 (relais déclarés). Usage : correle.py resultats/A/<date> resultats/B/<date>
Dépendances : apt install python3-pandas python3-yaml tshark"""
import argparse, os, re, subprocess
import pandas as pd

DELTA = pd.Timedelta(seconds=10)                               # tolérance après la fin de l'attaque
RELAIS = {"S-03": [("10.20.0.10", "10.20.0.70")]}              # nginx relaie la requête de l'attaquant vers l'API

def objectif_atteint(sc, sortie, sla_fenetre):                 # l'effet réel (critère de blocage, section 9.4)
    if sc == "S-01": return "/open/" in sortie
    if sc in ("S-02", "P-01"): return bool(len(sla_fenetre)) and sla_fenetre["succes"].mean() < 0.99
    if sc == "S-03": return "secret" in sortie
    if sc in ("S-04", "S-05"): return re.search(r"\(1 (row|ligne)\)", sortie) is not None
    return None                                                # S-06 : détection seulement

def synack_dans_pcap(fichier, cible, attaquant):               # « PCAP côté cible » : la base a-t-elle répondu À L'ATTAQUANT ?
    filtre = f"ip.src=={cible} && ip.dst=={attaquant} && tcp.flags.syn==1 && tcp.flags.ack==1"
    try:                                                       # (un SYN-ACK vers l'API, trafic légitime F05, ne compte pas)
        r = subprocess.run(["tshark", "-r", fichier, "-Y", filtre],
                           capture_output=True, text=True, timeout=120)
    except (OSError, subprocess.TimeoutExpired):
        return None
    return bool(r.stdout.strip())

def main():
    ap = argparse.ArgumentParser(); ap.add_argument("dossiers", nargs="+")
    a = ap.parse_args()
    par_exec, globaux = [], []
    for d in a.dossiers:
        runs = pd.read_csv(f"{d}/runs.csv", parse_dates=["t0_utc", "t1_utc"])
        ev = pd.read_csv(f"{d}/events.csv", parse_dates=["ts_utc"], dtype={"sid": "string", "dst_port": "string"})
        sla = pd.read_csv(f"{d}/sla.csv", parse_dates=["ts_utc"])
        alertes = ev[ev.source == "suricata"]
        en_fenetre = pd.Series(False, index=alertes.index)
        sla_attaque = pd.Series(False, index=sla.index)
        for _, r in runs.iterrows():
            debut, fin = r.t0_utc, r.t1_utc + DELTA
            f = alertes[(alertes.ts_utc >= debut) & (alertes.ts_utc <= fin)]
            en_fenetre |= alertes.index.isin(f.index)
            sla_attaque |= (sla.ts_utc >= debut) & (sla.ts_utc <= fin)
            garde = (f.src_ip == r.src_ip) | (f.dst_ip == r.src_ip)        # E08 : l'attaquant, pas la cible
            for s, c in RELAIS.get(r.scenario, []):
                garde |= (f.src_ip == s) & (f.dst_ip == c)
            A = f[garde]
            chemin = f"{d}/sorties/{r.run_id}.txt"
            sortie = open(chemin, encoding="utf-8", errors="replace").read() if os.path.exists(chemin) else ""
            sla_f = sla[(sla.ts_utc >= debut) & (sla.ts_utc <= fin) & (sla.type == "https")]
            atteint = objectif_atteint(r.scenario, sortie, sla_f)
            pcap = f"{d}/pcap/{r.run_id}_postgres.pcap"
            if r.scenario in ("S-04", "S-05") and os.path.exists(pcap):
                vu = synack_dans_pcap(pcap, "10.10.0.70", r.src_ip)
                if vu: atteint = True                                    # la base a répondu : pas bloqué au réseau
            par_exec.append({"run_id": r.run_id, "scenario": r.scenario, "etat": r.etat, "detecte": len(A) > 0,
                             "ttd_ms": round((A.ts_utc.min() - r.t0_utc).total_seconds() * 1000) if len(A) else None,
                             "bloque": None if atteint is None else not atteint})
        hors = alertes[~en_fenetre]                                       # faux positifs : hors de toute fenêtre
        duree_h = (sla.ts_utc.max() - sla.ts_utc.min()).total_seconds() / 3600
        https = sla.type == "https"
        ok = sla[https & ~sla_attaque]                                    # référence : hors fenêtres d'attaque
        sous = sla[https & sla_attaque]
        globaux.append({"etat": runs.etat.iloc[0], "fp_par_heure": round(len(hors) / duree_h, 2) if duree_h else None,
                        "disponibilite_pct": round(100 * ok.succes.mean(), 2) if len(ok) else None,           # nominale
                        "disponibilite_sous_attaque_pct": round(100 * sous.succes.mean(), 2) if len(sous) else None,
                        "latence_mediane_ms": ok[ok.succes == 1].duree_ms.median(),
                        "latence_p95_ms": ok[ok.succes == 1].duree_ms.quantile(0.95, interpolation="higher")})
    os.makedirs("resultats/synthese", exist_ok=True)
    df = pd.DataFrame(par_exec)
    df.to_csv("resultats/synthese/par_execution.csv", index=False)
    ind = df.groupby(["scenario", "etat"]).agg(n=("run_id", "count"), taux_detection=("detecte", "mean"),
            taux_blocage=("bloque", lambda s: s.dropna().astype(float).mean() if s.notna().any() else None),
            ttd_median_ms=("ttd_ms", "median")).reset_index()
    ind.to_csv("resultats/synthese/indicateurs.csv", index=False)
    pd.DataFrame(globaux).to_csv("resultats/synthese/globaux.csv", index=False)
    print(ind.to_string(index=False)); print(pd.DataFrame(globaux).to_string(index=False))

if __name__ == "__main__":
    main()
```

**Tester**

**

```
python3 analyse/correle.py resultats/A/2026-11-20 resultats/B/2026-11-25
```
Sortie attendue :
```
scenario etat  n  taux_detection  taux_blocage  ttd_median_ms
    S-01    A 10             0.0           0.0            NaN
    S-01    B 10             1.0           0.9          412.0
    S-05    A 10             0.0           0.0            NaN
    S-05    B 10             1.0           1.0           18.0
…
etat  fp_par_heure  disponibilite_pct  disponibilite_sous_attaque_pct  latence_mediane_ms  latence_p95_ms
   A          0.00             100.00                           81.30                42.0            61.0
   B          0.40              99.90                           99.20                47.0            72.0
```
- `exemple de forme, pas de résultat` : les valeurs réelles viendront de vos campagnes
- `disponibilite_pct` : disponibilité nominale, hors fenêtres d'attaque : c'est la valeur à comparer à l'objectif ≥ 99 % ; la disponibilité sous attaque est présentée à part
- `ttd NaN en A` : aucune alerte en état A : pas de temps de détection
- `taux_blocage 0.9` : 9 exécutions sur 10 bloquées : à commenter (sondes passées avant le seuil)

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Toutes les détections à 0 en état B | Horloges décalées ou fenêtre fausse | Comparer t0 et les premières alertes à la main ; vérifier NTP (étape 1). |
| Détection de S-03 à 0 | Relais non pris en compte | Vérifier RELAIS et les adresses de la ligne EVE (10.20.0.10 → 10.20.0.70). |
| taux_blocage vide pour S-06 | Normal : pas d'objectif de blocage | Le présenter comme « détection seulement ». |

**C'est fini quand**

- [ ] par_execution.csv, indicateurs.csv, globaux.csv produits pour A et B
- [ ] Chaque valeur inattendue a été vérifiée à la main sur au moins une exécution

**À livrer.** Commit de `analyse/correle.py` et de `resultats/synthese/*.csv`. Message au rôle 3 : fp_par_sid.csv pour le réglage (le 24) ; au rôle 1 : indicateurs le 28.

### Étape 6 — Produire les indicateurs et les graphiques A vs B

**Quand :** 27–28 nov. · **Prérequis :** étapes analyse-5, attaques-6 ; modules 10, 12

**But.** Les tableaux et graphiques du rapport final, régénérables d'une commande.

**Pourquoi.** Section 10.2 : taux de détection, taux de blocage, temps de détection, faux positifs, disponibilité, latence introduite (sonde, iperf3 : A, B sans IPS, B avec IPS). EF7 : tableaux et graphiques A vs B. Jalon du 28 novembre.

**Configuration et commandes**

*Graphiques* — fichier `analyse/graphes.py`

```
#!/usr/bin/env python3
"""Graphiques A vs B (section 10.2) à partir de resultats/synthese/. Dépendance : apt install python3-matplotlib"""
import json, glob
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt
import pandas as pd

S = "resultats/synthese"
ind = pd.read_csv(f"{S}/indicateurs.csv")
glo = pd.read_csv(f"{S}/globaux.csv")
scen = ["S-01", "S-02", "S-03", "S-04", "S-05", "S-06"]

def barres(col, titre, fichier):
    fig, ax = plt.subplots(figsize=(8, 3.6))
    for k, (etat, coul) in enumerate([("A", "#9aa3a0"), ("B", "#0c6a66")]):
        v = [ind[(ind.scenario == s) & (ind.etat == etat)][col].mean() for s in scen]
        x = [i + (k - 0.5) * 0.38 for i in range(len(scen))]
        ax.bar(x, [0 if pd.isna(y) else y for y in v], width=0.36, color=coul, label=f"état {etat}")
        for xi, y in zip(x, v):
            ax.text(xi, (0 if pd.isna(y) else y) + 0.02, "n/a" if pd.isna(y) else f"{y:.0%}", ha="center", fontsize=8)
    ax.set_xticks(range(len(scen)), scen); ax.set_ylim(0, 1.12); ax.set_ylabel(titre)
    ax.set_title(f"{titre} par scénario (N = {int(ind.n.max())})"); ax.legend(frameon=False)
    fig.tight_layout(); fig.savefig(f"{S}/{fichier}", dpi=150)

barres("taux_detection", "Taux de détection", "detection.png")
barres("taux_blocage", "Taux de blocage", "blocage.png")

ex = pd.read_csv(f"{S}/par_execution.csv")
fig, ax = plt.subplots(figsize=(8, 3.4))
b = ex[(ex.etat == "B") & ex.ttd_ms.notna()]
ax.boxplot([b[b.scenario == s].ttd_ms for s in scen if len(b[b.scenario == s])], tick_labels=[s for s in scen if len(b[b.scenario == s])])
ax.set_ylabel("ms"); ax.set_title("Temps de détection en état B (première alerte − t0)")
fig.tight_layout(); fig.savefig(f"{S}/ttd.png", dpi=150)

debits = {}
for f in sorted(glob.glob("resultats/*/*/iperf/*.json")):
    debits[f.split("/")[-1].replace(".json", "")] = json.load(open(f))["end"]["sum_received"]["bits_per_second"] / 1e6
fig, ax = plt.subplots(1, 2, figsize=(9, 3.2))
ax[0].bar(glo.etat, glo.latence_mediane_ms, color="#0c6a66", label="médiane"); ax[0].bar(glo.etat, glo.latence_p95_ms - glo.latence_mediane_ms, bottom=glo.latence_mediane_ms, color="#7fb3b0", label="jusqu'au p95")
ax[0].set_title("Latence HTTPS de la sonde (ms)"); ax[0].legend(frameon=False)
ax[1].bar(list(debits), list(debits.values()), color="#0c6a66"); ax[1].set_title("Débit iperf3 (Mbit/s)")
fig.tight_layout(); fig.savefig(f"{S}/latence_debit.png", dpi=150)
print("graphiques écrits dans", S)
```

**Tester**

**

```
python3 analyse/graphes.py && ls resultats/synthese/*.png
```
Sortie attendue :
```
graphiques écrits dans resultats/synthese
resultats/synthese/blocage.png  resultats/synthese/detection.png
resultats/synthese/latence_debit.png  resultats/synthese/ttd.png
```
- `4 images` : chaque graphique répond à une question du sujet

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Un scénario manque dans un graphique | Aucune exécution pour cet état | Vérifier runs.csv ; ne jamais compléter à la main. |
| Les pourcentages ne correspondent pas au tableau | Graphique ancien | Toujours régénérer après correle.py. |
| Pas de débit iperf3 | Fichiers JSON absents | Rôle 5 : iperf/A.json dans le dossier de la campagne A, iperf/B-avec-ips.json et iperf/B-sans-ips.json dans celui de la campagne B. |

**C'est fini quand**

- [ ] Les 4 graphiques produits par script
- [ ] Chaque « attendu » de la section 10.1 a son verdict mesuré (confirmé / infirmé)
- [ ] Tableau final relu par un autre rôle

**À livrer.** Commit de `resultats/synthese/` et message au rôle 1 : « indicateurs et graphiques prêts pour le rapport ».

### Étape 7 — Commenter 4 à 6 captures Wireshark

**Quand :** 27–28 nov. · **Prérequis :** étapes analyse-4, attaques-6 ; modules 10, 1, 9

**But.** Des captures commentées qui montrent, paquet par paquet, la différence entre A et B.

**Pourquoi.** Section 7.10 : les PCAP fournissent les captures Wireshark commentées demandées par le sujet. Elles illustrent les figures 6 et 7 avec des paquets réels.

**Configuration et commandes**

*Captures à produire et filtres d'affichage*

```
1. S-05 état A — A-S05-01_postgres.pcap        filtre : tcp.port == 5432
   Montrer : SYN de 10.10.0.12, SYN-ACK de 10.10.0.70, échange PostgreSQL : la connexion aboutit sans pare-feu.
2. S-05 état B — B-S05-01_postgres.pcap        filtre : ip.addr == 10.10.0.12
   Montrer : aucun paquet de client-03 n'arrive ; mettre en regard la ligne filterlog et l'alerte 9000020.
3. S-01 état A — A-S01-01_kali-wan.pcap        filtre : tcp.flags.syn == 1
   Montrer : SYN-ACK pour 80 / 443 / 53, rien pour les autres ports (filtered).
4. S-01 état B — B-S01-01_kali-wan.pcap        Statistics › I/O Graphs (SYN envoyés, SYN-ACK reçus)
   Montrer : les réponses cessent après le seuil de 20 SYN en 5 s.
5. S-03 état A — A-S03-01_api.pcap             Analyze › Decode As… : TCP 5000 → HTTP, puis filtre http.request.uri contains "OR", puis Follow › HTTP Stream
   Montrer : l'URL d'injection en clair et la réponse contenant « secret ».
6. S-06 — A-S06-01_kali-wan.pcap               filtre : ip.flags.mf == 1 || ip.frag_offset > 0
   Montrer : les fragments de 8 octets produits par nmap -f.
```

*Comment annoter*

```
Wireshark › Edit › Packet Comment… sur les paquets clés, puis File › Save As… au format pcapng (les commentaires y sont gardés).
Pour le rapport : capture d'écran de la liste des paquets + 3 à 5 lignes d'explication sous l'image
(que montre-t-on, quel paquet regarder, quelle conclusion pour A ou B).
```

**Tester**

**

```
tshark -r captures/1-S05-A.pcapng -Y 'frame.comment' -T fields -e frame.number -e frame.comment
```
Sortie attendue :
```
1	SYN de client-03 vers PostgreSQL : aucun pare-feu sur le chemin
2	SYN-ACK : la base accepte la connexion
```
- `commentaires présents` : les annotations sont enregistrées dans le fichier remis

**Pannes connues**

| Symptôme | Cause | Solution |
|---|---|---|
| Les commentaires disparaissent | Enregistré au format pcap | Enregistrer en pcapng. |
| HTTP illisible | Capture prise sur le WAN (TLS) | Utiliser la capture côté API (saut nginx → API en clair). |

**C'est fini quand**

- [ ] 4 à 6 captures pcapng commentées
- [ ] Chaque capture a sa légende dans le rapport

**À livrer.** Dossier `rapport/captures/` (pcapng + images) et message au rôle 1.

### Questions probables du jury

1. **Comment calculez-vous le taux de détection ?**
   Pour chaque exécution, on cherche les alertes Suricata dans [t0, t1 + 10 s] qui concernent l'adresse de l'attaquant (ou un relais déclaré, comme nginx pour S-03). Détecté = au moins une alerte. Le taux est la moyenne sur les N exécutions d'un scénario et d'un état.

2. **Pourquoi ne pas filtrer sur la cible, comme dans le pseudo-code de la conception ?**
   Parce que la cible de S-02 et S-03 est nginx, que la sonde légitime interroge en permanence : une alerte causée par la sonde pendant la fenêtre aurait compté comme détection. Le filtre sur l'attaquant évite ce biais (écart E08).

3. **Comment jugez-vous qu'une attaque est bloquée ?**
   Sur l'effet réel : ports ouverts dans la sortie de nmap, ligne renvoyée par psql, « secret » dans la réponse, disponibilité de la sonde ; et pour S-04 et S-05, présence ou non d'un SYN-ACK de la base dans le PCAP côté cible.

4. **Qu'est-ce qu'un faux positif dans vos chiffres ?**
   Une alerte hors de toute fenêtre d'attaque, rapportée à la durée de trafic légitime. La mesure de 2 h de trafic légitime seul est la référence : pendant ce temps, toute alerte est un faux positif.

5. **Pourquoi la médiane et le 95e centile pour la latence ?**
   Avec quelques valeurs extrêmes, la moyenne est trompeuse. La médiane décrit l'expérience typique, le p95 les lenteurs occasionnelles ; on compare A, B sans IPS et B avec IPS pour isoler le coût de Suricata.

### Démonstration finale (2 minutes)

Ce que vous montrez en 2 minutes : chaque chiffre vient d'un script et se vérifie à la main.

1. Une ligne filterlog brute, puis la même ligne dans events.csv (UTC).
2. Le schéma fenêtre [t0, t1 + 10 s] et le calcul d'un TTD sur une exécution réelle.
3. Le tableau des indicateurs A vs B et un graphique.
4. Une capture Wireshark commentée (S-05 en A).

**Plan de secours :** images des graphiques et des captures dans le rapport.
