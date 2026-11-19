# Le rôle de chacun

Les membres sont désignés par leur numéro de rôle. Charge estimée en heures de travail effectif.

| Rôle | Intitulé | Mission | Composants | Charge |
|---|---|---|---|---|
| 1 | Chef de projet · plateforme (Karim) | Monter l'hôte VMware et ses 4 VM, verrouiller MGMT, ouvrir l'accès à distance de l'équipe, tenir le dépôt Git et les états figés, intégrer le travail de tous jusqu'au rapport et à la démonstration | hôte VMware, réseaux, 4 VM, MGMT filtré, accès à distance, panneau + VNC, Git, instantanés etat-A / etat-B, rapport final, démonstration | 30 h |
| 2 | Pare-feu et segmentation | Configurer OPNsense (interfaces, VLAN, NAT, règles FA puis F01–F99, journalisation) et écrire le moteur de politique qui transforme flows.csv en règles et en tests vérifiés par test_flux | C1 pf, C2 moteur de politique, test_flux | 30 h |
| 3 | IDS/IPS et trafic légitime | Valider netmap sur les cartes em d'OPNsense, mettre Suricata en ligne, écrire les règles locales, produire le trafic légitime et la sonde SLA, passer en IPS, préparer la panne P-01 | C3 Suricata, C4 règles, C8 sonde | 30 h |
| 4 | Services applicatifs | Docker et macvlan par VLAN, nginx, API Flask, PostgreSQL, bind9, conteneurs clients, outil de capture, bascule en état B | C5 services, C10 capture | 28 h |
| 5 | Validation et attaques | Préparer Kali, écrire scenarios.yaml et l'orchestrateur, conduire les campagnes A et B, panne P-01 comprise | C6 orchestrateur, C7 attaques, runs.csv | 29 h |
| 6 | Observabilité et analyse | Horloges, collecte et normalisation des journaux (events.csv), corrélation, indicateurs, graphiques A vs B, captures commentées | NTP, C9, C11 | 31 h |

Déjà réalisé par le rôle 1 pendant la préparation de l'hôte (14–17 novembre) : étapes 1 à 5 du rôle 2, 1 à 6 du rôle 4, NTP et syslog du rôle 6, et Kali en grande partie pour le rôle 5. Chaque rôle vérifie ces étapes avant d'enchaîner (voir le guide).
