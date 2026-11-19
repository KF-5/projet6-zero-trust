/* Données de l'accueil : rôles, planning, jalons, interfaces entre rôles, liens du cours. */
window.G = window.G || {};

/* Cours : un artefact par module */
G.MOD = {0:"TxLP9U5WScGg3NjtWFEmd5",1:"6kh2gBrsTQtCsncSzzJs2y",2:"7UXfzJQcveDAsQnMTYyBzY",3:"Nj6R8RkwW2KE8wW4kEJYCd",4:"WDQrUeH9cvNpw9eSr7WJTL",5:"QnjHB3vAoMeGB9BsVHG1oG",6:"7c4hL1AmVtMdN44Nn7xE4V",7:"LxLJrWhD1EnW5wVa9D2qhZ",8:"NG8SQ2WSRj2bakstrpK6oG",9:"V9rbjBWxvbN258tuubapX5",10:"KRdXhSK9fQV3VQsrS1gWX8",11:"EEMFFdt7HCAuFrjGipcSxk",12:"Mj1FvAws7qig3dKbzzPMSe"};
G.MODT = {0:"Carte du parcours",1:"Les bases",2:"Adressage",3:"Commutation et VLAN",4:"Routage, NAT et DMZ",5:"Pare-feu stateful",6:"Zero Trust",7:"IDS et IPS",8:"Virtualisation",9:"Attaques côté défenseur",10:"Observabilité",11:"Mise en pratique",12:"Soutenance"};

/* Versions réellement installées (mise à jour du 17 novembre 2026 ; écarts dans ECARTS.md) */
G.VERSIONS = {pve:"VMware Workstation Pro 26 (hôte Windows)", opn:"OPNsense 26.7", suri:"Suricata 8.0 (intégré à OPNsense 26.7)", deb:"Debian 13 « trixie »", dock:"Docker 26.1.5 + Compose 2.26.1 (paquets Debian)", kali:"Kali Linux 2026.3", py:"Python 3.13 (Debian 13)"};

/* Les 6 rôles. Charge estimée en heures de travail effectif. */
G.ROLES = [
 {id:"chef", n:1, c:"--r1", titre:"Chef de projet · plateforme", court:"Plateforme",
  mission:"Monter l'hôte VMware et ses 4 VM, verrouiller le réseau MGMT, ouvrir l'accès à distance de l'équipe (Tailscale, tunnels SSH, panneau, consoles VNC), tenir le dépôt Git et les états figés, puis intégrer le travail de tous jusqu'au rapport et à la démonstration.",
  comp:["Hôte VMware (Windows)","p6-wan, p6-trunk, VMnet9","4 VM","MGMT filtré","accès à distance","panneau + VNC","Git","instantanés etat-A / etat-B","rapport final","démonstration"],
  charge:30, modules:[8,3,0,11,12], versions:["pve","deb","kali"]},
 {id:"parefeu", n:2, c:"--r2", titre:"Pare-feu et segmentation", court:"Pare-feu",
  mission:"Configurer OPNsense (interfaces, VLAN, NAT, règles FA puis F01–F99, journalisation) et écrire le moteur de politique qui transforme flows.csv en règles et en tests vérifiés par test_flux.",
  comp:["C1 pf","C2 moteur de politique","test_flux","VLAN 10/20 puis 11–13/21–23","NAT","filterlog"],
  charge:30, modules:[2,3,4,5,6], versions:["opn","py"]},
 {id:"ids", n:3, c:"--r3", titre:"IDS/IPS et trafic légitime", court:"IDS/IPS",
  mission:"Valider netmap sur les cartes VMware d'OPNsense (em), mettre Suricata en ligne sur em0 (WAN) et le trunk (em1), écrire les règles locales, produire le trafic légitime et la sonde SLA qui mesurent faux positifs et disponibilité, puis passer en IPS et préparer la panne P-01.",
  comp:["C3 Suricata","C4 règles (ET Open + 9000001–9000099)","C8 trafic légitime + sonde SLA","réglage IDS → IPS","P-01"],
  charge:30, modules:[7,1,9,10], versions:["opn","suri","py"]},
 {id:"services", n:4, c:"--r4", titre:"Services applicatifs", court:"Services",
  mission:"Installer Docker et les réseaux macvlan par VLAN, déployer nginx, l'API Flask, PostgreSQL, bind9 et tous les conteneurs clients, fournir l'outil de capture par conteneur, puis basculer en état B.",
  comp:["C5 nginx, API, PostgreSQL, bind9","conteneurs clients, dmz-pivot, client-ext","serveur iperf3","outil de capture C10","macvlan A puis B"],
  charge:28, modules:[8,3,2,4], versions:["deb","dock"]},
 {id:"attaques", n:5, c:"--r5", titre:"Validation et attaques", court:"Attaques",
  mission:"Préparer Kali, écrire scenarios.yaml et l'orchestrateur qui rejoue chaque scénario N = 10 fois avec la vérité terrain, puis conduire les campagnes A et B, panne P-01 comprise.",
  comp:["C6 orchestrateur","C7 attaques","scenarios.yaml","runs.csv","campagnes A et B","exécution de P-01"],
  charge:29, modules:[9,1,10,11], versions:["kali","py"]},
 {id:"analyse", n:6, c:"--r6", titre:"Observabilité et analyse", court:"Analyse",
  mission:"Synchroniser les horloges, collecter et normaliser tous les journaux dans events.csv, corréler avec la vérité terrain, calculer les indicateurs, tracer les graphiques A vs B et commenter les captures.",
  comp:["NTP du laboratoire","C9 rsyslog + parseurs","C11 corrélation et indicateurs","graphiques A vs B","captures Wireshark commentées"],
  charge:31, modules:[10,9,7,6], versions:["deb","py"]}
];

