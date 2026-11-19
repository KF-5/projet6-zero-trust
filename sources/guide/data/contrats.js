/* Contrats de données : identiques pour tous les rôles. */
window.G = window.G || {};
G.CONTRATS = [
 {id:"flows", f:"politique/flows.csv", prod:"parefeu", cons:["parefeu (C2)","analyse (rapport)"],
  cols:["id","src","dst","proto","port","justification","etat"],
  regles:[
   "Une ligne = un flux autorisé et justifié (section 5.2). F99 et FA5 décrivent le refus par défaut.",
   "src et dst : un nom de segment (WAN, DMZ-WEB…), un nom d'hôte de hosts.csv, ou any. « opnsense » désigne la passerelle du segment source.",
   "proto : tcp, udp, tcp/udp ou any. port : un nombre, plusieurs séparés par « ; », ou any. dst peut aussi lister plusieurs hôtes séparés par « ; » (F07).",
   "etat : A ou B. Les lignes FA* sont de l'état A, les lignes F* de l'état B.",
   "Encodage UTF-8, séparateur virgule, pas de virgule dans la justification."],
  ex:`id,src,dst,proto,port,justification,etat
FA1,WAN,nginx,tcp,80;443,Site public (NAT depuis 203.0.113.1),A
FA2,WAN,bind9,tcp/udp,53,DNS public (NAT),A
FA3,DMZ,postgresql,tcp,5432,Toute la DMZ vers la base,A
FA4,LAN,any,any,any,LAN vers tout (règle par défaut),A
FA6,client-ext,nginx,tcp,5201,Mesure iperf3 (activée pendant les mesures),A
FA5,any,any,any,any,Refus par défaut entre zones (block + log),A
F01,WAN,nginx,tcp,80;443,Site public,B
F02,WAN,bind9,tcp/udp,53,Zone DNS publique,B
F03,nginx,api,tcp,5000,Reverse proxy vers l'API,B
F04,api,postgresql,tcp,5432,Seul accès autorisé à la base,B
F05,LAN-USERS,nginx,tcp,443,Application interne,B
F06,LAN-USERS,bind9,udp,53,Résolution de noms,B
F07,admin,nginx;bind9;api;postgresql,tcp,22,Administration SSH,B
F08,admin,opnsense,tcp,443,Console du pare-feu,B
F09,client-ext,nginx,tcp,5201,Mesure iperf3 (activée pendant les mesures),B
F99,any,any,any,any,Refus par défaut (block + log),B`},
 {id:"segments", f:"politique/segments.csv", prod:"parefeu", cons:["parefeu (C2)","services (réseaux Docker)"],
  cols:["nom","etat","reseau","vlan","passerelle"],
  regles:["Ajouté au contrat parce que C2 le lit (figure 11, écart E22).","MGMT est présent dans les deux états mais exclu de l'évaluation."],
  ex:`nom,etat,reseau,vlan,passerelle
WAN,A,203.0.113.0/24,,203.0.113.1
DMZ,A,10.20.0.0/24,20,10.20.0.1
LAN,A,10.10.0.0/24,10,10.10.0.1
WAN,B,203.0.113.0/24,,203.0.113.1
DMZ-WEB,B,10.20.0.0/27,21,10.20.0.1
DMZ-DNS,B,10.20.0.32/27,22,10.20.0.33
DMZ-APP,B,10.20.0.64/27,23,10.20.0.65
LAN-USERS,B,10.10.0.0/26,11,10.10.0.1
LAN-DB,B,10.10.0.64/27,12,10.10.0.65
LAN-ADMIN,B,10.10.0.96/27,13,10.10.0.97
MGMT,AB,10.99.0.0/24,,10.99.0.1`},
 {id:"hosts", f:"politique/hosts.csv", prod:"parefeu", cons:["parefeu (C2, test_flux)","attaques","analyse"],
  cols:["hote","ip","segment_A","segment_B","conteneur"],
  regles:["Ajouté au contrat parce que C2 le lit (figure 11).","conteneur : nom du conteneur Docker (pour nsenter et docker exec) ; « - » pour une VM.","Les adresses sont identiques en A et en B (décision 1 du rapport)."],
  ex:`hote,ip,segment_A,segment_B,conteneur
client-ext,203.0.113.10,WAN,WAN,client-ext
kali,203.0.113.66,WAN,WAN,-
nginx,10.20.0.10,DMZ,DMZ-WEB,nginx
dmz-pivot,10.20.0.20,DMZ,DMZ-WEB,dmz-pivot
bind9,10.20.0.40,DMZ,DMZ-DNS,bind9
api,10.20.0.70,DMZ,DMZ-APP,api
client-01,10.10.0.10,LAN,LAN-USERS,client-01
client-03,10.10.0.12,LAN,LAN-USERS,client-03
postgresql,10.10.0.70,LAN,LAN-DB,postgres
admin,10.10.0.100,LAN,LAN-ADMIN,admin`},
 {id:"tests", f:"politique/tests.csv", prod:"parefeu (C2)", cons:["parefeu (test_flux)","attaques (vérification avant campagne)"],
  cols:["id_test","src_host","dst_ip","proto","port","attendu","flux_ref"],
  regles:["Produit par C2 (section 7.2) : un test « pass » par flux, un « mauvais port » par flux, un « block » par couple de segments sans flux.","dst_ip est l'adresse que la source vise réellement : depuis le WAN, 203.0.113.1 (avant NAT).","attendu : pass ou block. flux_ref : le flux testé, ou F99/FA5 pour un refus par défaut."],
  ex:`id_test,src_host,dst_ip,proto,port,attendu,flux_ref
T001,client-ext,203.0.113.1,tcp,443,pass,F01
T002,client-ext,203.0.113.1,tcp,22,block,F01
T003,nginx,10.20.0.70,tcp,5000,pass,F03
T004,api,10.10.0.70,tcp,5432,pass,F04
T005,api,10.10.0.70,tcp,22,block,F04
T006,client-01,10.20.0.40,udp,53,pass,F06
T007,client-03,10.10.0.70,tcp,5432,block,F99
T008,dmz-pivot,10.10.0.70,tcp,5432,block,F99`},
 {id:"scenarios", f:"campagne/scenarios.yaml", prod:"attaques", cons:["attaques (C6)","analyse (critère objectif atteint)"],
  cols:["id","description","machine","commande","cible","duree_max_s","objectif_atteint","captures","actions (P-01)"],
  regles:["machine : kali (SSH sur la VM) ou un nom de conteneur (docker exec sur l'hôte Docker).","commande : une ligne, ou un bloc « | » de plusieurs lignes exécutées à la suite (indispensable dès qu'il y a « : » ou des guillemets).","actions : seulement pour P-01 ; commandes exécutées sur OPNsense à a_s secondes après t0.","cible : l'adresse visée par la commande (avant NAT pour les scénarios WAN).","objectif_atteint : la règle qui permet de juger « bloqué » sur l'effet réel (section 9.4).","captures : points de capture C10 démarrés par l'orchestrateur."],
  ex:`graine: 2026          # même graine en A et en B (section 10.3)
n: 10
pause_s: 30
scenarios:
  - id: S-01
    description: Scan SYN depuis le WAN
    machine: kali
    commande: nmap -sS -Pn -p 1-1024 -T4 -oG - 203.0.113.1
    cible: 203.0.113.1
    duree_max_s: 240
    objectif_atteint: "au moins une ligne '/open/' dans la sortie"
    captures: [kali-wan, nginx]
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
  - id: P-01
    …
    actions:
      - {a_s: 10, commande: "configctl ids stop"}
      - {a_s: 40, commande: "configctl ids start"}`},
 {id:"runs", f:"resultats/<etat>/<date>/runs.csv", prod:"attaques (C6)", cons:["analyse (C11)"],
  cols:["run_id","scenario","etat","iteration","t0_utc","t1_utc","src_ip","cible_ip","code_retour"],
  regles:["run_id = <etat>-<scénario sans tiret>-<itération sur 2 chiffres> : A-S01-03.","Horodatages ISO 8601 en UTC à la milliseconde, horloge du collecteur (NTP).","cible_ip : l'adresse de scenarios.yaml. C11 retrouve l'adresse interne avec sa table NAT (écart E09)."],
  ex:`run_id,scenario,etat,iteration,t0_utc,t1_utc,src_ip,cible_ip,code_retour
A-S01-01,S-01,A,1,2026-11-20T09:02:10.412Z,2026-11-20T09:02:31.880Z,203.0.113.66,203.0.113.1,0
A-S05-01,S-05,A,1,2026-11-20T09:03:02.105Z,2026-11-20T09:03:09.731Z,10.10.0.12,10.10.0.70,0
B-S05-01,S-05,B,1,2026-11-25T09:03:02.230Z,2026-11-25T09:03:19.002Z,10.10.0.12,10.10.0.70,2`},
 {id:"events", f:"resultats/<etat>/<date>/events.csv", prod:"analyse (C9)", cons:["analyse (C11)","ids (faux positifs)"],
  cols:["ts_utc","source","src_ip","dst_ip","dst_port","proto","action","sid","message"],
  regles:["source : pf, suricata, nginx.","action : pass ou block (pf) ; alert ou drop (Suricata) ; code HTTP (nginx).","sid vide hors Suricata. message : signature Suricata, règle pf (tracker) ou requête HTTP."],
  ex:`ts_utc,source,src_ip,dst_ip,dst_port,proto,action,sid,message
2026-11-25T09:03:02.418Z,suricata,10.10.0.12,10.10.0.70,5432,tcp,alert,9000020,LOCAL LAN-USERS vers PostgreSQL
2026-11-25T09:03:02.420Z,pf,10.10.0.12,10.10.0.70,5432,tcp,block,,F99 (défaut)
2026-11-20T09:10:44.000Z,nginx,203.0.113.66,10.20.0.10,443,tcp,200,,GET /api/produit?id=1%27%20OR%20%271%27%3D%271`},
 {id:"sla", f:"resultats/<etat>/<date>/sla.csv", prod:"ids (C8)", cons:["analyse (C11)"],
  cols:["ts_utc","client","cible","type","succes","duree_ms"],
  regles:["Une ligne par requête de la sonde : HTTPS toutes les secondes (client-ext et client-01), DNS toutes les 5 s (section 7.8).","succes : 1 ou 0. duree_ms vide si échec."],
  ex:`ts_utc,client,cible,type,succes,duree_ms
2026-11-20T09:02:11.003Z,client-ext,https://203.0.113.1/api/produits,https,1,48
2026-11-20T09:02:11.010Z,client-01,https://10.20.0.10/api/produits,https,1,41
2026-11-20T09:02:15.002Z,client-01,10.20.0.40,dns,1,3
2026-11-20T09:02:12.004Z,client-ext,https://203.0.113.1/api/produits,https,0,`}
];
G.ARBO = `resultats/
├── A/
│   └── 2026-11-20/
│       ├── runs.csv            (C6)
│       ├── events.csv          (C9)
│       ├── sla.csv             (C8)
│       ├── sorties/A-S01-01.txt        sortie brute de l'outil (C6)
│       ├── pcap/A-S01-01_kali-wan.pcap capture C10 : <run_id>_<point>.pcap
│       ├── pcap/A-S05-01_postgres.pcap
│       ├── iperf/A.json                débit iperf3 (C8)
│       └── VERSION.txt                 commit Git, hash du jeu de règles, versions
├── B/
│   └── 2026-11-25/  (même contenu) + fp_par_sid.csv + iperf/B-sans-ips.json + iperf/B-avec-ips.json
└── synthese/        indicateurs.csv, graphiques PNG (C11)`;
G.POINTS = [["kali-wan","eth0 de Kali (vue WAN de l'attaquant, écart E18)"],["nginx","netns du conteneur nginx"],["api","netns du conteneur API"],["postgres","netns du conteneur PostgreSQL"],["bind9","netns du conteneur bind9"]];
