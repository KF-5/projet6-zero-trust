# Réseaux, VM et cartes réseau (plateforme réelle, VMware)

## Réseaux virtuels

| Réseau | Type VMware | Rôle | Plage |
|---|---|---|---|
| p6-wan | segment LAN | WAN simulé, aucun lien physique | 203.0.113.0/24 |
| p6-trunk | segment LAN | trunk 802.1Q (VLAN 10 LAN, 20 DMZ ; 11–13 et 21–23 en état B) | — |
| VMnet9 | hôte uniquement, sans DHCP | MGMT ; Windows = 10.99.0.2 | 10.99.0.0/24 |
| VMnet8 | NAT | Internet, pendant les installations seulement | — |

Jamais de mode Pont (VMnet0).

## Machines virtuelles

| VM | vCPU | RAM | Disque | Cartes (dans l'ordre) | MGMT |
|---|---|---|---|---|---|
| p6-opnsense | 2 | 3 Go | 20 Go | em0 p6-wan (203.0.113.1) · em1 p6-trunk · em2 VMnet9 · em3 NAT (débranchée) | 10.99.0.1 |
| p6-docker | 2 | 2,5 Go | 30 Go | ens32 VMnet9 · ens33 p6-trunk · ens34 p6-wan · ens35 NAT (débranchée) | 10.99.0.20 |
| p6-collecteur | 1 | 1 Go | 20 Go | ens32 VMnet9 · ens33 NAT (débranchée) | 10.99.0.10 |
| p6-kali | 1 | 1,5 Go | 25 Go | eth0 p6-wan (203.0.113.66, passerelle 203.0.113.1) · eth1 VMnet9 · eth2 NAT (débranchée) | 10.99.0.66 |

Chaque carte a été vérifiée par son adresse MAC. Les cartes NAT ont `startConnected = "FALSE"`.

## Correspondance avec le rapport de conception (écarts E15, E39)

| Rapport (Proxmox) | Réalité (VMware) |
|---|---|
| vmbr1, vmbr2, vmbr9 | p6-wan, p6-trunk, VMnet9 |
| OPNsense vtnet0 / vtnet1 / vtnet2 | em0 / em1 / em2 |
| Hôte Docker eth0 / eth1 / eth2 | ens32 / ens33 / ens34 |
| Réseaux Docker lan / dmz / wan | p6_lan (ens33.10) / p6_dmz (ens33.20) / p6_wan (ens34) |

## Interfaces d'OPNsense

| Interface | Appareil | VLAN | Adresse |
|---|---|---|---|
| WAN | em0 | — | 203.0.113.1/24 |
| MGMT | em2 | — | 10.99.0.1/24 |
| LAN | vlan01 sur em1 | 10 | 10.10.0.1/24 |
| DMZ | vlan02 sur em1 | 20 | 10.20.0.1/24 |