/* Justification de la répartition (affichée sur l'accueil) */
G.REPARTITION = [
 ["C8 (trafic légitime et sonde) passe du rôle Services au rôle IDS/IPS.","Le rôle IDS est le premier utilisateur de la sonde : faux positifs sur 2 h de trafic légitime (section 9.3) et disponibilité pendant P-01. Il est libre du 16 au 21, alors que le rôle Services est sur le chemin critique du 17 au 19."],
 ["NTP passe du rôle Plateforme au rôle Analyse.","Le serveur NTP tourne sur le collecteur, comme C9 et C11. La justesse du temps de détection est la responsabilité de celui qui le calcule."],
 ["L'outil de capture C10 est écrit par le rôle Services ; l'analyse des PCAP reste au rôle Analyse.","La capture s'exécute dans l'espace réseau des conteneurs, sur l'hôte Docker que le rôle Services administre. Le rôle Analyse, le plus chargé en fin de projet, se concentre sur l'exploitation."],
 ["Chaque rôle rédige sa section du rapport ; le rôle Plateforme assemble.","Personne ne connaît mieux une partie que celui qui l'a construite, et cela retire au chef de projet la rédaction intégrale."],
 ["P-01 : le rôle IDS prépare la panne, le rôle Attaques l'exécute pendant la campagne B.","P-01 est une exécution comme les autres pour l'orchestrateur (section 10.1), mais son mécanisme (arrêt et redémarrage de Suricata) relève du rôle IDS."]
];

