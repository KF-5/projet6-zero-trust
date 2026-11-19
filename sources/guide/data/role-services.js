/* Rôle 4 : services applicatifs (C5) et outil de capture (C10). */
window.G = window.G || {}; G.roles = G.roles || {};
(function(){
const NETA = `#!/bin/bash
# services/reseaux-A.sh — réseaux Docker de l'état A (section 4.1). À lancer en root sur l'hôte Docker.
set -eu
TRUNK=ens33        # carte reliée à p6-trunk (« eth1 » du rapport)
WAN=ens34          # carte reliée à p6-wan (« eth2 » du rapport)
docker network create -d macvlan -o parent=$TRUNK.10 --subnet 10.10.0.0/24 --gateway 10.10.0.1 lan
docker network create -d macvlan -o parent=$TRUNK.20 --subnet 10.20.0.0/24 --gateway 10.20.0.1 dmz
docker network create -d macvlan -o parent=$WAN      --subnet 203.0.113.0/24 --gateway 203.0.113.1 wan
#   parent=ens33.10 : Docker crée lui-même la sous-interface VLAN 10 (le point dans le nom le lui indique)
#   --gateway : devient la route par défaut des conteneurs (la passerelle OPNsense du segment)
docker network ls --filter driver=macvlan`;
const NETB = `#!/bin/bash
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
docker network ls --filter driver=macvlan`;
const COMPOSE = `# services/compose.yml — mêmes conteneurs, mêmes adresses en A et en B ; seuls les réseaux changent.
# Lancement :  docker compose --env-file etat-A.env up -d   (ou etat-B.env)
name: projet6
networks:                                  # réseaux créés par reseaux-A.sh / reseaux-B.sh
  web:   {external: true, name: \${NET_WEB}}     # A : dmz       B : dmz-web
  dns:   {external: true, name: \${NET_DNS}}     # A : dmz       B : dmz-dns
  app:   {external: true, name: \${NET_APP}}     # A : dmz       B : dmz-app
  users: {external: true, name: \${NET_USERS}}   # A : lan       B : lan-users
  db:    {external: true, name: \${NET_DB}}      # A : lan       B : lan-db
  admin: {external: true, name: \${NET_ADMIN}}   # A : lan       B : lan-admin
  wan:   {external: true, name: wan}
services:
  postgres:                                # C5 — base de l'application (LAN-DB)
    image: postgres:17
    container_name: postgres
    environment: {POSTGRES_PASSWORD: \${PG_ADMIN_PWD}, APP_PWD: \${PG_APP_PWD}, LECTEUR_PWD: \${PG_LECTEUR_PWD}}
    volumes: ["./postgres/init.sh:/docker-entrypoint-initdb.d/init.sh:ro"]
    networks: {db: {ipv4_address: 10.10.0.70}}
  api:                                     # C5 — API Flask (DMZ-APP), point d'entrée vulnérable : labo uniquement
    build: ./api
    container_name: api
    environment: {DSN: "host=10.10.0.70 dbname=catalogue user=app password=\${PG_APP_PWD} connect_timeout=3"}
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
    environment: {PGPASSWORD: \${PG_LECTEUR_PWD}}
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
    environment: {PGPASSWORD: \${PG_LECTEUR_PWD}}
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
    networks: {wan: {ipv4_address: 203.0.113.10}}`;
const ENVS = `# services/etat-A.env
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
NET_ADMIN=lan-admin`;
const OUTILS = `# services/outils/Dockerfile — image commune des postes (clients, dmz-pivot, admin, client-ext, iperf3)
FROM debian:13-slim
RUN apt-get update && apt-get install -y --no-install-recommends \\
      curl ca-certificates dnsutils nmap netcat-openbsd postgresql-client iperf3 \\
      iproute2 iputils-ping procps openssh-client python3 python3-requests python3-dnspython \\
    && rm -rf /var/lib/apt/lists/*
#   nmap, psql : nécessaires aux scénarios S-04 et S-05 lancés dans dmz-pivot et client-03
#   python3-requests, python3-dnspython, procps (pkill) : sonde SLA du rôle 3
# Construction (pendant une fenêtre de configuration) :  docker build -t projet6/outils services/outils`;
const INIT = `#!/bin/bash
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
#   La colonne « secret » ne doit jamais sortir par /api/produits : elle sort par l'injection (S-03).`;
const APIPY = `# services/api/app.py — API « catalogue » (C5). LABORATOIRE UNIQUEMENT : /api/produit est volontairement vulnérable.
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
    app.run(host="0.0.0.0", port=5000, threaded=True)`;
const APIDF = `# services/api/Dockerfile — la construction télécharge Flask et psycopg (pip) : carte NAT branchée
FROM python:3.13-slim
RUN pip install --no-cache-dir flask "psycopg[binary]"
COPY app.py /app/app.py
CMD ["python", "/app/app.py"]`;
const NGINX = `# services/nginx/site.conf — TLS et reverse proxy vers l'API (C5)
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
        return 200 "Catalogue Projet 6\\n";            # page d'accueil statique, servie par nginx lui-même
    }
}`;
const BIND = `// services/bind9/named.conf — zone publique et résolution interne (C5)
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
;  « .test » est réservé aux essais (RFC 2606) : aucun risque de collision avec un vrai domaine`;
const CAPT = `#!/bin/bash
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
  nsenter -t "$PID" -n tcpdump -i eth0 -U -s 0 -Z root -w "$DIR/\${RUN}_\${POINT}.pcap" >/dev/null 2>"$DIR/.$RUN-$POINT.err" &
  echo $! > "$PIDF"                                                  # mémorise tcpdump pour l'arrêter
  sleep 1                                                            # laisse tcpdump démarrer avant t0
elif [ "$ACTION" = stop ]; then
  kill -INT "$(cat "$PIDF")" && rm -f "$PIDF"                        # INT : tcpdump ferme proprement le fichier
  sleep 1; ls -l "$DIR/\${RUN}_\${POINT}.pcap"
fi`;

G.roles.services = {
intro:`<p><b>Vos conteneurs sont la cible de tout le monde</b> : test_flux (rôle 2), sonde (rôle 3), attaques (rôle 5), captures (rôle 6). Leur nom de conteneur et leur adresse doivent correspondre exactement à <code>hosts.csv</code> (page Contrats).</p><p><b>Mise à jour du 17 novembre.</b> Les étapes 1 à 6 ont été réalisées par le rôle 1 : l'état A tourne sur <code>p6-docker</code> (10.99.0.20) avec 10 conteneurs, et <code>tests.sh</code> donne 12/12. Les fichiers sont dans <code>~/p6-etat-a</code> sur la VM (copie de <code>D:\\Labo-P6\\docker\\p6-etat-a</code> sur l'hôte) : un seul <code>docker-compose.yml</code> (projet <code>p6</code>), <code>setup.sh</code> relançable, <code>tests.sh</code>. Vérifie-les, reprends-les dans le dépôt Git, puis enchaîne sur l'étape 7 (outil de capture C10).</p><p>Cartes réelles de <code>p6-docker</code> : <code>ens32</code> (MGMT), <code>ens33</code> (trunk, « eth1 » du rapport), <code>ens34</code> (WAN, « eth2 »), <code>ens35</code> (NAT, débranchée). Réseaux Docker réels : <code>p6_lan</code>, <code>p6_dmz</code>, <code>p6_wan</code> (le nom du projet Compose est ajouté devant). Docker installé : <b>26.1.5</b> avec Compose 2.26.1 (paquets Debian <code>docker.io</code> et <code>docker-compose</code>), pas Docker Engine 29. La VM n'a pas Internet : pour un <code>apt</code> ou un <code>docker pull</code>, demande au rôle 1 de rebrancher sa carte NAT le temps du téléchargement.</p>`,
steps:[
/* 1 */ {titre:"Installer Docker et les outils sur l'hôte Docker", quand:"14 nov.", fait:true,
 maj:`<p>Fait par le rôle 1 : Docker <b>26.1.5</b> et Compose 2.26.1 (paquets Debian), tcpdump installé, <code>karim</code> dans le groupe <code>docker</code>. Le filtre SSH du rôle 1 est la table <code>inet p6_ssh</code> (pas <code>inet mgmt</code>) : <code>sudo nft list table inet p6_ssh</code> doit toujours l'afficher à côté des tables de Docker.</p>`,
 but:"Docker (26.1.5, paquets Debian) et les outils de test (nc, dig, tcpdump) installés sur p6-docker, sans casser le pare-feu MGMT.",
 pourquoi:"Section 6 : les services tournent en conteneurs Docker rattachés aux VLAN par macvlan (4 VM au lieu de 9, section 11.1). test_flux et les captures s'exécutent depuis cet hôte.",
 prereq:{etapes:["chef-4"], texte:"Fait par le rôle 1. Pour toute nouvelle installation : carte NAT de p6-docker rebranchée le temps du téléchargement.", modules:[8]},
 config:[
  {t:"Dépôt officiel Docker pour Debian 13", verif:"docs.docker.com › Install on Debian", code:`apt-get update && apt-get install -y ca-certificates curl
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
apt-get install -y netcat-openbsd dnsutils tcpdump      # test_flux (rôle 2) et captures (étape 7)`},
  {t:"Images à télécharger pendant la même fenêtre", code:`docker pull postgres:17
docker pull nginx:stable
docker pull python:3.13-slim
docker pull internetsystemsconsortium/bind9:9.20
docker pull debian:13-slim`},
  {t:"Image commune des postes, construite pendant la même fenêtre", file:"services/outils/Dockerfile", code:OUTILS},
  {t:"Construire", code:`mkdir -p /opt/projet6/services/outils     # ou le dépôt, s'il est déjà cloné (rôle 1, étape 7)
# y placer le Dockerfile ci-dessus, puis :
docker build -t projet6/outils /opt/projet6/services/outils     # apt-get dans l'image : carte NAT branchée`, note:"Après cela, la carte NAT peut être débranchée. Seule l'image de l'API (étape 3) demandera de la rebrancher, le temps de son <code>build</code>."}
 ],
 tester:[{cmd:"docker version --format '{{.Server.Version}}'\ndocker compose version\nnft list tables", sortie:"26.1.5\nDocker Compose version 2.26.1\ntable inet p6_ssh\ntable ip nat\ntable ip filter\n…", sens:[["29.x.y","Docker Engine 29"],["table inet p6_ssh","le filtre SSH du rôle 1 est toujours là"],["table ip nat / filter","tables créées par Docker (selon la version, ip ou inet)"]]}],
 pannes:[
  ["<code>apt-get update</code> échoue","Carte NAT débranchée","Rebrancher le temps de l'installation (rôle 1, étape 4)."],
  ["Plus de SSH depuis le collecteur après l'installation","<code>nft list ruleset</code>","Recharger <code>/etc/nftables.d/mgmt.nft</code> ; ne jamais utiliser <code>flush ruleset</code> sur cet hôte."],
  ["« permission denied » sur docker","Utilisateur hors du groupe docker","Travailler en root, ou <code>usermod -aG docker &lt;vous&gt;</code> puis reconnexion."]
 ],
 fini:["Docker 26.1.5 et Compose 2.26.1 installés","nc, dig, tcpdump présents sur l'hôte","Les 5 images de base téléchargées, projet6/outils construite","Le pare-feu MGMT est intact"],
 livrer:"Mettre la version exacte de Docker dans <code>docs/versions.md</code>. Message au rôle 2 : « hôte Docker prêt, nsenter disponible pour test_flux »."
},
/* 2 */ {titre:"Créer les réseaux macvlan de l'état A", quand:"15–16 nov.", fait:true,
 maj:`<p>Fait par le rôle 1, avec des noms différents : <code>p6_lan</code> (parent <code>ens33.10</code>), <code>p6_dmz</code> (<code>ens33.20</code>), <code>p6_wan</code> (<code>ens34</code>), créés par le <code>docker-compose.yml</code> du projet <code>p6</code>. Vérifier : <code>docker network ls</code> et <code>docker network inspect p6_dmz</code>.</p>`,
 but:"Trois réseaux Docker : lan (VLAN 10), dmz (VLAN 20) et wan (eth2), avec les passerelles d'OPNsense.",
 pourquoi:"Section 4.1 (adresses, VLAN, passerelles) et section 6 (macvlan par VLAN). macvlan donne à chaque conteneur sa propre adresse : sans cela, OPNsense ne verrait que l'adresse de l'hôte et la matrice serait inapplicable (module 8).",
 prereq:{etapes:["services-1","parefeu-3"], modules:[3,8,2]},
 schema:"macvlan", schemaSt:"A",
 config:[{t:"Script", file:"services/reseaux-A.sh", code:NETA}],
 tester:[
  {cmd:"ip -d link show ens33.10 | head -3\ndocker run --rm --network lan --ip 10.10.0.50 projet6/outils ping -c 2 10.10.0.1", sortie:"5: ens33.10@ens33: <BROADCAST,MULTICAST,UP,LOWER_UP> …\n    vlan protocol 802.1Q id 10 <REORDER_HDR>\n64 bytes from 10.10.0.1: icmp_seq=1 ttl=64 time=0.41 ms", sens:[["ens33.10@ens33","sous-interface VLAN créée par Docker"],["id 10","elle étiquette en VLAN 10"],["64 bytes from 10.10.0.1","la passerelle OPNsense répond (FA4 l'autorise en état A)"]]}
 ],
 pannes:[
  ["« invalid subinterface vlan name »","Nom de carte faux","Utiliser le vrai nom (<code>ip -br link</code>), par exemple ens33.10."],
  ["Pas de réponse de 10.10.0.1","Sur OPNsense : <code>tcpdump -nei em1 vlan 10</code> voit-il l'ARP ?","Rien vu : carte du trunk sur le mauvais segment VMware (rôle 1). Vu sans réponse : VLAN 10 non activé (rôle 2)."],
  ["Le réseau existe déjà","<code>docker network ls</code>","<code>docker network rm lan dmz</code> puis relancer le script."]
 ],
 fini:["Réseaux lan, dmz, wan créés","Un conteneur de test joint 10.10.0.1 et 10.20.0.1"],
 livrer:"Commit de <code>services/reseaux-A.sh</code>. Message au rôle 2 : « vous pouvez tester vos VLAN avec un conteneur »."
},
/* 3 */ {titre:"Déployer PostgreSQL et l'API Flask", quand:"17 nov.", fait:true,
 maj:`<p>Fait par le rôle 1 : PostgreSQL 17 (10.10.0.70, base <code>catalogue</code>, rôle <code>api_catalogue</code>, <code>pg_hba</code> limité à 10.20.0.70 et 10.10.0.100) et l'API Flask + gunicorn (10.20.0.70:5000, <code>/api/produit</code> volontairement injectable). Mots de passe aléatoires dans <code>.env</code> (droits 600, jamais affichés).</p>`,
 but:"La base « catalogue » sur 10.10.0.70 et l'API sur 10.20.0.70:5000, avec un point d'entrée sûr et un point d'entrée volontairement vulnérable.",
 pourquoi:"Section 7.5 : API « catalogue » avec un point d'entrée vulnérable à l'injection SQL pour S-03 (laboratoire uniquement) ; PostgreSQL seule cible de F04. Figure 5 (<code>/api/produits</code>) et section 10.1 (<code>/api/produit?id=</code>) : deux chemins (écart E17).",
 prereq:{etapes:["services-2"], modules:[4,9]},
 schema:"chaine-app", schemaSt:"A",
 config:[
  {t:"Création de la base", file:"services/postgres/init.sh", code:INIT},
  {t:"API", file:"services/api/app.py", code:APIPY},
  {t:"Image de l'API", file:"services/api/Dockerfile", code:APIDF},
  {t:"Fichier de lancement complet (utilisé jusqu'à l'étape 6)", file:"services/compose.yml", code:COMPOSE},
  {t:"Choix des réseaux par état", file:"services/etat-A.env, etat-B.env", code:ENVS},
  {t:"Construire l'image de l'API (carte NAT branchée le temps du téléchargement), puis lancer la base et l'API", code:`cd /opt/projet6/services
docker compose --env-file etat-A.env build api          # pip télécharge Flask et psycopg : carte NAT branchée
# Rôle 1 : débrancher la carte NAT, puis :
docker compose --env-file etat-A.env up -d postgres api`}
 ],
 tester:[
  {t:"Dans le conteneur de l'API (la requête part de l'API vers la base, à travers FA3)", cmd:"docker exec api python -c \"import urllib.request;print(urllib.request.urlopen('http://127.0.0.1:5000/api/produits').read())\"", sortie:"b'[{\"id\":1,\"nom\":\"Routeur\",\"prix\":120.0},{\"id\":2,\"nom\":\"Commutateur\",\"prix\":80.0},{\"id\":3,\"nom\":\"C\\\\u00e2ble RJ45\",\"prix\":4.5}]'", sens:[["trois produits, pas de « secret »","l'API joint la base (FA3 l'autorise en état A) et le point d'entrée sûr ne divulgue rien"]]},
  {t:"Vérifier la vulnérabilité (labo uniquement)", cmd:"docker exec api python -c \"import urllib.request,urllib.parse;q=urllib.parse.quote(\\\"1' OR '1'='1\\\");print(urllib.request.urlopen('http://127.0.0.1:5000/api/produit?id='+q).read())\"", sortie:"b'[{\"id\":1,…,\"secret\":\"remise-interne-15\"},{\"id\":2,…},{\"id\":3,…}]'", sens:[["trois lignes avec « secret »","l'injection fonctionne : c'est l'effet que le rôle 5 cherchera dans S-03"]]}
 ],
 pannes:[
  ["L'API répond 500 « connection timeout »","<code>docker exec api python -c \"import socket;socket.create_connection(('10.10.0.70',5432),3)\"</code>","En état A, vérifier FA3 (rôle 2) ; en état B, F04."],
  ["« password authentication failed for user app »","Mot de passe différent entre .env et init.sh","Recréer la base : <code>docker compose down -v</code> (efface le volume) puis <code>up</code>."],
  ["init.sh ne s'exécute pas","Le volume de données existe déjà","Les scripts d'initialisation ne tournent qu'à la création : supprimer le volume."]
 ],
 fini:["postgres sur 10.10.0.70, base catalogue avec 3 produits","api sur 10.20.0.70:5000 ; /api/produits sans « secret »","/api/produit?id=… vulnérable, vérifié une fois"],
 livrer:"Commit de <code>services/</code> (sans les mots de passe : <code>.env.secrets</code> non versionné). Message aux rôles 3 et 5 : « API prête : /api/produits (sûr), /api/produit?id= (vulnérable, S-03) »."
},
/* 4 */ {titre:"Configurer nginx : TLS et reverse proxy", quand:"17 nov.", fait:true,
 maj:`<p>Fait par le rôle 1 : nginx 10.20.0.10, HTTP et HTTPS (certificat auto-signé), relais <code>/api/</code> vers l'API. Testé par FA1 depuis client-ext à travers 203.0.113.1.</p>`,
 but:"nginx sur 10.20.0.10 sert HTTPS (certificat auto-signé), redirige HTTP vers HTTPS et relaie <code>/api/</code> vers l'API en HTTP clair.",
 pourquoi:"Section 7.5 : nginx (DMZ-WEB) : TLS et reverse proxy vers l'API. Section 11.3 : le TLS s'arrête à nginx, l'injection est détectée sur le saut nginx → API, en clair (F03).",
 prereq:{etapes:["services-3"], modules:[4,7]},
 schema:"chaine-app",
 config:[
  {t:"Certificat auto-signé (sur l'hôte Docker)", code:`mkdir -p /opt/projet6/services/nginx/tls && cd /opt/projet6/services/nginx/tls
openssl req -x509 -newkey rsa:2048 -nodes -days 60 \\
  -keyout site.key -out site.crt \\
  -subj "/CN=www.labo.test" -addext "subjectAltName=DNS:www.labo.test,IP:203.0.113.1,IP:10.20.0.10"
#   -nodes : clé sans mot de passe (nginx démarre sans intervention)
#   subjectAltName : le certificat vaut pour le nom et les deux adresses (publique et interne)`},
  {t:"Configuration", file:"services/nginx/site.conf", code:NGINX},
  {t:"Lancer", code:`docker compose --env-file etat-A.env up -d nginx iperf3`}
 ],
 tester:[
  {t:"Depuis Kali (WAN), après les règles FA du rôle 2", cmd:"curl -sk https://203.0.113.1/api/produits | head -c 120; echo\ncurl -sI http://203.0.113.1 | head -2", sortie:"[{\"id\":1,\"nom\":\"Routeur\",\"prix\":120.0},{\"id\":2,…\nHTTP/1.1 301 Moved Permanently\nLocation: https://203.0.113.1/", sens:[["[{\"id\":1 …","toute la chaîne fonctionne : FA1 + NAT, nginx, API, FA3, PostgreSQL"],["301","le port 80 renvoie vers HTTPS"],["-k","accepte le certificat auto-signé"]]},
  {t:"Journal d'accès (lu par le rôle 6, sur l'hôte Docker)", cmd:"tail -n 1 /srv/logs/nginx/projet6.log", sortie:"203.0.113.66 - - [17/Nov/2026:16:02:11 +0000] \"GET /api/produits HTTP/1.1\" 200 112 \"-\" \"curl/8.x\"", sens:[["203.0.113.66","nginx voit la vraie adresse du client : pas de NAT de source"],["200 112","code et taille de la réponse : preuve d'effet réel"]]}
 ],
 pannes:[
  ["502 Bad Gateway","nginx ne joint pas 10.20.0.70:5000","État A : vérifier que l'API tourne. État B : F03 (rôle 2)."],
  ["Le journal d'accès est vide","<code>ls -l /srv/logs/nginx/</code> ; la directive access_log pointe-t-elle vers projet6.log ?","Dans l'image officielle, access.log est un lien vers la sortie standard : écrire dans projet6.log (fichier ci-dessus) et monter /srv/logs/nginx."],
  ["« SSL: certificate verify failed »","Client sans -k","Normal avec un certificat auto-signé ; la sonde utilise <code>verify=False</code>."]
 ],
 fini:["HTTPS répond sur 10.20.0.10 et via 203.0.113.1","HTTP renvoie vers HTTPS","/api/ relayé vers l'API","Le journal d'accès est lisible"],
 livrer:"Message au rôle 6 : le journal d'accès nginx est <code>/srv/logs/nginx/projet6.log</code> sur l'hôte Docker, format combined. Message au rôle 3 : « URL de la sonde : https://203.0.113.1/api/produits (externe), https://10.20.0.10/api/produits (interne) »."
},
/* 5 */ {titre:"Configurer bind9", quand:"18 nov.", fait:true,
 maj:`<p>Fait par le rôle 1 : bind9 10.20.0.40, zone <code>p6.lab</code> faisant autorité, sans récursion. Testé par FA2 (réponse exacte vérifiée).</p>`,
 but:"bind9 sur 10.20.0.40 fait autorité sur <code>labo.test</code> : réponse interne (10.20.0.10) pour le LAN et la DMZ, publique (203.0.113.1) pour le WAN.",
 pourquoi:"Section 7.5 : bind9 (DMZ-DNS) : zone publique et résolution interne. Flux FA2 / F02 (WAN, UDP/TCP 53) et F06 (LAN-USERS, UDP 53). Sonde DNS toutes les 5 s (section 7.8).",
 prereq:{etapes:["services-2"], modules:[1,4]},
 config:[
  {t:"Configuration et zone", file:"services/bind9/", verif:"chemins et utilisateur de l'image ISC bind9", code:BIND},
  {t:"Lancer et vérifier la syntaxe", code:`docker compose --env-file etat-A.env up -d bind9
docker exec bind9 named-checkconf /etc/bind/named.conf
docker exec bind9 named-checkzone labo.test /etc/bind/db.labo.test.interne`}
 ],
 tester:[
  {cmd:"docker compose --env-file etat-A.env up -d client-01 client-ext\ndocker exec client-01  dig +short @10.20.0.40 www.labo.test\ndocker exec client-ext dig +short @203.0.113.1 www.labo.test", sortie:"10.20.0.10\n203.0.113.1", sens:[["10.20.0.10","vue interne : le LAN joint nginx directement (F05 en état B)"],["203.0.113.1","vue externe, via la redirection FA2 / F02"]]}
 ],
 pannes:[
  ["« connection timed out; no servers could be reached »","bind9 tourne-t-il ? <code>docker logs bind9</code>","Erreur de syntaxe dans named.conf ou la zone : <code>named-checkconf</code>."],
  ["client-ext reçoit 10.20.0.10","Mauvaise vue choisie","Vérifier <code>match-clients</code> : 203.0.113.0/24 ne doit pas être dans la vue interne."],
  ["Le DNS marche en A mais pas en B pour client-01","F06 en TCP au lieu d'UDP","Rôle 2 (écart E16)."]
 ],
 fini:["named-checkconf et named-checkzone sans erreur","Réponse interne pour le LAN, publique pour le WAN"],
 livrer:"Commit de <code>services/bind9/</code>. Message au rôle 3 : « sonde DNS : dig @10.20.0.40 www.labo.test »."
},
/* 6 */ {titre:"Lancer les postes : clients, dmz-pivot, admin, client-ext, iperf3", quand:"18 nov.", fait:true,
 maj:`<p>Fait par le rôle 1 : client-01 (10.10.0.10), client-03 (10.10.0.12), admin (10.10.0.100), dmz-pivot (10.20.0.20), client-ext (203.0.113.10), iperf3 (partage la pile réseau de nginx, port 5201). Image des postes : <code>p6/outils:1</code>. Les 10 conteneurs redémarrent seuls après un redémarrage de la VM.</p>`,
 but:"Tous les conteneurs de hosts.csv en marche à leur adresse, avec les outils dont ont besoin la sonde et les scénarios.",
 pourquoi:"Section 7.7 : client-03 (S-05) et dmz-pivot (S-04) sont des positions d'attaque internes. Section 7.8 : client-ext (conteneur de l'hôte Docker relié au WAN, distinct de Kali) et client-01 portent la sonde. FA6 / F09 : serveur iperf3 sur 10.20.0.10:5201.",
 prereq:{etapes:["services-3","services-4","services-5"], modules:[2,8]},
 schema:"macvlan-addr",
 config:[{t:"Tout lancer", code:`cd /opt/projet6/services
docker compose --env-file etat-A.env up -d
docker compose ps --format 'table {{.Name}}\\t{{.State}}'
for c in nginx dmz-pivot bind9 api client-01 client-03 postgres admin client-ext; do
  printf "%-11s %s\\n" "$c" "$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $c)"
done`, note:"iperf3 n'a pas d'adresse propre : il partage l'espace réseau de nginx (10.20.0.10)."}],
 tester:[
  {cmd:"(sortie de la boucle ci-dessus)", sortie:"nginx       10.20.0.10\ndmz-pivot   10.20.0.20\nbind9       10.20.0.40\napi         10.20.0.70\nclient-01   10.10.0.10\nclient-03   10.10.0.12\npostgres    10.10.0.70\nadmin       10.10.0.100\nclient-ext  203.0.113.10", sens:[["chaque ligne","doit correspondre exactement à hosts.csv (page Contrats)"]]},
  {t:"Les outils des scénarios sont là", cmd:"docker exec client-03 sh -c 'nmap --version | head -1; psql --version'", sortie:"Nmap version 7.9x …\npsql (PostgreSQL) 17.x", sens:[["Nmap, psql","S-05 pourra s'exécuter dans client-03"]]}
 ],
 pannes:[
  ["« Address already in use »","Un conteneur de test a gardé l'adresse","<code>docker ps -a</code>, supprimer le conteneur de test."],
  ["client-ext ne joint pas 203.0.113.1","Réseau wan sur la mauvaise carte","Le parent doit être la carte reliée à p6-wan (ens34)."],
  ["iperf3 ne démarre pas","nginx arrêté (espace réseau partagé)","Démarrer nginx d'abord."]
 ],
 fini:["Les 9 conteneurs de hosts.csv tournent à leur adresse","iperf3 écoute sur 10.20.0.10:5201","test_flux du rôle 2 peut s'exécuter"],
 livrer:"Message aux rôles 2, 3 et 5 : « tous les postes sont en place ». Fichier <code>docs/acces-attaquants.md</code> : les scénarios internes s'exécutent par <code>ssh root@10.99.0.20 docker exec client-03 …</code> et <code>… dmz-pivot …</code>."
},
/* 7 */ {titre:"Écrire l'outil de capture C10", quand:"18–19 nov.",
 but:"Un script que l'orchestrateur appelle par SSH pour démarrer et arrêter une capture dans l'espace réseau d'un conteneur cible.",
 pourquoi:"Section 7.10 : un tcpdump par exécution, lancé dans l'espace réseau du conteneur cible ; le trafic entre deux conteneurs d'un même VLAN macvlan ne passe pas par l'interface parente. Les PCAP prouvent le blocage (section 9.4).",
 prereq:{etapes:["services-6"], texte:"Nommage des fichiers : page <a href='#contrat-arbo'>Contrats</a>.", modules:[10,3]},
 schema:"macvlan", schemaSt:"A",
 config:[{t:"Script", file:"capture/capture.sh (installé dans /opt/projet6/capture/)", code:CAPT}],
 tester:[
  {cmd:"/opt/projet6/capture/capture.sh start TEST-S05-01 postgres\ndocker exec client-03 nc -z -w 2 10.10.0.70 5432\n/opt/projet6/capture/capture.sh stop TEST-S05-01 postgres\ntcpdump -nr /srv/captures/TEST-S05-01_postgres.pcap 'tcp[tcpflags] & tcp-syn != 0' | head -2", sortie:"-rw-r--r-- 1 root root 1024 … /srv/captures/TEST-S05-01_postgres.pcap\n16:20:01.102 IP 10.10.0.12.43120 > 10.10.0.70.5432: Flags [S], …\n16:20:01.102 IP 10.10.0.70.5432 > 10.10.0.12.43120: Flags [S.], …", sens:[["Flags [S] puis [S.]","SYN de client-03 puis SYN-ACK de la base : en état A, la connexion aboutit et seule cette capture le prouve"]]}
 ],
 pannes:[
  ["Fichier vide ou absent","<code>cat /srv/captures/.&lt;run_id&gt;-&lt;point&gt;.err</code>","Message de tcpdump (droits, interface). Arrêt par KILL : toujours <code>kill -INT</code>, tcpdump vide son tampon."],
  ["« nsenter: cannot open … »","Lancé sans root","L'orchestrateur se connecte en root (clé du collecteur)."],
  ["Capture sur eth1 vide en état A","Normal : trafic interne au macvlan","Capturer dans le conteneur (ce script)."]
 ],
 fini:["start / stop fonctionnent pour nginx, api, postgres, bind9","Le PCAP de test montre le SYN de client-03"],
 livrer:"Commit de <code>capture/capture.sh</code>. Message aux rôles 5 et 6 : « capture.sh prêt ; fichiers dans /srv/captures/&lt;run_id&gt;_&lt;point&gt;.pcap, à rapatrier dans resultats/&lt;etat&gt;/&lt;date&gt;/pcap/ »."
},
/* 8 */ {titre:"Basculer les services en état B", quand:"22–23 nov.",
 maj:`<p>Adapter les commandes à l'état A réel : arrêter d'abord l'état A avec <code>docker compose -p p6 down</code> (dans <code>~/p6-etat-a</code>), ce qui supprime <code>p6_lan</code>, <code>p6_dmz</code> et <code>p6_wan</code> ; les parents des réseaux de l'état B sont <code>ens33.11</code>…<code>ens33.23</code> et <code>ens34</code>. Prendre un instantané de <code>p6-docker</code> avant (panneau, « Prendre un instantané »).</p>`,
 but:"Mêmes conteneurs, mêmes adresses, sur les six réseaux /26 et /27 de l'état B.",
 pourquoi:"Section 5.1 : les hôtes gardent leur adresse, seuls le masque et la passerelle changent. Section 6 : on ne change que des sous-interfaces et des règles, jamais le câblage.",
 prereq:{etapes:["parefeu-8","chef-7"], texte:"etat-A doit être figé (snapshots) avant la bascule.", modules:[2,3]},
 schema:"macvlan", schemaSt:"B",
 config:[
  {t:"Script", file:"services/reseaux-B.sh", code:NETB},
  {t:"Bascule", code:`cd /opt/projet6/services
docker compose --env-file etat-A.env down          # arrête les conteneurs ; la base est dans un volume anonyme créé par l'image postgres, conservé (seul « down -v » l'efface)
docker network rm lan dmz                          # supprime aussi les sous-interfaces ens33.10 et ens33.20
./reseaux-B.sh
docker compose --env-file etat-B.env up -d`}
 ],
 tester:[
  {cmd:"docker exec postgres sh -c 'ip -br addr show eth0; ip route'\ndocker exec client-03 ip route", sortie:"eth0  UP  10.10.0.70/27\ndefault via 10.10.0.65 dev eth0\n10.10.0.64/27 dev eth0 proto kernel scope link src 10.10.0.70\ndefault via 10.10.0.1 dev eth0\n10.10.0.0/26 dev eth0 proto kernel scope link src 10.10.0.12", sens:[["10.10.0.70/27 · via 10.10.0.65","PostgreSQL dans LAN-DB, passerelle de LAN-DB (section 5.1)"],["10.10.0.0/26","client-03 ne voit plus la base comme voisine : S-05 passera par pf"]]},
  {t:"Chaîne complète en état B", cmd:"docker exec client-ext curl -sk https://203.0.113.1/api/produits | head -c 60", sortie:"[{\"id\":1,\"nom\":\"Routeur\",\"prix\":120.0},…", sens:[["réponse JSON","F01, F03 et F04 sont en place (rôle 2)"]]}
 ],
 pannes:[
  ["« network dmz-web not found »","reseaux-B.sh non exécuté","Le lancer avant <code>up</code>."],
  ["PostgreSQL injoignable depuis l'API","Passerelle de LAN-DB","Vérifier <code>--gateway 10.10.0.65</code> ; sinon voir module 2, exercice 2."],
  ["Les données de la base ont disparu","<code>down -v</code> utilisé par erreur","Relancer : init.sh recrée la base à l'identique."]
 ],
 fini:["Six réseaux B créés, réseaux A supprimés","Les 9 conteneurs à leur adresse, masques et passerelles de la section 5.1","La chaîne client-ext → nginx → API → base fonctionne"],
 livrer:"Commit de <code>services/reseaux-B.sh</code>. Message au rôle 2 : « services en état B, vous pouvez lancer test_flux »."
}
],
demo:{intro:"Ce que vous montrez en 2 minutes : les services sont de vraies machines de leur segment.", etapes:["<code>docker network ls</code> et <code>ip -d link show ens33.12</code> : un réseau macvlan par VLAN.","<code>docker exec postgres ip route</code> en état B : /27 et passerelle 10.10.0.65, même adresse qu'en A.","<code>curl -k https://203.0.113.1/api/produits</code> depuis client-ext : la chaîne complète.","Le PCAP d'une exécution capturé par capture.sh."], secours:"captures des sorties dans le rapport."},
questions:[
 ["Pourquoi macvlan et pas le réseau Docker par défaut ?","Le réseau par défaut cache les conteneurs derrière l'adresse de l'hôte (NAT) : OPNsense ne verrait qu'une source et ne pourrait pas appliquer F03 ou F04. Avec macvlan, chaque conteneur a sa MAC et son adresse dans son VLAN : c'est une machine du segment."],
 ["Pourquoi l'API est-elle volontairement vulnérable ?","Pour que S-03 ait un effet mesurable : en état A, l'injection renvoie la colonne « secret » ; en état B, Suricata doit la bloquer sur le saut nginx → API. Le point d'entrée n'existe que dans le laboratoire isolé ; /api/produits montre la bonne pratique."],
 ["Pourquoi la capture se fait-elle dans le conteneur ?","Deux conteneurs d'un même réseau macvlan se parlent dans le noyau de l'hôte, sans passer par eth1 : une capture sur l'hôte ou sur OPNsense ne voit pas S-05 en état A. Seule la capture dans l'espace réseau de la cible prouve l'effet réel (section 7.10)."],
 ["Qu'est-ce qui change pour un conteneur entre A et B ?","Seulement le réseau Docker : masque, passerelle et VLAN. L'adresse, l'image et la configuration restent identiques ; c'est ce qui rend la comparaison A/B honnête (ENF3)."],
 ["Pourquoi un serveur iperf3 dans l'espace réseau de nginx ?","FA6 et F09 ouvrent 10.20.0.10:5201. Partager l'espace réseau de nginx donne cette adresse au serveur iperf3 sans créer d'hôte supplémentaire ; la mesure de débit traverse exactement le même chemin que le trafic web."]
]
};
})();
