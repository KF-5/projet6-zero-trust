/* Rôle 3 : IDS/IPS (C3, C4) et trafic légitime (C8). */
window.G = window.G || {}; G.roles = G.roles || {};
(function(){
const RULES = `# ids/local.rules — règles locales du projet (sid 9000001 à 9000099, section 7.4). Syntaxe Suricata 8.
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
alert http any any -> 10.20.0.70 5000 (msg:"LOCAL S-03 injection SQL sur /api/"; flow:established,to_server; http.uri; content:"/api/"; startswith; pcre:"/'(\\s|\\+)*or(\\s|\\+)*'?\\d+'?(\\s|\\+)*=(\\s|\\+)*'?\\d+/i"; classtype:web-application-attack; sid:9000010; rev:2;)

# S-05 · un poste utilisateur tente de joindre la base (em1)
alert tcp 10.10.0.0/26 any -> 10.10.0.70 5432 (msg:"LOCAL S-05 LAN-USERS vers PostgreSQL"; flow:stateless; flags:S,12; classtype:policy-violation; sid:9000020; rev:1;)
# S-04 · un hôte de la DMZ autre que l'API tente de joindre la base (écart E07)
alert tcp [10.20.0.0/24,!10.20.0.70] any -> 10.10.0.70 5432 (msg:"LOCAL S-04 DMZ hors API vers PostgreSQL"; flow:stateless; flags:S,12; classtype:policy-violation; sid:9000021; rev:1;)

# S-06 · fragments IP minuscules (nmap -f découpe en morceaux de 8 octets) — À VALIDER EN LABORATOIRE
alert ip any any -> 203.0.113.1 any (msg:"LOCAL S-06 petits fragments IP"; fragbits:M; dsize:<24; threshold:type limit, track by_src, count 1, seconds 60; classtype:misc-activity; sid:9000030; rev:1;)`;
const THR = `# ids/threshold.config — blocage temporaire de la source (figure 6 : « rate_filter »)
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
rate_filter gen_id 1, sig_id 9000004, track by_src, count 100, seconds 1, new_action drop, timeout 20`;
const SONDE = `#!/usr/bin/env python3
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
    time.sleep(max(0, 1 - (time.monotonic() - debut)))   # une itération par seconde`;
const SONDESH = `#!/bin/bash
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
  OUT=\${2:-/srv/sla}; mkdir -p "$OUT"
  { echo "ts_utc,client,cible,type,succes,duree_ms"
    for c in client-ext client-01; do docker exec "$c" cat /tmp/sla.csv; done | sort; } > "$OUT/sla.csv"
  echo "$(($(wc -l < "$OUT/sla.csv") - 1)) mesures dans $OUT/sla.csv" ;;
esac`;

G.roles.ids = {
intro:`<p><b>Votre premier livrable est une décision</b> : netmap fonctionne-t-il sur les cartes réseau d'OPNsense sous VMware (pilote <code>em</code>) ? Le rapport l'a marquée « à valider au J1 » (section 5.3), avec deux replis : le mode Divert (proposé par OPNsense 26.7) ou l'IDS seul avec blocage par pf. Tout le reste en dépend.</p><p>Vous portez aussi C8, la sonde de trafic légitime : c'est elle qui mesure les faux positifs et la disponibilité dont vous avez besoin pour régler l'IPS.</p><p><b>Mise à jour du 17 novembre.</b> La plateforme est VMware, pas Proxmox : les cartes d'OPNsense sont <code>em0</code> (WAN, 203.0.113.1), <code>em1</code> (trunk, parent de <code>vlan01</code> LAN et <code>vlan02</code> DMZ), <code>em2</code> (MGMT). Les déchargements CRC, TSO, LRO et VLAN Hardware Filtering sont déjà désactivés. Le test J1 porte donc sur netmap avec <code>em</code>. Avant d'activer l'IPS, demande au rôle 1 un instantané d'OPNsense (référence actuelle : <code>07-vnc</code>) ; si l'interface web devient injoignable, la console VNC (<code>127.0.0.1::5901</code>) reste disponible.</p><p><b>Comptes et SSH réels.</b> Les VM Debian et Kali ont l'utilisateur <code>karim</code> avec <code>sudo</code>, pas de connexion <code>root</code>. SSH n'est accepté sur les VM que depuis Windows (10.99.0.2) et le collecteur (10.99.0.10) ; le SSH d'OPNsense est désactivé. Avant que l'orchestrateur tourne depuis le collecteur, le rôle 1 doit : installer la clé du collecteur sur p6-docker et p6-kali (pour <code>root</code>, ou pour <code>karim</code> avec un <code>sudo</code> sans mot de passe limité aux commandes utiles), et réactiver le SSH d'OPNsense sur MGMT, par clé seulement (la règle « Admin du pare-feu » autorise déjà le port 22 depuis 10.99.0.10).</p>`,
prep:{titre:"Préparer et tester ses règles sur son PC (avant l'hôte)", quand:"13–21 nov.",
 but:"Le fichier <code>ids/local.rules</code> de l'étape 4, testé hors ligne : chaque capture de scénario déclenche ses sid attendus, et une capture de trafic légitime n'en déclenche aucun.",
 pourquoi:"Les étapes 4 et 5 ont lieu sur l'hôte, pendant des séances partagées. En arrivant avec des règles déjà validées, on n'y perd pas de temps sur la syntaxe (document « Travail en parallèle », rôle 3). Suricata analyse un fichier de capture (option <code>-r</code>) avec le même moteur que le trafic en direct : seule la partie netmap/IPS reste à tester sur l'hôte.",
 prereq:{etapes:[], texte:"Aucune étape d'un autre rôle : tout se fait sur son PC, sous WSL2 + Ubuntu (ou une VM Linux). Optionnel : une capture de trafic légitime fournie par le rôle 4.", modules:[7,9]},
 schema:"seuil-sim",
 ui:[
  {path:"Windows › PowerShell (administrateur)", faire:"<code>wsl -l -v</code> doit afficher Ubuntu en VERSION 2. Sinon : <code>wsl --install -d Ubuntu</code>, puis redémarrer. Dans le terminal Ubuntu, coller avec un clic droit (ou Ctrl+Shift+V)."},
  {path:"Mini-laboratoire Docker", faire:"Quatre conteneurs sur trois réseaux <b>isolés</b> (<code>internal: true</code>, aucun accès à Internet), avec les <b>mêmes adresses que le vrai laboratoire</b> : les règles de l'étape 4 s'appliquent donc sans aucune modification. L'attaquant a une patte dans chaque réseau : 203.0.113.66 (Kali), 10.20.0.80 (DMZ) et 10.10.0.12 (client-03)."}
 ],
 config:[
  {t:"1. Installer les outils (terminal Ubuntu)", code:`sudo apt update && sudo apt upgrade -y
sudo add-apt-repository -y ppa:oisf/suricata-stable
sudo apt update
sudo apt install -y suricata jq tcpdump nmap docker.io docker-compose-v2
sudo usermod -aG docker $USER        # puis fermer et rouvrir le terminal Ubuntu
suricata -V                          # noter la version (8.0.x) pour le rapport
docker run --rm hello-world`},
  {t:"2. Arborescence", code:`mkdir -p ~/projet6/ids/{labo,rules,pcap,out} && cd ~/projet6/ids`},
  {t:"3. Mini-laboratoire", file:"labo/Dockerfile", code:`FROM debian:stable-slim
RUN apt-get update && apt-get install -y --no-install-recommends nmap hping3 curl iproute2 \\
    && rm -rf /var/lib/apt/lists/*
CMD ["sleep", "infinity"]`},
  {t:"", file:"labo/docker-compose.yml", code:`# Mêmes adresses que le rapport. La passerelle Docker est déplacée en fin de plage (.254 / .94)
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
    ipam: { config: [ { subnet: 10.10.0.0/24, gateway: 10.10.0.254 } ] }`, note:"Lancer : <code>cd labo && docker compose up -d --build && cd ..</code> puis <code>docker ps</code> (4 conteneurs)."},
  {t:"4. Les règles", file:"rules/local.rules", code:`# Copier ici, sans rien changer, le bloc « ids/local.rules » de l'étape 4 (bouton Copier).
nano rules/local.rules
suricata -T -S rules/local.rules -l /tmp     # -T : vérifie la syntaxe sans rien analyser`},
  {t:"5. Capturer chaque scénario (deux terminaux)", code:`# Terminal 1 : capture sur le pont du réseau visé (WAN pour S-01, S-02, S-06 ; DMZ pour S-03 ; LAN pour S-05)
cd ~/projet6/ids && sudo tcpdump -i br-wan -w pcap/s01.pcap      # Ctrl+C quand l'attaque est finie

# Terminal 2 : lancer l'attaque depuis le conteneur attaquant
cd ~/projet6/ids/labo
docker compose exec attaquant nmap -sS -Pn -p 1-1024 203.0.113.1                     # s01.pcap  (br-wan)
docker compose exec attaquant timeout 3 hping3 -S --flood -p 443 203.0.113.1        # s02.pcap  (br-wan)
docker compose exec attaquant curl -s "http://10.20.0.70:5000/api/produit?id=1'%20OR%20'1'='1"   # s03.pcap (br-dmz)
docker compose exec attaquant nmap -sS -Pn -p 5432 10.10.0.70                        # s05.pcap  (br-lan)
docker compose exec attaquant nmap -f -sS -Pn -p 443 203.0.113.1                     # s06.pcap  (br-wan)
# Trafic légitime (2 min, br-wan) : 1 requête web par seconde, comme la sonde C8
docker compose exec attaquant sh -c 'for i in $(seq 120); do curl -s -o /dev/null http://203.0.113.1/; sleep 1; done'   # legitime.pcap`},
  {t:"6. Script de vérification", file:"tester.sh", code:`#!/usr/bin/env bash
# Rejoue chaque capture dans Suricata et compare les sid obtenus aux sid attendus.
cd ~/projet6/ids
declare -A ATTENDU=([s01]="9000001 9000002" [s02]="9000003 9000004" [s03]="9000010" [s05]="9000020" [s06]="9000030" [legitime]="")
for cap in s01 s02 s03 s05 s06 legitime; do
  [ -f pcap/\${cap}.pcap ] || { echo "\${cap} : capture absente"; continue; }
  rm -rf out/\${cap} && mkdir -p out/\${cap}
  suricata -r pcap/\${cap}.pcap -S rules/local.rules -k none -l out/\${cap} \\
    --set vars.address-groups.HOME_NET="[10.0.0.0/8,203.0.113.1/32]" > /dev/null 2>&1
  obtenu=$(jq -r 'select(.event_type=="alert") | .alert.signature_id' out/\${cap}/eve.json 2>/dev/null | sort -u | xargs)
  [ "\${obtenu}" = "\${ATTENDU[\${cap}]}" ] && etat="OK" || etat="ÉCART"
  printf '%-9s attendu [%s]  obtenu [%s]  %s\\n' "\${cap}" "\${ATTENDU[\${cap}]}" "\${obtenu}" "\${etat}"
done`, note:"Rendre exécutable puis lancer : <code>chmod +x tester.sh && ./tester.sh</code>."}
 ],
 tester:[{cmd:"./tester.sh", sortie:`s01       attendu [9000001 9000002]  obtenu [9000001 9000002]  OK
s02       attendu [9000003 9000004]  obtenu [9000003 9000004]  OK
s03       attendu [9000010]  obtenu [9000010]  OK
s05       attendu [9000020]  obtenu [9000020]  OK
s06       attendu [9000030]  obtenu []  ÉCART
legitime  attendu []  obtenu []  OK`, sens:[["OK","la capture déclenche exactement les règles prévues pour ce scénario"],["s06 ÉCART","exemple réaliste : la règle de fragments est marquée « À VALIDER EN LABORATOIRE » ; noter le résultat, essayer une variante, ou la signaler comme limite"],["legitime OK","aucune alerte sur du trafic normal : premier indice de zéro faux positif (la vraie mesure de 2 h se fait à l'étape 6)"]]},
  {t:"Lire une alerte en détail", cmd:"jq -c 'select(.event_type==\"alert\") | [.timestamp, .src_ip, .dest_ip, .dest_port, .alert.signature_id, .alert.signature]' out/s01/eve.json | head -3", sortie:`["2026-11-14T10:02:11.481+0000","203.0.113.66","203.0.113.1",113,9000001,"LOCAL S-01 scan SYN > 20 en 5 s"]`, sens:[["src_ip","l'attaquant (Kali) : c'est la source suivie par track by_src"],["dest_port","le port sondé au moment où le seuil de 20 SYN est franchi"],["signature_id","le sid de la règle : c'est lui que C11 compte pour le taux de détection"]]}],
 pannes:[
  ["<code>suricata -T</code> signale une erreur de règle","Le message donne le numéro de ligne et le mot-clé fautif","Corriger la ligne (souvent un guillemet ou un point-virgule manquant après un copier-coller), relancer <code>-T</code>."],
  ["Aucune alerte, <code>eve.json</code> vide","<code>tcpdump -nr pcap/s01.pcap | head</code> : la capture est vide, ou prise sur le mauvais pont","Capturer sur <code>br-wan</code>, <code>br-dmz</code> ou <code>br-lan</code> selon la cible. Jamais <code>-i any</code> : les paquets apparaissent en double et faussent les seuils."],
  ["<code>docker compose up</code> : « address already in use » ou conflit de passerelle","Une adresse du compose est prise par la passerelle Docker","Vérifier les lignes <code>gateway:</code> (.254 et .94), puis <code>docker compose down</code> et relancer."],
  ["<code>tcpdump: permission denied</code>","tcpdump lancé sans sudo","Relancer avec <code>sudo</code>."],
  ["Le flood S-02 produit une capture énorme","hping3 en mode flood envoie des centaines de milliers de paquets","Garder <code>timeout 3</code> ; ne pas pousser ce PCAP dans Git s'il dépasse 50 Mo."]
 ],
 fini:["Mini-laboratoire lancé : 4 conteneurs, 3 réseaux isolés","<code>suricata -T</code> accepte <code>local.rules</code> sans erreur","Captures s01, s02, s03, s05, s06 et legitime enregistrées","<code>tester.sh</code> : OK partout, ou écart expliqué et noté (S-06)","Règles, script et tableau des résultats poussés dans Git"],
 livrer:"Dans le dépôt Git, dossier <code>ids/</code> : <code>local.rules</code> testé, <code>tester.sh</code>, captures de moins de 50 Mo, et <code>docs/tests-regles.md</code> (sortie de <code>tester.sh</code> commentée). Message au rôle 5 avec les commandes d'attaque utilisées, pour aligner <code>scenarios.yaml</code>."
},
steps:[
/* 1 */ {titre:"Test de plateforme : netmap sur les cartes VMware (em)", quand:"14–15 nov.",
 maj:`<p>Les cartes sont des cartes Intel e1000 émulées par VMware (pilote <code>em</code>), et non VirtIO. Le test reste le même : activer l'IPS sur <code>em0</code>, vérifier le trafic et <code>dmesg | grep netmap</code>. Si <code>em</code> pose problème, une option à décider avec le rôle 1 est de passer les cartes d'OPNsense en <code>vmxnet3</code> (pilote <code>vmx</code>) dans le .vmx, VM éteinte et après un instantané. Les interfaces changent alors de nom et doivent être réaffectées. Sinon, appliquer les replis prévus (Divert, ou IDS seul + pf).</p>`,
 but:"Une décision écrite : IPS en ligne avec netmap, ou repli (Divert, ou IDS seul + blocage pf), avec la version d'OPNsense testée.",
 pourquoi:"Section 5.3 : Suricata en mode IPS utilise netmap ; « repli prévu si netmap est instable sur les cartes em de VMware ». Section 11.2 : risque n° 1, test au J1. Jalon du 15 novembre.",
 prereq:{etapes:["parefeu-2"], texte:"WAN et TRUNK affectés, offloading désactivé. Carte de configuration branchée pour télécharger ET Open.", modules:[7]},
 schema:"netmap-position",
 ui:[
  {path:"Services › Intrusion Detection › Administration", faire:"Activer, cocher « IPS mode », interface : WAN seulement pour ce test. Onglet Download : ET Open, télécharger. Activer une seule famille simple (par exemple « scan »)."},
  {path:"System › Settings › Tunables", faire:"Si netmap refuse la carte em, ajouter <code>dev.netmap.admode</code> = <code>2</code> (mode émulé, recommandé par le manuel IPS pour les cartes non prises en charge), redémarrer, recommencer."}
 ],
 config:[{t:"Protocole de test (10 minutes)", code:`# Le 15 novembre, les services (rôle 4) ne sont pas encore publiés : le test se fait depuis Kali, vers OPNsense lui-même.
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
tail -2 /tmp/j1.txt`}],
 tester:[{cmd:"(bilan du test)", sortie:"600 packets transmitted, 597 received, 0.5% packet loss, time 599812ms\nrtt min/avg/max/mdev = 0.310/0.620/4.100/0.200 ms\n(dmesg : aucune erreur netmap répétée ; suricata.log : « engine started »)", sens:[["597 received","le trafic passe à travers Suricata en ligne : netmap fonctionne"],["aucune erreur répétée","pas de blocage de l'interface"],["alertes > 0","Suricata voit bien le trafic du WAN"]]}],
 pannes:[
  ["Plus aucun trafic après activation de l'IPS","<code>dmesg | grep netmap</code> : erreurs sur em0","Essayer <code>dev.netmap.admode=2</code> ; sinon repli Divert (mode IPS « Divert » dans l'administration IDS) ou IDS seul."],
  ["Le trafic passe mais aucune alerte","Home networks ne contient pas 203.0.113.1 (écart E05)","Étape 2 ; pour ce test, utiliser une règle qui vise l'adresse explicitement."],
  ["Latence très élevée ou pertes","Offloading encore actif","Rôle 2, étape 2 : désactiver CRC, TSO, LRO, redémarrer."]
 ],
 fini:["Test de 10 minutes mené (ping continu + scan), règle ICMP temporaire supprimée ensuite","Décision écrite : netmap / Divert / IDS seul, version d'OPNsense notée","Décision reportée dans le rapport (jalon du 15 novembre)"],
 livrer:"Fichier <code>docs/decision-ips.md</code> (version, mode retenu, résultats du test) et message à tous, en particulier aux rôles 1 et 5."
},
/* 2 */ {titre:"Configurer l'IDS : interfaces, réseaux maison, journaux", quand:"16 nov. puis 22 nov.",
 but:"Suricata configuré sur em0 et sur l'interface parente du trunk, avec des réseaux maison corrects et des alertes EVE exportées.",
 pourquoi:"Section 5.3 : IPS sur em0 (WAN) et sur em1 (parent du trunk) ; avec des VLAN, l'IPS s'active sur l'interface parente. Le manuel IPS d'OPNsense : désactiver l'offloading, mode promiscuous pour les VLAN. Écart E05 : 203.0.113.1 doit être un réseau maison.",
 prereq:{etapes:["ids-1","parefeu-2"], modules:[7,3]},
 schema:"netmap-position",
 ui:[
  {path:"Services › Intrusion Detection › Administration › Settings", verif:true, faire:"Enabled ; IPS mode selon la décision du J1 ; <b>Promiscuous mode</b> coché (VLAN) ; Interfaces : <code>WAN</code> et <code>TRUNK</code> (em1, affectée sans adresse par le rôle 2) ; Pattern matcher : Hyperscan si proposé, sinon Aho-Corasick."},
  {path:"… › Administration › Advanced (Home networks)", verif:true, faire:"Réseaux maison : <code>10.0.0.0/8</code> et <code>203.0.113.1/32</code>. Pas tout 203.0.113.0/24 : Kali deviendrait « maison » et les règles « depuis l'extérieur » ne la verraient plus."},
  {path:"… › Administration › Logging", verif:true, faire:"Activer la sortie EVE en syslog (alertes) pour que C9 la reçoive ; garder aussi eve.json local. Rotation : quotidienne, 7 jours."},
  {path:"Jusqu'au 22 novembre", faire:"En état A, l'IDS reste <b>désactivé</b> : l'état A n'a pas d'inspection (section 3). Ne l'activez qu'après le passage en état B."}
 ],
 tester:[{t:"Shell OPNsense (état B)", cmd:"grep -A3 'HOME_NET' /usr/local/etc/suricata/suricata.yaml | head -4\npgrep -l suricata", sortie:"    HOME_NET: \"[10.0.0.0/8,203.0.113.1/32]\"\n…\n12345 suricata", sens:[["HOME_NET","la configuration générée contient bien l'adresse publique"],["suricata","le moteur tourne"]]}],
 pannes:[["Aucune alerte sur em1","TRUNK non sélectionné, ou mode promiscuous absent","Sélectionner l'interface parente, cocher promiscuous, redémarrer le service."],["Les alertes n'arrivent pas au collecteur","Cible syslog sans l'application suricata","Rôle 2, étape 5 : ajouter suricata à la cible distante."],["L'IDS bloque déjà du trafic en état B","Une politique drop est active","Toutes les familles en alert jusqu'à l'étape 6."]],
 fini:["WAN et TRUNK sélectionnés, promiscuous coché","Réseaux maison : 10.0.0.0/8 et 203.0.113.1/32","Alertes visibles dans eve.json et sur le collecteur"],
 livrer:"Export de la configuration (avec le rôle 2) et message au rôle 6 : « les alertes EVE partent en syslog ; eve.json local dans /var/log/suricata/ »."
},
/* 3 */ {titre:"C8 : sonde SLA et trafic légitime", quand:"16–19 nov.",
 but:"Une sonde qui produit sla.csv pendant toutes les mesures, pilotable par l'orchestrateur, et les mesures de débit iperf3.",
 pourquoi:"Section 7.8 : 1 requête HTTPS par seconde depuis client-ext et client-01, 1 requête DNS toutes les 5 s, chaque requête enregistrée ; débit iperf3 par FA6 / F09 hors campagnes. Section 9.3 : 2 h de trafic légitime seul pour mesurer les faux positifs. ENF4 : disponibilité ≥ 99 %.",
 prereq:{etapes:["services-6"], texte:"Format de sla.csv : page <a href='#contrat-sla'>Contrats</a>. L'image des postes doit contenir python3-requests, python3-dnspython et procps (pour pkill).", modules:[10,1]},
 schema:"p01-panne",
 config:[
  {t:"Sonde", file:"sonde/sonde.py", code:SONDE},
  {t:"Pilotage", file:"sonde/sonde.sh (sur l'hôte Docker, dans /opt/projet6/sonde/)", code:SONDESH},
  {t:"Débit iperf3 (hors campagnes : A, B avec IPS, B sans IPS)", code:`# Le rôle 2 active la redirection et la règle FA6 / F09 le temps de la mesure, puis les désactive.
docker exec client-ext iperf3 -c 203.0.113.1 -p 5201 -t 30 -J > /srv/sla/iperf-A.json
#   -J : sortie JSON (débit moyen dans end.sum_received.bits_per_second), lue par le rôle 6
#   état B : iperf-B-avec-ips.json, puis Suricata désactivé : iperf-B-sans-ips.json`}
 ],
 tester:[{cmd:"/opt/projet6/sonde/sonde.sh start; sleep 20; /opt/projet6/sonde/sonde.sh stop /tmp/essai\nhead -4 /tmp/essai/sla.csv", sortie:"40 mesures dans /tmp/essai/sla.csv\nts_utc,client,cible,type,succes,duree_ms\n2026-11-18T14:02:11.003Z,client-01,10.20.0.40,dns,1,3\n2026-11-18T14:02:11.004Z,client-01,https://10.20.0.10/api/produits,https,1,41\n2026-11-18T14:02:11.010Z,client-ext,https://203.0.113.1/api/produits,https,1,48", sens:[["≈ 40 mesures en 20 s","2 clients × 1 requête/s, plus le DNS toutes les 5 s"],["succes 1, duree_ms 41","requête servie en 41 ms"],["horodatage …Z","UTC, à la milliseconde : prêt pour la corrélation"]]}],
 pannes:[["« pkill: not found »","procps absent de l'image des postes","Ajouter procps au Dockerfile des postes (rôle 4) et reconstruire."],["Toutes les requêtes HTTPS échouent depuis client-01 en état B","F05 (rôle 2) ou passerelle (rôle 4)","test_flux T009 doit être conforme."],["Des secondes manquent dans sla.csv","Requêtes en délai dépassé (2 s) : la boucle prend du retard","Normal pendant un flood : c'est justement l'indisponibilité mesurée."]],
 fini:["sla.csv produit avec les colonnes du contrat","start / stop fonctionnent depuis le collecteur par SSH","Une mesure iperf3 de référence (état A) enregistrée"],
 livrer:"Commit de <code>sonde/</code>. Message aux rôles 5 et 6 : « sonde.sh start | stop ; sla.csv dans /srv/sla/ sur l'hôte Docker »."
},
/* 4 */ {titre:"Écrire les règles locales et le blocage temporaire", quand:"22 nov.",
 but:"Le jeu de règles C4 : ET Open + règles locales sid 9000001 à 9000099, et le rate_filter de S-01, chargés par Suricata.",
 pourquoi:"Section 7.4 : seuil de scan SYN (> 20 SYN en 5 s, track by_src), seuil de SYN flood, motifs d'injection SQL sur /api/, fragments anormaux, LAN-USERS → 5432. Figure 6 : blocage temporaire de la source (rate_filter). Écart E07 : règle ajoutée pour S-04.",
 prereq:{etapes:["ids-2","parefeu-8"], modules:[7]},
 schema:"seuil-sim",
 config:[
  {t:"Règles locales", file:"ids/local.rules", code:RULES},
  {t:"Blocage temporaire de S-01 et S-02", file:"ids/threshold.config", code:THR},
  {t:"Charger les fichiers dans OPNsense", verif:"méthode d'ajout de règles personnalisées", code:`# OPNsense n'a pas d'éditeur de règles Suricata. Deux méthodes connues (forum OPNsense), à valider :
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
grep -c 9000 /var/log/suricata/suricata.log; tail -n 3 /var/log/suricata/suricata.log`}
 ],
 tester:[{t:"Shell OPNsense", cmd:"suricata -T -c /usr/local/etc/suricata/suricata.yaml 2>&1 | tail -2\ngrep -E 'rules successfully loaded|signatures processed' /var/log/suricata/suricata.log | tail -1", sortie:"… Configuration provided was successfully loaded. Exiting.\n… 1 rule files processed. 48211 rules successfully loaded, 0 rules failed", sens:[["successfully loaded","la syntaxe des règles et du fichier de seuils est valide"],["0 rules failed","aucune règle locale rejetée ; sinon le journal donne le sid fautif"]]}],
 pannes:[["« failed to parse signature » pour un sid 90000xx","Faute de syntaxe (point-virgule, guillemet)","Corriger la règle ; tester avec <code>suricata -T</code> avant de recharger."],["Les règles locales disparaissent après une mise à jour des règles","Fichier écrasé par la régénération","Utiliser la méthode B (ensemble déclaré) ; garder le fichier dans Git."],["rate_filter sans effet","threshold-file absent de la configuration, ou interaction avec « threshold limit » de la règle","Déclarer le fichier (gabarit custom.yaml) ; vérifier dans suricata.log ; essayer la règle compteur sans threshold."]],
 fini:["ET Open téléchargé (fenêtre de configuration)","Règles 9000001–9000030 chargées, 0 échec","rate_filter de 9000002 et 9000004 chargés, en variante new_action alert","Tout est en action alert"],
 livrer:"Commit de <code>ids/local.rules</code> et <code>ids/threshold.config</code> avec le hash du jeu de règles (<code>sha256sum</code>) dans <code>ids/VERSION.txt</code>."
},
/* 5 */ {titre:"Mode IDS : vérifier que chaque scénario est détecté", quand:"23 nov.",
 but:"Chaque scénario produit l'alerte attendue en mode IDS, sans rien bloquer.",
 pourquoi:"Section 7.3 : IDS (alert uniquement) pendant le réglage. Figure 12 : rejouer trafic légitime et scénarios S-01 à S-06, mesurer détection et faux positifs.",
 prereq:{etapes:["ids-4","attaques-3"], modules:[7,9]},
 schema:"netmap-position", schemaSt:"A",
 config:[{t:"Une exécution par scénario (collecteur)", code:`python3 campagne/orchestrateur.py --etat B --n 1     # IPS en alert seulement à ce stade
ssh root@10.99.0.1 "grep '\\"event_type\\":\\"alert\\"' /var/log/suricata/eve.json | tail -n 50" > /tmp/alertes.json
python3 -c "import json,collections;c=collections.Counter(json.loads(l)['alert']['signature_id'] for l in open('/tmp/alertes.json'));print(c)"`}],
 tester:[{cmd:"(compte des alertes par sid)", sortie:"Counter({9000001: 3, 9000003: 6, 9000002: 1, 9000004: 1, 9000010: 1, 9000020: 2, 9000021: 1, 9000030: 1, 2010937: 4, …})", sens:[["9000001","S-01 détecté (une alerte par fenêtre de 5 s)"],["9000002, 9000004","compteurs du blocage : une alerte au plus par 20 s ; en mode IDS, le rate_filter est en new_action alert"],["9000010","S-03 : injection vue sur em1"],["9000020, 9000021","S-05 et S-04 détectés"],["9000030","S-06 : à valider ; si absent, ajuster la règle"],["sid à 7 chiffres","règles ET Open : candidates aux faux positifs, à mesurer à l'étape 6"]]}],
 pannes:[["Pas d'alerte S-03","La requête n'arrive pas en clair sur em1, ou URI encodée","Vérifier sur em1 avec tcpdump ; http.uri est normalisée, le motif doit viser la forme décodée."],["Pas d'alerte S-01","HOME_NET ou règle visant 203.0.113.1","Étape 2 ; vérifier que la règle vise l'adresse publique (em0 voit l'adresse avant NAT)."],["Des centaines d'alertes S-01","threshold absent sur 9000001","Une alerte par fenêtre : <code>threshold:type both</code>."]],
 fini:["Chaque scénario a au moins une alerte locale (S-06 : à valider)","Aucun paquet bloqué en mode IDS"],
 livrer:"Tableau « scénario → sid observés » dans <code>docs/detection-ids.md</code>, envoyé au rôle 6."
},
/* 6 */ {titre:"Régler pendant 2 h, puis passer en IPS", quand:"23–24 nov.",
 but:"Les familles validées en drop, les autres en alert, avec au plus 1 faux positif par heure et une disponibilité d'au moins 99 %.",
 pourquoi:"Section 9.3 et figure 12 : 2 h de trafic légitime seul (une fenêtre plus courte ne permet pas de vérifier « 1 par heure ») ; drop seulement pour les familles validées ; critère d'arrêt : ≤ 1 FP/h et disponibilité ≥ 99 %. Jalon du 24 novembre.",
 prereq:{etapes:["ids-5","analyse-3"], texte:"Le rôle 6 calcule fp_par_sid.csv à partir d'events.csv.", modules:[7,10]},
 schema:"fp-tuning",
 ui:[{path:"Services › Intrusion Detection › Policy", verif:"gestion des politiques", faire:"Créer une politique : action « drop » pour 9000010 et pour les familles ET validées. Les blocages de S-01 et S-02 ne passent pas par la politique : ils viennent du rate_filter (lignes « Phase IPS » de threshold.config, new_action drop). Laisser 9000001, 9000003, 9000020, 9000021, 9000030 en alert : ce sont les règles de détection ; pf bloque déjà S-04 et S-05."}],
 config:[{t:"Boucle de réglage", code:`# 1. 2 h de trafic légitime seul, IDS en alert (collecteur)
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
tail -n 60 /var/log/suricata/stats.log | grep -iE 'drop|block'        # compteurs de rejet (noms : à vérifier sur votre version)`}],
 tester:[{cmd:"column -s, -t resultats/B/reglage/fp_par_sid.csv | head -5", sortie:"sid       alertes  fp_par_heure\n2013504   6        3.0\n2027390   1        0.5\n9000030   1        0.5", sens:[["3.0 > 1","famille à laisser en alert (ou à supprimer avec justification)"],["total des familles en drop ≤ 1","critère de la section 9.3"]]}],
 pannes:[["La sonde échoue après le passage en drop","Une règle en drop touche le trafic légitime","Remettre la famille en alert (figure 12, « remettre en alert les règles fautives »)."],["Aucun « blocked » dans EVE","Politique non appliquée, ou mode IDS pcap","Vérifier le mode IPS et la politique ; relancer le service."],["Kali reste bloquée longtemps","timeout du rate_filter trop long","timeout 20 s, inférieur à la pause de 30 s."]],
 fini:["fp_par_sid.csv sur 2 h, et décision par famille consignée","Familles en drop : ≤ 1 FP/h au total","Disponibilité de la sonde ≥ 99 % sur les 2 h","Jeu de règles figé : hash dans ids/VERSION.txt, commit"],
 livrer:"Message aux rôles 1 et 5 : « IPS prêt, jeu de règles figé (hash …) ». Le rôle 1 peut préparer etat-B."
},
/* 7 */ {titre:"Préparer la panne P-01", quand:"24 nov.",
 but:"Les commandes d'arrêt et de redémarrage de Suricata testées, et le protocole de mesure prêt pour l'orchestrateur.",
 pourquoi:"Section 8.5 et figure 9 : arrêter Suricata pendant un SYN flood ; deux comportements possibles ; on ne suppose pas, on mesure (disponibilité et alertes avant, pendant, après, temps de reprise). Section 10.3 : P-01 en état B, 5 répétitions.",
 prereq:{etapes:["ids-6"], modules:[7,9]},
 schema:"p01-panne",
 config:[{t:"Commandes à valider sur OPNsense (en dehors de toute campagne)", verif:"noms des actions configctl", code:`configctl ids stop      # arrêt du moteur (figure 9 : « service suricata stop »)
pgrep -l suricata || echo "Suricata arrêté"
configctl ids start
sleep 5; pgrep -l suricata
# Si configctl n'a pas ces actions sur votre version : service suricata onestop / onestart,
# ou le bouton d'arrêt de Services › Intrusion Detection. Reporter la commande validée
# dans campagne/scenarios.yaml (P-01, champ actions).`}],
 tester:[{t:"Essai à blanc avec la sonde", cmd:"python3 campagne/orchestrateur.py --etat B --scenario P-01 --n 1", sortie:"B-P01-01 · Panne de l'IPS pendant un SYN flood\nTerminé : resultats/B/2026-11-24/runs.csv", sens:[["une exécution complète","arrêt à t0 + 10 s, redémarrage à t0 + 40 s, sonde active : la mesure est prête"]]}],
 pannes:[["Suricata ne redémarre pas","<code>tail /var/log/suricata/suricata.log</code>","Démarrer depuis l'interface ; ajuster la commande dans scenarios.yaml."],["Toute l'interface WAN reste coupée après le redémarrage","netmap n'a pas repris l'interface","Noter ce comportement : c'est un résultat ; redémarrer l'interface et documenter la procédure de reprise."],["Pas de mesure pendant la panne","Sonde arrêtée","La sonde doit tourner avant t0 et après t1 + 30 s."]],
 fini:["Arrêt et redémarrage testés à la main","Une exécution à blanc de P-01 complète","Commande définitive reportée dans scenarios.yaml"],
 livrer:"Fichier <code>docs/p01.md</code> (commandes, durée de redémarrage observée) et message au rôle 5 : « P-01 prêt pour la campagne B »."
}
],
demo:{intro:"Ce que vous montrez en 2 minutes : Suricata est en ligne avant pf, et il bloque sans gêner les vrais clients.", etapes:["Le schéma netmap : où Suricata prend le paquet.","Un scan S-01 depuis Kali : alerte 9000001, puis alerte 9000002 « blocked » dans EVE et compteur de rejet en hausse dans stats.log ; aucune ligne filterlog pour les paquets rejetés.","fp_par_sid.csv : ≤ 1 FP/h pour les familles en drop.","La disponibilité de la sonde pendant ce temps (≥ 99 %)."], secours:"extraits d'EVE et de sla.csv dans le rapport."},
questions:[
 ["Pourquoi Suricata est-il avant pf ?","Avec netmap, Suricata intercepte les paquets entre la carte et la pile du noyau : pour le trafic entrant, il les voit avant pf. Il détecte donc aussi les sondes vers des ports fermés, et un paquet qu'il rejette n'atteint jamais pf. Nous l'avons vérifié au test J1 : un paquet rejeté apparaît dans EVE (« blocked ») et pas dans filterlog."],
 ["Pourquoi ne pas tout passer en drop directement ?","Des règles génériques se déclenchent sur du trafic légitime : en drop, elles feraient chuter la disponibilité. Nous mesurons d'abord 2 h de trafic légitime en alert, règle par règle, et ne passons en drop que les familles à moins d'1 faux positif par heure (section 9.3)."],
 ["Comment fonctionne le blocage de S-01 ?","La règle 9000002 compte les SYN de chaque source vers les ports non publiés ; un rate_filter change son action en drop pendant 20 s dès 20 SYN en 5 s. S-02 suit le même principe avec 9000004 (100 SYN en 1 s). Les premières sondes passent donc : un port publié peut être découvert s'il est sondé tôt. Nous l'écrivons tel quel dans les résultats."],
 ["Pourquoi l'injection n'est-elle pas vue sur le WAN ?","Elle est chiffrée en TLS jusqu'à nginx. Le saut nginx → API est en HTTP clair et passe par em1 : c'est là que la règle 9000010 la voit. C'est une limite assumée (section 11.3)."],
 ["Que se passe-t-il si Suricata tombe ?","C'est P-01 : nous arrêtons Suricata pendant un SYN flood et mesurons la disponibilité et les alertes avant, pendant et après, puis le temps de reprise. Le rapport ne suppose pas le comportement : [résultat mesuré]."]
]
};
})();