/* Planning commun (jours de novembre). crit = sur le chemin critique. dep = tâches dont celle-ci dépend. */
G.TASKS = [
 {id:"t11", r:"chef", t:"VMware, réseaux, 4 VM (fait)", d1:13, d2:14, crit:true, step:"chef-1"},
 {id:"t12", r:"chef", t:"Systèmes, MGMT verrouillé, accès équipe (fait), Git", d1:15, d2:16, dep:["t11"], step:"chef-4"},
 {id:"t13", r:"chef", t:"etat-A figé", d1:21, d2:21, dep:["t52"], step:"chef-7"},
 {id:"t14", r:"chef", t:"etat-B figé", d1:26, d2:26, dep:["t53"], step:"chef-7"},
 {id:"t15", r:"chef", t:"Rapport, démo, vidéo", d1:28, d2:30, crit:true, dep:["t65"], step:"chef-10"},
 {id:"t21", r:"parefeu", t:"OPNsense : WAN, MGMT", d1:14, d2:15, crit:true, dep:["t11"], step:"parefeu-1"},
 {id:"t22", r:"parefeu", t:"VLAN 10/20, NAT, FA", d1:16, d2:17, crit:true, dep:["t21"], step:"parefeu-3"},
 {id:"t23", r:"parefeu", t:"C2 + test_flux A", d1:17, d2:19, crit:true, dep:["t22","t42"], step:"parefeu-6"},
 {id:"t24", r:"parefeu", t:"État B : F01–F99", d1:22, d2:23, crit:true, dep:["t52"], step:"parefeu-8"},
 {id:"t25", r:"parefeu", t:"Section du rapport", d1:27, d2:28},
 {id:"t31", r:"ids", t:"Test netmap (J1)", d1:14, d2:15, dep:["t21"], step:"ids-1"},
 {id:"t32", r:"ids", t:"C8 sonde + iperf3", d1:16, d2:19, dep:["t41"], step:"ids-3"},
 {id:"t33", r:"ids", t:"IDS : règles, validation", d1:23, d2:23, crit:true, dep:["t24"], step:"ids-5"},
 {id:"t34", r:"ids", t:"2 h de réglage, IPS", d1:24, d2:24, crit:true, dep:["t33"], step:"ids-6"},
 {id:"t35", r:"ids", t:"Section du rapport", d1:27, d2:28},
 {id:"t41", r:"services", t:"Docker, macvlan A", d1:14, d2:16, dep:["t11"], step:"services-1"},
 {id:"t42", r:"services", t:"Services de l'état A", d1:17, d2:18, crit:true, dep:["t41","t22"], step:"services-3"},
 {id:"t43", r:"services", t:"Outil de capture C10", d1:18, d2:19, dep:["t42"], step:"services-7"},
 {id:"t44", r:"services", t:"Réseaux et services B", d1:22, d2:23, crit:true, dep:["t52"], step:"services-8"},
 {id:"t45", r:"services", t:"Section du rapport", d1:27, d2:28},
 {id:"t51", r:"attaques", t:"Kali, scénarios, orchestrateur", d1:14, d2:18, dep:["t11"], step:"attaques-1"},
 {id:"t52", r:"attaques", t:"Répétition puis campagne A", d1:19, d2:21, crit:true, dep:["t23","t42","t32","t62","t43"], step:"attaques-5"},
 {id:"t53", r:"attaques", t:"Campagne B + P-01", d1:25, d2:26, crit:true, dep:["t34","t44"], step:"attaques-6"},
 {id:"t54", r:"attaques", t:"Section du rapport", d1:27, d2:28},
 {id:"t61", r:"analyse", t:"NTP, rsyslog", d1:14, d2:15, dep:["t11"], step:"analyse-1"},
 {id:"t62", r:"analyse", t:"Parseurs → events.csv", d1:16, d2:19, dep:["t61"], step:"analyse-3"},
 {id:"t63", r:"analyse", t:"Corrélation sur A", d1:22, d2:24, dep:["t52"], step:"analyse-5"},
 {id:"t65", r:"analyse", t:"Indicateurs A vs B, graphiques", d1:27, d2:28, crit:true, dep:["t53","t63"], step:"analyse-6"},
 {id:"t64", r:"analyse", t:"Captures commentées", d1:27, d2:28, dep:["t53"], step:"analyse-7"}
];
G.MILESTONES = [
 {d:15, t:"Décision IPS en ligne ou repli"},
 {d:16, t:"Remise de la conception"},
 {d:19, t:"test_flux conforme ; S-01 → runs.csv + events.csv"},
 {d:21, t:"Données A archivées, tag etat-A"},
 {d:24, t:"test_flux B conforme ; ≤ 1 FP/h"},
 {d:26, t:"Données B archivées, tag etat-B"},
 {d:28, t:"Résultats complets"},
 {d:29, t:"Version finale relue"},
 {d:30, t:"Remise totale"}
];

