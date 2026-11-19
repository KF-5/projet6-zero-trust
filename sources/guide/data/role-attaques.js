/* Rôle 5 : validation et attaques (C6, C7). */
window.G = window.G || {}; G.roles = G.roles || {};
(function(){
const YAML = `# campagne/scenarios.yaml — scénarios de la section 10.1, rejoués à l'identique en A et en B
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
      - {a_s: 40, commande: "configctl ids start"}`;
const ORCH = `#!/usr/bin/env python3
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
    main()`;

G.roles.attaques = {
intro:`<div class="callout danger" style="border:0;padding:0"><p><b>Confinement.</b> Toutes les commandes de ce rôle visent uniquement les adresses du laboratoire isolé (203.0.113.0/24, 10.10.0.0/24, 10.20.0.0/24). Avant chaque campagne, le rôle 1 vérifie que les cartes NAT des VM sont débranchées et relance <code>tests-cloisonnement.sh</code> depuis Kali (19/19 au 17 novembre) : aucune VM n'a de route vers l'extérieur (ENF1). N'exécutez jamais ces outils hors du laboratoire.</p></div><p>Vous êtes le seul à « savoir » quand une attaque a lieu : <code>runs.csv</code> est la vérité terrain dont dépendent tous les indicateurs (section 8.4).</p><p><b>Mise à jour du 17 novembre.</b> Kali tourne déjà : <code>p6-kali</code>, 203.0.113.66 sur le WAN (eth0, passerelle 203.0.113.1) et 10.99.0.66 sur MGMT (eth1), sans bureau. Les outils nmap, hping3, nikto, curl, psql, dig, nc et tcpdump sont installés. Console : TightVNC sur <code>127.0.0.1::5904</code> par le tunnel.</p><p><b>Comptes et SSH réels.</b> Les VM Debian et Kali ont l'utilisateur <code>karim</code> avec <code>sudo</code>, pas de connexion <code>root</code>. SSH n'est accepté sur les VM que depuis Windows (10.99.0.2) et le collecteur (10.99.0.10) ; le SSH d'OPNsense est désactivé. Avant que l'orchestrateur tourne depuis le collecteur, le rôle 1 doit : installer la clé du collecteur sur p6-docker et p6-kali (pour <code>root</code>, ou pour <code>karim</code> avec un <code>sudo</code> sans mot de passe limité aux commandes utiles), et réactiver le SSH d'OPNsense sur MGMT, par clé seulement (la règle « Admin du pare-feu » autorise déjà le port 22 depuis 10.99.0.10).</p>`,
steps:[
/* 1 */ {titre:"Préparer Kali sans interface graphique", quand:"14–15 nov.", fait:"en partie fait",
 maj:`<p>En grande partie fait par le rôle 1 (script <code>setup-kali.sh</code>) : adresses, outils, heure prise sur 10.99.0.10 (avec <code>systemctl enable --now chrony</code> : Kali désactive les services au démarrage), SSH qui n'écoute que sur 10.99.0.66, démarrage en mode texte. Le filtre SSH de Kali est la table <code>inet p6_ssh</code> (SSH accepté depuis 10.99.0.2 et 10.99.0.10). Reste à faire : la clé de l'orchestrateur (voir l'encadré du rôle) et tes propres outils Python.</p>`,
 but:"Kali 2026.3 avec nmap, hping3, nikto, curl et tcpdump, pilotable en root par le collecteur, sans aucune route vers l'extérieur.",
 pourquoi:"Section 6 : Kali sans interface graphique, eth0 WAN, eth1 MGMT ; section 7.7 : positions d'attaque. ENF1 : aucun trafic d'attaque ne sort du laboratoire.",
 prereq:{etapes:["chef-4","chef-5"], modules:[9,1]},
 schema:"proxmox-topo",
 config:[
  {t:"Outils (pendant une fenêtre de configuration)", code:`apt update
apt install -y nmap hping3 nikto curl tcpdump openssh-server
systemctl enable --now ssh
# Clé publique du collecteur pour root (connexion par clé uniquement)
mkdir -p /root/.ssh && chmod 700 /root/.ssh
echo "ssh-ed25519 AAAA… collecteur" >> /root/.ssh/authorized_keys`},
  {t:"Contrôle du confinement, à refaire avant chaque campagne", code:`ip route                         # attendu : seulement 203.0.113.0/24 et 10.99.0.0/24, AUCUNE ligne « default »
ping -c 1 -W 1 1.1.1.1           # attendu : « Network is unreachable »
nmap --version | head -1; hping3 --version | head -1; nikto -Version | head -2`}
 ],
 tester:[{t:"Depuis le collecteur", cmd:"ssh -o BatchMode=yes root@10.99.0.66 'nmap -sn -Pn 203.0.113.1 | tail -1'", sortie:"Nmap done: 1 IP address (1 host up) scanned in 0.05 seconds", sens:[["BatchMode=yes","aucune question interactive : c'est ainsi que l'orchestrateur se connectera"],["1 host up","Kali voit OPNsense sur le WAN simulé"]]}],
 pannes:[["« Permission denied (publickey) »","Clé absente de /root/.ssh/authorized_keys","Ajouter la clé du collecteur ; vérifier <code>PermitRootLogin prohibit-password</code>."],["Une route par défaut existe","<code>ip route</code>","La supprimer (carte de configuration encore active) : <code>ip route del default</code>, et prévenir le rôle 1."],["nmap -sS : « requires root »","Connexion avec un utilisateur normal","L'orchestrateur se connecte en root."]],
 fini:["Outils installés, versions notées dans docs/versions.md","SSH en root par clé depuis 10.99.0.10","Aucune route par défaut sur Kali"],
 livrer:"Message au rôle 1 : « Kali prête ; contrôle de confinement OK »."
},
/* 2 */ {titre:"Écrire scenarios.yaml", quand:"15–16 nov.",
 but:"Le fichier qui décrit les 6 scénarios et P-01 : machine, commande exacte, cible, durée maximale, critère « objectif atteint », points de capture.",
 pourquoi:"Section 10.1 (scénarios) et section 9.4 : le blocage se juge sur l'effet réel, donc chaque scénario doit dire ce qu'est « objectif atteint ». Corrections appliquées : commande curl de S-03 (écart E10), timeout de hping3.",
 prereq:{etapes:["attaques-1","services-3"], texte:"Format : page <a href='#contrat-scenarios'>Contrats</a>.", modules:[9]},
 schema:"comparateur-ab",
 config:[{t:"Fichier complet", file:"campagne/scenarios.yaml", code:YAML}],
 tester:[{t:"Sur le collecteur", cmd:"python3 -c \"import yaml;d=yaml.safe_load(open('campagne/scenarios.yaml'));print(len(d['scenarios']),[s['id'] for s in d['scenarios']])\"", sortie:"7 ['S-01', 'S-02', 'S-03', 'S-04', 'S-05', 'S-06', 'P-01']", sens:[["7","six scénarios et la panne"]]}],
 pannes:[["« mapping values are not allowed here »","Deux-points non protégé dans une commande","Mettre la commande en bloc <code>|</code> (comme S-03) ou entre guillemets."],["S-03 : curl renvoie « URL using bad/illegal format »","Commande du rapport sans encodage","Utiliser <code>-G --data-urlencode</code> (écart E10)."],["psql demande un mot de passe","PGPASSWORD absent du conteneur","Il est fourni par compose.yml (rôle 4) pour dmz-pivot et client-03."]],
 fini:["7 entrées, chacune avec un critère objectif_atteint vérifiable","Commandes testées une fois à la main (état A)"],
 livrer:"Commit de <code>campagne/scenarios.yaml</code>. Message au rôle 6 : « critères objectif_atteint disponibles pour le calcul du blocage »."
},
/* 3 */ {titre:"Écrire l'orchestrateur C6", quand:"16–18 nov.",
 but:"Un programme qui rejoue les scénarios dans un ordre aléatoire reproductible, avec capture, t0, t1, sortie, code de retour, réinitialisation et pause.",
 pourquoi:"Section 7.6 et figure 8 : démarrer la capture, noter t0, lancer par SSH, noter t1, écrire la vérité terrain, arrêter la capture, réinitialiser (pfctl -k, levée des blocages), attendre 30 s. Section 10.3 : même graine en A et en B.",
 prereq:{etapes:["attaques-2","services-7","parefeu-2"], texte:"Le shell de connexion de root sur OPNsense doit exécuter une commande passée par SSH (System › Access › Users › root, « Login shell » : /bin/sh) <span class='verif'>à vérifier sur votre version</span>.", modules:[10,11]},
 schema:"campagne-chrono",
 config:[{t:"Programme complet", file:"campagne/orchestrateur.py", code:ORCH}],
 tester:[{t:"Simulation (n'exécute rien)", cmd:"python3 campagne/orchestrateur.py --etat A --scenario S-05 --n 1 --simulation", sortie:"A-S05-01 · Mouvement latéral du LAN vers la base\n    [simulation] ssh root@10.99.0.20 /opt/projet6/capture/capture.sh start A-S05-01 postgres\n    [simulation] ssh root@10.99.0.20 docker exec client-03 sh -c 'nmap -sT …'\n    [simulation] ssh root@10.99.0.20 /opt/projet6/capture/capture.sh stop A-S05-01 postgres\n    [simulation] scp root@10.99.0.20:/srv/captures/A-S05-01_postgres.pcap resultats/A/simulation/pcap/\n    [simulation] ssh root@10.99.0.1 pfctl -k 10.10.0.12\nTerminé : resultats/A/simulation/runs.csv", sens:[["ordre des lignes","capture, attaque, arrêt, rapatriement, réinitialisation : exactement la figure 8"],["pfctl -k 10.10.0.12","les états de l'attaquant sont supprimés après chaque exécution"]]}],
 pannes:[["« Host key verification failed »","Première connexion SSH","Se connecter une fois à la main à 10.99.0.1, .20 et .66 pour accepter les clés."],["pfctl -k ne fait rien","Le shell de root sur OPNsense est le menu console","Choisir /bin/sh comme shell de connexion de root (à vérifier sur votre version)."],["Le PCAP n'est pas rapatrié","Point de capture inconnu de capture.sh","Points valides : kali-wan, nginx, api, postgres, bind9."]],
 fini:["La simulation affiche la séquence complète pour chaque scénario","Une exécution réelle de S-05 produit runs.csv, sorties/…txt et pcap/…pcap"],
 livrer:"Commit de <code>campagne/orchestrateur.py</code>. Message au rôle 6 : « exemple de runs.csv réel joint »."
},
/* 4 */ {titre:"Répétition à blanc", quand:"19 nov.",
 but:"Une exécution réelle de chaque scénario en état A, pour valider les commandes, les critères et toute la chaîne de mesure avant la campagne.",
 pourquoi:"Jalon du 19 novembre (section 12) : test_flux conforme ; une exécution S-01 produit runs.csv + events.csv. C'est la preuve que les rôles 2, 3, 4 et 6 sont prêts.",
 prereq:{etapes:["parefeu-7","services-7","ids-3","analyse-3"], modules:[9,11]},
 schema:"comparateur-ab", schemaSt:"A",
 config:[{t:"Sur le collecteur", code:`python3 campagne/orchestrateur.py --etat A --n 1 --dossier resultats/A/essai     # 6 exécutions, environ 6 minutes
ls resultats/A/essai/
cat resultats/A/essai/runs.csv
python3 analyse/parse.py --dossier resultats/A/essai                                # rôle 6 : produit events.csv`}],
 tester:[{cmd:"grep -o '/open/' resultats/A/essai/sorties/A-S01-01.txt | wc -l\ngrep -c secret resultats/A/essai/sorties/A-S03-01.txt\ntail -3 resultats/A/essai/runs.csv", sortie:"3\n1\nA-S04-01,S-04,A,1,2026-11-19T10:12:40.004Z,2026-11-19T10:12:47.210Z,10.20.0.20,10.10.0.70,0\nA-S01-01,S-01,A,1,…,203.0.113.66,203.0.113.1,0\n…", sens:[["3","trois occurrences de « /open/ » (grep -o compte chaque port, pas chaque ligne : nmap -oG met tous les ports sur une ligne) : 53, 80, 443 découverts, objectif atteint"],["secret","S-03 en A : l'injection a réussi"],["code 0 pour S-04","psql connecté : FA3 laisse passer dmz-pivot"]]}],
 pannes:[["S-02 renvoie 124","Normal : timeout arrête hping3","Le blocage de S-02 se juge sur sla.csv, pas sur le code."],["Aucune alerte pour aucun scénario en A","Normal : pas d'IDS en état A","Vérifier seulement que filterlog et sla.csv arrivent."],["events.csv vide","Collecte du rôle 6 pas prête","Escalader au rôle 1 : la campagne A ne peut pas partir."]],
 fini:["Les 6 scénarios s'exécutent sans intervention","Chaque objectif est jugé atteint en état A comme prévu (S-06 : port 443 vu ouvert)","runs.csv et events.csv existent pour S-01"],
 livrer:"Message au rôle 1 : « jalon du 19 atteint ». Les fichiers de l'essai restent dans <code>resultats/A/essai/</code> (pas dans les données de campagne)."
},
/* 5 */ {titre:"Conduire la campagne A", quand:"20–21 nov.",
 but:"6 scénarios × 10 exécutions en état A, dans les conditions du protocole, archivés et marqués.",
 pourquoi:"Section 10.3 : restaurer le snapshot et vérifier test_flux ; 2 h de trafic légitime seul ; campagne de 6 × 10 exécutions, ordre aléatoire, pause de 30 s (environ 1 h 30) ; archiver ; même version d'outils et même graine dans les deux états.",
 prereq:{etapes:["parefeu-7","ids-3"], modules:[9,10]},
 schema:"campagne-chrono",
 config:[{t:"Déroulé (collecteur)", code:`# 0. Rôle 1 : instantané etat-A restauré, cartes NAT débranchées (startConnected FALSE), tests-cloisonnement.sh = 19/19
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
git add resultats/A && git commit -m "[rôle 5] Campagne A" && git tag campagne-A`}],
 tester:[{cmd:"wc -l resultats/A/*/runs.csv\nls resultats/A/*/pcap | wc -l", sortie:"61 resultats/A/2026-11-20/runs.csv\n80", sens:[["61","60 exécutions + l'en-tête"],["80","8 points de capture par tour (S-01 : 2, S-02 : 1, S-03 : 2, S-04 : 1, S-05 : 1, S-06 : 1) × 10 exécutions"]]}],
 pannes:[["Une exécution échoue au milieu","Ligne manquante dans runs.csv","Relancer seulement ce scénario avec --scenario ; noter l'incident dans docs/journal-campagnes.md."],["La sonde s'est arrêtée","sla.csv incomplet","Les disponibilités de la période sont invalides : relancer les exécutions concernées."],["Retard sur le planning","—","Réduire N à 5 pour tous les scénarios, jamais pour un seul état (section 11.2)."]],
 fini:["60 lignes dans runs.csv, toutes avec un code de retour","PCAP et sorties présents pour chaque exécution","Tag campagne-A poussé ; rôle 1 peut figer etat-A"],
 livrer:"Message aux rôles 1 et 6 : « campagne A terminée, resultats/A/2026-11-20 » (dossier daté du jour de début)."
},
/* 6 */ {titre:"Conduire la campagne B et la panne P-01", quand:"25–26 nov.",
 but:"La même campagne en état B, puis P-01 répété 5 fois, puis la mesure iperf3 « B sans IPS ».",
 pourquoi:"Section 10.3, étapes 3 et 4 : même protocole qu'en A ; P-01 en état B uniquement, 5 répétitions ; débit iperf3 en B avec Suricata désactivé (référence « B sans IPS »). Section 8.5 : le comportement en panne est mesuré, pas supposé.",
 prereq:{etapes:["parefeu-9","ids-6","ids-7","services-8"], modules:[9,7]},
 schema:"comparateur-ab",
 config:[{t:"Déroulé (collecteur)", code:`# Même préparation qu'en A : snapshot etat-B (ou état validé), test_flux B 0 écart, 2 h de trafic légitime
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
git add resultats/B && git commit -m "[rôle 5] Campagne B et P-01" && git tag campagne-B`}],
 tester:[{cmd:"grep -c ',P-01,' resultats/B/2026-11-25/runs.csv\ngrep -c /open/ resultats/B/2026-11-25/sorties/B-S01-*.txt | head -3", sortie:"5\nresultats/B/…/B-S01-01.txt:0\nresultats/B/…/B-S01-02.txt:0\nresultats/B/…/B-S01-03.txt:1", sens:[["5","les 5 répétitions de P-01"],["0 puis 1","S-01 bloqué dans la plupart des exécutions ; une sonde passée avant le seuil peut révéler un port (écart E06) : c'est un résultat à rapporter, pas à masquer"]]}],
 pannes:[["Tous les scénarios WAN échouent dès la 1re exécution","Suricata bloque Kali durablement","Vérifier le timeout du rate_filter (rôle 3) ; il doit être inférieur à la pause de 30 s."],["P-01 : Suricata ne redémarre pas","Commande d'arrêt / démarrage non valide sur votre version","Tester les deux commandes à la main avec le rôle 3 avant la campagne."],["La sonde est coupée pendant P-01","C'est peut-être le résultat : l'interface ne transmet plus rien","Ne pas corriger : mesurer la durée et le temps de reprise (section 8.5)."]],
 fini:["60 exécutions en B, même ordre qu'en A","5 exécutions de P-01","Tag campagne-B poussé ; rôle 1 peut figer etat-B"],
 livrer:"Message aux rôles 1 et 6 : « campagne B et P-01 terminées, resultats/B/2026-11-25 »."
}
],
demo:{intro:"Ce que vous montrez en 2 minutes : les mesures sont reproductibles et la vérité terrain est fiable.", etapes:["<code>scenarios.yaml</code> : un scénario, sa commande et son critère « objectif atteint ».","Lancer <code>orchestrateur.py --scenario S-05 --n 1</code> en direct (état B) : la séquence s'affiche.","La ligne ajoutée à runs.csv, la sortie de psql (délai dépassé) et le PCAP vide de SYN.","Rappeler : même graine, même N, mêmes commandes en A et en B."], secours:"enregistrement de l'exécution."},
questions:[
 ["Comment évitez-vous qu'une exécution influence la suivante ?","Après chaque exécution : suppression des états pf de l'attaquant (pfctl -k), attente de 30 s pendant laquelle les blocages temporaires de Suricata expirent (timeout du rate_filter inférieur à la pause). L'ordre des scénarios est aléatoire pour ne pas favoriser un scénario par sa position."],
 ["Comment savez-vous qu'une attaque est bloquée ?","Par son effet réel, défini dans scenarios.yaml : ports ouverts dans la sortie de nmap, connexion psql réussie, présence de « secret » dans la réponse, disponibilité de la sonde. On le vérifie aussi dans le PCAP côté cible (section 9.4)."],
 ["Vos scénarios ne favorisent-ils pas l'état B ?","Les commandes, N, la graine, le trafic de fond et les adresses sont identiques. En revanche, les règles locales de Suricata ont été écrites en connaissant les scénarios : les taux valent pour ces attaques (section 11.3)."],
 ["Comment garantissez-vous qu'aucune attaque ne sort du laboratoire ?","Toutes les cibles sont dans 203.0.113.0/24 ou les réseaux internes ; Kali n'a pas de route par défaut ; avant chaque campagne, le rôle 1 vérifie que les cartes de configuration sont débranchées. Les commandes passent par MGMT, mais les attaques partent du WAN ou des segments évalués."],
 ["Pourquoi l'horloge du collecteur pour t0 et t1 ?","C'est la référence NTP du laboratoire : OPNsense et l'hôte Docker se synchronisent sur elle. Le temps de détection (première alerte − t0) compare deux horloges synchronisées."]
]
};
})();
