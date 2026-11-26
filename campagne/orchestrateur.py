#!/usr/bin/env python3
"""C6 — orchestrateur de campagnes (rapport, sections 7.6, 8.4, 10.3 ; figure 10).
Exécute chaque scénario N fois, produit la vérité terrain runs.csv, les sorties et les PCAP.

Déploiement réel (écarts E35/E40) :
  - connexions par alias SSH p6-docker / p6-kali / opnsense (clé ~/.ssh/orchestrateur, ~/.ssh/config)
  - utilisateur karim + sudo -n sur les Debian/Kali ; root seulement sur OPNsense
  - capture via le wrapper « sudo -n p6-capture » ; capture WAN sur Kali (écart E18)
Version figée du rôle 1 (26/11), utilisée à l'identique en A et en B : voir les lignes marquées [rôle 1, 26/11].
Aucune dépendance Python externe (json = bibliothèque standard). Nécessite seulement openssh-client.
"""
import argparse, csv, datetime as dt, os, random, shlex, subprocess, sys, threading, time
import json

SRC_IP = {"kali": "203.0.113.66", "client-03": "10.10.0.12", "dmz-pivot": "10.20.0.20"}   # hosts.csv
SSH = {"kali": "p6-kali", "docker": "p6-docker", "opnsense": "opnsense"}   # alias ~/.ssh/config, clé ~/.ssh/orchestrateur

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
    if sc["machine"] == "kali":                     # sudo -n est déjà dans la commande (nmap/hping3)
        return ssh("kali", f"sh -c {shlex.quote(cmd)}", simul, sc["duree_max_s"])
    return ssh("docker", f"docker exec {sc['machine']} sh -c {shlex.quote(cmd)}", simul, sc["duree_max_s"])

def capture(action, run_id, points, simul):         # C10 (rôle 4) ; le point kali-wan est sur Kali (écart E18)
    for pt in points:
        if pt == "kali-wan":
            if action == "start":
                ssh("kali", f"nohup sudo -n tcpdump -i eth0 -U -Z karim -w /tmp/{run_id}_kali-wan.pcap >/dev/null 2>&1 & sleep 1", simul)
            else:
                ssh("kali", "sudo -n pkill -INT -x tcpdump; sleep 1", simul)
        else:
            ssh("docker", f"sudo -n p6-capture {action} {run_id} {pt}", simul)

def rapatrier(run_id, points, dossier, simul):
    for pt in points:
        src = (f"{SSH['kali']}:/tmp/{run_id}_kali-wan.pcap" if pt == "kali-wan"
               else f"{SSH['docker']}:/srv/captures/{run_id}_{pt}.pcap")
        if simul:
            print(f"    [simulation] scp {src} {dossier}/pcap/")
            continue
        r = subprocess.run(["scp", "-q", "-o", "BatchMode=yes", src, f"{dossier}/pcap/"], check=False)
        if pt == "kali-wan" and r.returncode == 0:   # [rôle 1, 26/11] /tmp de Kali en mémoire (714 Mo) : effacer après copie réussie
            ssh("kali", f"rm -f /tmp/{run_id}_kali-wan.pcap", simul)

def actions_programmees(sc, simul):                 # P-01 : arrêt puis redémarrage de Suricata (état B)
    fils = []
    for a in sc.get("actions", []):
        t = threading.Timer(0 if simul else a["a_s"], ssh, args=("opnsense", a["commande"], simul))
        t.start()
        fils.append(t)
    return fils

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--etat", choices=["A", "B"], required=True)
    ap.add_argument("--fichier", default="campagne/scenarios.json")
    ap.add_argument("--scenario", help="ne jouer que ce scénario, par exemple S-01 ou P-01")
    ap.add_argument("--n", type=int, help="remplace n (1 pour une répétition à blanc)")
    ap.add_argument("--simulation", action="store_true", help="affiche les commandes sans rien exécuter")
    ap.add_argument("--dossier", help="dossier de résultats, par exemple resultats/A/2026-11-20 (défaut : date du jour)")
    a = ap.parse_args()
    with open(a.fichier, encoding="utf-8") as f:
        conf = json.load(f)
    n, pause = a.n or conf["n"], conf["pause_s"]
    scen = [s for s in conf["scenarios"] if (s["id"] == a.scenario) or (a.scenario is None and s["id"] != "P-01")]
    if not scen:
        sys.exit("aucun scénario sélectionné")
    if a.etat == "A" and any(s["id"] == "P-01" for s in scen):
        sys.exit("P-01 ne concerne que l'état B")
    random.Random(conf["graine"]).shuffle(scen)     # ordre aléatoire, identique en A et en B
    if a.simulation:
        dossier = f"resultats/{a.etat}/simulation"
    else:
        dossier = a.dossier or f"resultats/{a.etat}/{dt.date.today().isoformat()}"
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
                time.sleep(0 if a.simulation else 2)   # [rôle 1, 26/11] tcpdump livre les paquets par blocs (~1 s) : sans pause, captures vides
                capture("stop", run_id, sc.get("captures", []), a.simulation)
                rapatrier(run_id, sc.get("captures", []), dossier, a.simulation)
                with open(f"{dossier}/sorties/{run_id}.txt", "w", encoding="utf-8") as o:
                    o.write(sortie)
                w.writerow([run_id, sc["id"], a.etat, i, t0, t1, SRC_IP.get(sc["machine"], ""), sc["cible"], code])
                f.flush()
                src = SRC_IP.get(sc["machine"])
                if src:
                    ssh("opnsense", f"pfctl -k {src}", a.simulation)    # supprime les états de l'attaquant
                time.sleep(0 if a.simulation else pause)
    print(f"Terminé : {chemin}")

if __name__ == "__main__":
    main()