/* Interfaces entre rôles : qui livre quoi, à qui, sous quel format, pour quand */
G.INTERFACES = [
 ["chef","tous","4 VM démarrées, accès SSH par MGMT, table des noms d'interfaces","message + docs/interfaces.md","14 nov."],
 ["parefeu","services","VLAN 10 et 20 actifs, passerelles 10.10.0.1 et 10.20.0.1","message (ping de la passerelle depuis un conteneur de test)","16 nov."],
 ["parefeu","ids","OPNsense joignable, WAN et TRUNK assignés, offloading désactivé","message + config.xml commité","15 nov."],
 ["services","parefeu","conteneurs à leurs adresses + accès nsenter pour test_flux","docker-compose.yml commité","18 nov."],
 ["services","attaques","exécution dans client-03 et dmz-pivot (docker exec) par SSH","docs/acces-attaquants.md","18 nov."],
 ["services","attaques, analyse","outil de capture C10 : capture.sh start|stop <run_id> <point>","capture/capture.sh","19 nov."],
 ["ids","attaques","sonde C8 pilotable : sonde.sh start|stop","sonde/ + sla.csv","19 nov."],
 ["analyse","attaques","collecte prête : events.csv produit à partir de syslog","analyse/parse.py","19 nov."],
 ["parefeu","attaques","tests.csv + test_flux 100 % conforme (état A)","politique/tests.csv + rapport de conformité","19 nov."],
 ["attaques","analyse","runs.csv, sorties des outils, PCAP de la campagne A","resultats/A/<date>/","21 nov."],
 ["parefeu","chef","config.xml de l'état A, puis de l'état B","opnsense/config-etat-A.xml, -B.xml","19 nov. / 24 nov."],
 ["analyse","ids","events.csv des 2 h de trafic légitime (parse.py) ; fp_par_sid.csv calculé ensemble avec fp.py","resultats/B/reglage/fp_par_sid.csv","24 nov."],
 ["ids","attaques","jeu de règles figé + procédure P-01 (arrêt, redémarrage)","ids/local.rules, ids/threshold.config, docs/p01.md","24 nov."],
 ["attaques","analyse","runs.csv, sorties, PCAP de la campagne B et de P-01","resultats/B/<date>/","26 nov."],
 ["analyse","chef","indicateurs, tableaux et graphiques A vs B","resultats/synthese/","28 nov."],
 ["tous","chef","section du rapport final de chaque rôle","rapport/<role>.md","28 nov."]
];

/* Responsable de chaque élément de l'architecture (carte de l'accueil) */
G.OWNERS = {
 wan:["chef"], kali:["attaques"], ce:["services","ids"], opn:["parefeu"], pf:["parefeu"], nat:["parefeu"], surW:["ids"], surT:["ids"], trunk:["chef","parefeu"],
 dmz:["parefeu"], lan:["parefeu"], nginx:["services"], pivot:["services","attaques"], bind9:["services"], api:["services"], c01:["services","ids"], c03:["services","attaques"], pg:["services"], admin:["services"],
 mgmt:["chef"], coll:["analyse","parefeu","attaques"], dock:["services"]
};

/* Plateforme réellement construite (mise à jour du 17 novembre 2026) */
G.REEL = `<p><b>Plateforme réelle (mise à jour du 17 novembre).</b> Le laboratoire tourne sur <b>VMware Workstation Pro</b>, installé sur le PC Windows du rôle 1, et non sur Proxmox : l'installateur de Proxmox efface le disque, ce que le PC ne permettait pas (écart E31). L'architecture, les adresses, les VLAN et les flux du rapport sont inchangés ; seuls les noms des réseaux, des cartes et la façon d'accéder aux machines changent. Les étapes déjà réalisées portent la pastille <span class="depchip fait">déjà fait</span>, et chaque étape concernée commence par un encadré vert.</p>
<table class="reel"><tr><th>Dans le guide (Proxmox)</th><th>En réalité (VMware)</th></tr>
<tr><td>Pont <code>vmbr1</code> (WAN)</td><td>Segment LAN <code>p6-wan</code>, aucun lien physique</td></tr>
<tr><td>Pont <code>vmbr2</code> (trunk VLAN-aware)</td><td>Segment LAN <code>p6-trunk</code> (les étiquettes 802.1Q passent telles quelles)</td></tr>
<tr><td>Pont <code>vmbr9</code> (MGMT)</td><td><code>VMnet9</code> « hôte uniquement », sans DHCP ; Windows y a l'adresse <b>10.99.0.2</b></td></tr>
<tr><td>Cartes <code>net9</code> sur <code>vmbr0</code> (fenêtre de configuration)</td><td>Une carte <b>NAT VMware</b> par VM, utilisée seulement pour les installations, <b>débranchée</b> et « non connectée au démarrage »</td></tr>
<tr><td>OPNsense <code>vtnet0 / vtnet1 / vtnet2</code></td><td><code>em0</code> WAN 203.0.113.1 · <code>em1</code> trunk · <code>em2</code> MGMT 10.99.0.1 · <code>em3</code> NAT débranchée</td></tr>
<tr><td>Hôte Docker <code>ens18 / ens19 / ens20</code></td><td><code>ens32</code> MGMT 10.99.0.20 · <code>ens33</code> trunk · <code>ens34</code> WAN · <code>ens35</code> NAT débranchée</td></tr>
<tr><td>Réseau CONF et interface web depuis vos PC</td><td>Pas de réseau CONF : interface web d'OPNsense sur <b>https://10.99.0.1</b>, depuis Windows ou par tunnel SSH</td></tr>
<tr><td>Console noVNC de Proxmox</td><td><b>Consoles VNC</b> par VM (ports 5901 à 5904), uniquement à travers le tunnel</td></tr>
<tr><td><code>qm start / stop / snapshot / rollback</code></td><td><b>Panneau de contrôle</b> (http://127.0.0.1:8080 par le tunnel) ou <code>vmrun</code> sur l'hôte</td></tr>
<tr><td>Identifiants VM 100 à 103</td><td><code>p6-opnsense</code>, <code>p6-docker</code>, <code>p6-kali</code>, <code>p6-collecteur</code></td></tr>
<tr><td>Docker Engine 29, réseaux <code>lan / dmz / wan</code></td><td>Docker 26.1.5 (paquets Debian), réseaux <code>p6_lan / p6_dmz / p6_wan</code> du projet Compose <code>p6</code></td></tr></table>
<p><b>Se connecter</b> (chacun avec <b>son</b> compte Tailscale et <b>sa</b> clé SSH, voir le message du rôle 1) :</p>
<pre class="code"><code>ssh -i $HOME\\.ssh\\p6labo -N -L 8443:10.99.0.1:443 -L 8080:127.0.0.1:8080 -L 5901:127.0.0.1:5901 -L 5902:127.0.0.1:5902 -L 5903:127.0.0.1:5903 -L 5904:127.0.0.1:5904 p6labo@100.125.208.28</code></pre>
<p>Puis : OPNsense sur <code>https://127.0.0.1:8443</code>, panneau sur <code>http://127.0.0.1:8080</code>, consoles avec TightVNC Viewer sur <code>127.0.0.1::5901</code> (opnsense), <code>::5902</code> (docker), <code>::5903</code> (collecteur), <code>::5904</code> (kali). La fenêtre du tunnel doit rester ouverte.</p>
<p><b>État au 17 novembre :</b> l'état A est construit et prouvé. <code>tests.sh</code> (hôte Docker) = 12/12, <code>tests-cloisonnement.sh</code> (Kali) = 19/19. Points de retour : <code>07-vnc</code> (OPNsense) et <code>vnc-pret</code> (les 3 autres VM).</p>`;
