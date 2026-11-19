/* Rôle 6 : observabilité et analyse (C9, C11, NTP, exploitation de C10). */
window.G = window.G || {}; G.roles = G.roles || {};
(function(){
const PARSE = `#!/usr/bin/env python3
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
RX_FL = re.compile(r"(\\d{4}-\\d\\d-\\d\\dT[\\d:.]+(?:Z|[+-]\\d\\d:?\\d\\d)).*?filterlog\\S*\\s+(?:\\d+\\s+-\\s+(?:\\[[^\\]]*\\]|-)\\s+)?(\\d+,.*)$")
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

RX_NG = re.compile(r'^(\\S+) \\S+ \\S+ \\[([^\\]]+)\\] "(\\S+) (\\S+)[^"]*" (\\d{3}) ')
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
    main()`;
const FP = `#!/usr/bin/env python3
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
    print(f"{sid},{n},{n / duree:.2f}")`;
const CORR = `#!/usr/bin/env python3
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
    if sc in ("S-04", "S-05"): return re.search(r"\\(1 (row|ligne)\\)", sortie) is not None
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
    main()`;
const GRAPH = `#!/usr/bin/env python3
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
print("graphiques écrits dans", S)`;

G.roles.analyse = {
intro:`<p><b>Les chiffres du rapport final sortent de vos scripts, jamais d'un tableur rempli à la main.</b> Tout est rejouable à partir des fichiers bruts archivés : si une correction est faite, on relance la chaîne.</p><p>Trois corrections du pseudo-code de la section 9.4 sont appliquées : filtre sur l'adresse de l'attaquant (écart E08), relais déclaré pour S-03 (écart E09), relecture des fichiers bruts plutôt que du seul syslog UDP (écart E13).</p><p><b>Mise à jour du 17 novembre.</b> Le collecteur tourne déjà (<code>p6-collecteur</code>, 10.99.0.10) : NTP pour tout MGMT et réception syslog UDP 514 rangée par machine dans <code>/var/log/p6/&lt;adresse&gt;.log</code> (par exemple <code>/var/log/p6/10.99.0.1.log</code> pour OPNsense), et non dans <code>/var/log/remote/</code>. Adapte les chemins de tes parseurs. Console : TightVNC sur <code>127.0.0.1::5903</code> par le tunnel.</p><p><b>Comptes et SSH réels.</b> Les VM Debian et Kali ont l'utilisateur <code>karim</code> avec <code>sudo</code>, pas de connexion <code>root</code>. SSH n'est accepté sur les VM que depuis Windows (10.99.0.2) et le collecteur (10.99.0.10) ; le SSH d'OPNsense est désactivé. Avant que l'orchestrateur tourne depuis le collecteur, le rôle 1 doit : installer la clé du collecteur sur p6-docker et p6-kali (pour <code>root</code>, ou pour <code>karim</code> avec un <code>sudo</code> sans mot de passe limité aux commandes utiles), et réactiver le SSH d'OPNsense sur MGMT, par clé seulement (la règle « Admin du pare-feu » autorise déjà le port 22 depuis 10.99.0.10).</p>`,
steps:[
/* 1 */ {titre:"Synchroniser les horloges du laboratoire (NTP)", quand:"14 nov.", fait:true,
 maj:`<p>Fait par le rôle 1 (script <code>setup-collecteur.sh</code>) : chrony serveur sur le collecteur (<code>allow 10.99.0.0/24</code>, <code>local stratum 8</code>), clients : OPNsense (Services › Network Time, 10.99.0.10 seul), p6-docker et Kali. OPNsense avait 17 h 35 de retard après une suspension : corrigé le 16 novembre. Contrôle : <code>chronyc clients</code> sur le collecteur. Le collecteur prend son heure de départ de Windows (horloge de VMware).</p>`,
 but:"Le collecteur sert l'heure à toutes les VM, et l'écart entre horloges est mesuré avant chaque campagne.",
 pourquoi:"Section 6 : les horloges des 4 VM sont synchronisées par NTP sur le collecteur, condition nécessaire au calcul des temps de détection. Risque « horloges désynchronisées » (section 11.2) : contrôle du décalage avant chaque campagne.",
 prereq:{etapes:["chef-4","chef-5"], modules:[10]},
 schema:"fenetre-ttd",
 config:[
  {t:"Installer chrony (collecteur, hôte Docker, Kali) pendant une fenêtre de configuration", code:"apt install chrony     # remplace systemd-timesyncd, désactivé automatiquement à l'installation\nsystemctl is-active systemd-timesyncd chrony", note:"Résultat attendu : <code>inactive</code> puis <code>active</code>. Deux clients NTP en même temps se contrediraient (à vérifier sur votre version)."},
  {t:"Serveur NTP sur le collecteur", file:"/etc/chrony/chrony.conf (ajouts)", code:`# Supprimer ou commenter les lignes « pool … » : le laboratoire n'a pas d'accès à Internet
local stratum 10          # le collecteur fait référence même sans source externe
allow 10.99.0.0/24        # sert l'heure sur MGMT uniquement`, note:"Puis <code>systemctl restart chrony</code>. Le collecteur prend son heure de départ de l'hôte Windows (horloge de VMware) : l'exactitude absolue importe peu, l'accord entre machines est ce qui compte."},
  {t:"Clients : hôte Docker et Kali (OPNsense : rôle 2)", file:"/etc/chrony/chrony.conf", code:`# remplacer les lignes pool par :
server 10.99.0.10 iburst  # le collecteur, seule référence`}
 ],
 tester:[{t:"Avant chaque campagne, sur le collecteur", cmd:"chronyc clients\nfor h in 10.99.0.20 10.99.0.66; do ssh root@$h 'chronyc tracking | grep -E \"Reference|System time\"'; done", sortie:"Hostname         NTP   Drop Int IntL Last\n10.99.0.1         12      0   6   -    31\n10.99.0.20        12      0   6   -    40\n10.99.0.66        12      0   6   -    18\nReference ID    : 0A63000A (10.99.0.10)\nSystem time     : 0.000041210 seconds fast of NTP time", sens:[["trois clients","OPNsense, hôte Docker, Kali interrogent le collecteur"],["0.000041 seconds","écart de 41 µs : négligeable devant un temps de détection en millisecondes"]]}],
 pannes:[["Un client n'apparaît pas","Pare-feu du collecteur (UDP 123 depuis 10.99.0.0/24)","Rôle 1, étape 5."],["« Reference ID : 00000000 »","Le client ne joint pas le serveur","Vérifier <code>server 10.99.0.10</code> et la route MGMT."],["Écart de plusieurs secondes après un rollback","Snapshot avec RAM restauré","Attendre la resynchronisation (ou <code>chronyc makestep</code>) avant toute mesure."]],
 fini:["Les 3 clients synchronisés sur 10.99.0.10","Écart < 10 ms, noté dans docs/jalons.md avant chaque campagne"],
 livrer:"Message à tous : « NTP en place ; contrôle à faire avant chaque campagne (commande ci-dessus) »."
},
/* 2 */ {titre:"Recevoir les journaux : rsyslog", quand:"15–16 nov.", fait:"en partie fait",
 maj:`<p>En partie fait par le rôle 1 : rsyslog reçoit en UDP 514 et range chaque source dans <code>/var/log/p6/&lt;adresse&gt;.log</code> ; OPNsense envoie déjà ses journaux (System › Settings › Logging › Remote, 10.99.0.10 UDP 514), lignes <code>filterlog</code> reçues. Restent : le journal d'accès de nginx depuis p6-docker, TCP 514 si tu le gardes (écart E13), et les noms de fichiers attendus par ton parseur.</p>`,
 but:"Le collecteur reçoit filterlog et les alertes EVE d'OPNsense, et le journal d'accès de nginx, dans des fichiers horodatés précisément.",
 pourquoi:"Section 7.9 : rsyslog reçoit filterlog et EVE JSON d'OPNsense, et les journaux Docker. Section 6 : syslog vers le collecteur est l'un des trois usages autorisés de MGMT.",
 prereq:{etapes:["chef-5"], texte:"La cible syslog d'OPNsense (rôle 2, étape 5) peut arriver après : le collecteur est prêt à recevoir avant.", modules:[10]},
 schema:"pipeline-logs",
 config:[
  {t:"Collecteur", file:"/etc/rsyslog.d/10-projet6.conf", code:`module(load="imudp")
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
}`, note:"Debian 13 n'installe plus rsyslog par défaut : <code>apt install rsyslog</code> pendant une fenêtre de configuration. Puis <code>systemctl restart rsyslog</code>."},
  {t:"Hôte Docker : envoyer le journal d'accès de nginx", file:"/etc/rsyslog.d/20-nginx.conf (p6-docker)", code:`module(load="imfile")
input(type="imfile" File="/srv/logs/nginx/projet6.log" Tag="nginx:" Facility="local6")   # fichier écrit par nginx (rôle 4)
local6.* action(type="omfwd" target="10.99.0.10" port="514" protocol="tcp")             # vers le collecteur`, note:"Sur l'hôte Docker aussi : <code>apt install rsyslog</code> pendant une fenêtre de configuration (absent par défaut sous Debian 13), puis <code>systemctl restart rsyslog</code>."}
 ],
 tester:[{cmd:"tail -n 2 /var/log/remote/opnsense.log\ntail -n 1 /var/log/remote/docker.log", sortie:"2026-11-17T15:02:11.420318+01:00 opnsense filterlog[412]: 77,,,fae5…,em0,match,block,in,4,0x0,,64,…,203.0.113.66,203.0.113.1,40001,22,0,S,…\n2026-11-17T15:02:12.001+01:00 opnsense suricata[901]: … (vide en état A)\n2026-11-17T16:02:11.000000+00:00 docker nginx: 203.0.113.66 - - [17/Nov/2026:16:02:11 +0000] \"GET /api/produits HTTP/1.1\" 200 112 …", sens:[["…420318+01:00","horodatage à la microseconde avec fuseau : converti en UTC par parse.py"],["filterlog[412]:","l'étiquette qui permet au parseur de reconnaître la ligne"]]}],
 pannes:[["Rien n'arrive","<code>tcpdump -ni ens32 port 514</code> sur le collecteur","Rien sur le fil : cible syslog d'OPNsense (rôle 2). Paquets vus mais pas de fichier : pare-feu du collecteur ou configuration rsyslog."],["Horodatage sans année ni millisecondes","Modèle de fichier par défaut","Utiliser <code>RSYSLOG_FileFormat</code>."],["Le journal nginx n'arrive pas","Fichier vide côté hôte Docker","Rôle 4, étape 4 : nginx doit écrire dans /srv/logs/nginx/projet6.log."]],
 fini:["filterlog d'OPNsense reçu avec un horodatage complet","Journal d'accès nginx reçu","Alertes EVE reçues (vérifiable en état B)"],
 livrer:"Commit de <code>analyse/rsyslog/</code>. Message au rôle 2 : « syslog reçu, merci de garder aussi les journaux locaux »."
},
/* 3 */ {titre:"Écrire le parseur : events.csv", quand:"16–19 nov.",
 but:"Un programme qui rapatrie les fichiers bruts et écrit events.csv au schéma commun, trié dans l'ordre chronologique.",
 pourquoi:"Section 7.9 : un parseur convertit chaque ligne vers le schéma commun (ts_utc, source, src_ip, dst_ip, dst_port, proto, action, sid, message). Jalon du 19 novembre : une exécution S-01 produit runs.csv et events.csv. Les fichiers bruts d'OPNsense complètent syslog (écart E13).",
 prereq:{etapes:["analyse-2"], texte:"Format : page <a href='#contrat-events'>Contrats</a>.", modules:[10,5]},
 schema:"pipeline-logs",
 config:[{t:"Parseur", file:"analyse/parse.py", code:PARSE},{t:"Faux positifs par sid (pour le rôle 3)", file:"analyse/fp.py", code:FP}],
 tester:[{cmd:"python3 analyse/parse.py --etat A --date 2026-11-19\nhead -3 resultats/A/2026-11-19/events.csv", sortie:"142 événements dans resultats/A/2026-11-19/events.csv\nts_utc,source,src_ip,dst_ip,dst_port,proto,action,sid,message\n2026-11-19T09:02:10.533Z,pf,203.0.113.66,203.0.113.1,22,tcp,block,,règle 77 fae55933 em0", sens:[["…Z","heure convertie en UTC"],["pf … block","ligne filterlog normalisée : c'est le format attendu par correle.py"]]}],
 pannes:[["0 événement","Fichiers absents de brut/, ou période de runs.csv / sla.csv sans rapport avec les journaux","Vérifier la collecte (scp) ou utiliser --sans-collecte avec des fichiers copiés à la main ; vérifier les dates de runs.csv."],["Lignes filterlog ignorées","Format de ligne différent","Comparer une ligne brute à l'expression RX_FL ; ajuster la partie qui précède les champs."],["« time data … does not match format »","Format d'horodatage EVE différent","Afficher une ligne d'eve.json ; adapter le format de strptime."]],
 fini:["events.csv produit pour l'essai du 19 novembre (<code>parse.py --dossier resultats/A/essai</code>)","Les trois sources présentes (pf, suricata en état B, nginx)","Ordre chronologique vérifié"],
 livrer:"Commit de <code>analyse/parse.py</code> et <code>analyse/fp.py</code>. Message aux rôles 1, 3, 5 : « parseur prêt »."
},
/* 4 */ {titre:"Exploiter les captures (C10)", quand:"20–26 nov.",
 but:"Pour chaque exécution, les PCAP rangés, et une vérification automatique de l'effet réel côté cible.",
 pourquoi:"Section 7.10 : les PCAP servent à prouver si l'attaque a atteint sa cible (critère de blocage) et à fournir les captures commentées. Section 9.4 : effet vérifié dans la sortie de l'outil et dans le PCAP côté cible.",
 prereq:{etapes:["services-7","attaques-3"], texte:"Nommage : page <a href='#contrat-arbo'>Contrats</a>.", modules:[10,3]},
 schema:"macvlan", schemaSt:"A",
 config:[{t:"Vérifications en ligne de commande (collecteur, tshark installé)", code:`# La base a-t-elle répondu à client-03 ? (S-05)
tshark -r resultats/A/2026-11-20/pcap/A-S05-01_postgres.pcap -Y "ip.src==10.10.0.70 && ip.dst==10.10.0.12 && tcp.flags.syn==1 && tcp.flags.ack==1"
# La requête d'injection est-elle arrivée à l'API ? (S-03)
# -d : l'API écoute sur 5000, que tshark ne décode pas en HTTP par défaut
tshark -r resultats/A/2026-11-20/pcap/A-S03-01_api.pcap -d tcp.port==5000,http -Y 'http.request.uri contains "OR"' -T fields -e http.request.uri
# Fragments vus par Kali (S-06)
tshark -r resultats/A/2026-11-20/pcap/A-S06-01_kali-wan.pcap -Y "ip.flags.mf==1 || ip.frag_offset>0" | wc -l`}],
 tester:[{cmd:"(première commande, état A puis état B)", sortie:"  2 0.000412 10.10.0.70 → 10.10.0.12 TCP 74 5432 → 43120 [SYN, ACK] Seq=0 Ack=1 …\n(état B : aucune ligne)", sens:[["SYN, ACK de 10.10.0.70 vers 10.10.0.12","état A : la base a répondu à l'attaquant ; pas bloqué au réseau. Le filtre sur la destination écarte les réponses à l'API (trafic légitime F05)."],["aucune ligne","état B : la base n'a jamais reçu le SYN ; c'est la preuve du blocage"]]}],
 pannes:[["PCAP vide","Capture arrêtée par KILL, ou mauvais point","capture.sh stop envoie INT ; vérifier le point de capture dans scenarios.yaml."],["Fichiers énormes pendant S-02","Capture de nginx pendant un flood","S-02 ne capture que kali-wan (scenarios.yaml)."],["tshark absent","—","<code>apt install tshark</code> pendant une fenêtre de configuration."]],
 fini:["Chaque exécution a ses PCAP au bon nom","Les vérifications tshark donnent le résultat attendu sur l'essai du 19"],
 livrer:"Archive datée des PCAP (hors Git) et message au rôle 1 : emplacement de l'archive."
},
/* 5 */ {titre:"Écrire la corrélation C11", quand:"22–24 nov. (sur A), relancée le 27 sur B",
 but:"Pour chaque exécution : détecté, temps de détection, bloqué ; pour chaque état : faux positifs par heure, disponibilité, latence.",
 pourquoi:"Section 9.4 et figure 13 : fenêtre [t0, t1 + 10 s] ; détecté = au moins une alerte ; TTD = première alerte − t0 ; bloqué = objectif non atteint (outil + PCAP) ; faux positifs = alertes hors de toute fenêtre, par heure de trafic légitime. Corrections E08 et E09 appliquées.",
 prereq:{etapes:["analyse-3","attaques-5"], texte:"Écrite et vérifiée sur la campagne A ; relancée sur A et B dès la fin de la campagne B (attaques-6).", modules:[10]},
 schema:"fenetre-ttd",
 config:[{t:"Corrélation", file:"analyse/correle.py", code:CORR}],
 tester:[{cmd:"python3 analyse/correle.py resultats/A/2026-11-20 resultats/B/2026-11-25", sortie:"scenario etat  n  taux_detection  taux_blocage  ttd_median_ms\n    S-01    A 10             0.0           0.0            NaN\n    S-01    B 10             1.0           0.9          412.0\n    S-05    A 10             0.0           0.0            NaN\n    S-05    B 10             1.0           1.0           18.0\n…\netat  fp_par_heure  disponibilite_pct  disponibilite_sous_attaque_pct  latence_mediane_ms  latence_p95_ms\n   A          0.00             100.00                           81.30                42.0            61.0\n   B          0.40              99.90                           99.20                47.0            72.0", sens:[["exemple de forme, pas de résultat","les valeurs réelles viendront de vos campagnes"],["disponibilite_pct","disponibilité nominale, hors fenêtres d'attaque : c'est la valeur à comparer à l'objectif ≥ 99 % ; la disponibilité sous attaque est présentée à part"],["ttd NaN en A","aucune alerte en état A : pas de temps de détection"],["taux_blocage 0.9","9 exécutions sur 10 bloquées : à commenter (sondes passées avant le seuil)"]]}],
 pannes:[["Toutes les détections à 0 en état B","Horloges décalées ou fenêtre fausse","Comparer t0 et les premières alertes à la main ; vérifier NTP (étape 1)."],["Détection de S-03 à 0","Relais non pris en compte","Vérifier RELAIS et les adresses de la ligne EVE (10.20.0.10 → 10.20.0.70)."],["taux_blocage vide pour S-06","Normal : pas d'objectif de blocage","Le présenter comme « détection seulement »."]],
 fini:["par_execution.csv, indicateurs.csv, globaux.csv produits pour A et B","Chaque valeur inattendue a été vérifiée à la main sur au moins une exécution"],
 livrer:"Commit de <code>analyse/correle.py</code> et de <code>resultats/synthese/*.csv</code>. Message au rôle 3 : fp_par_sid.csv pour le réglage (le 24) ; au rôle 1 : indicateurs le 28."
},
/* 6 */ {titre:"Produire les indicateurs et les graphiques A vs B", quand:"27–28 nov.",
 but:"Les tableaux et graphiques du rapport final, régénérables d'une commande.",
 pourquoi:"Section 10.2 : taux de détection, taux de blocage, temps de détection, faux positifs, disponibilité, latence introduite (sonde, iperf3 : A, B sans IPS, B avec IPS). EF7 : tableaux et graphiques A vs B. Jalon du 28 novembre.",
 prereq:{etapes:["analyse-5","attaques-6"], modules:[10,12]},
 schema:"graphes-maquette",
 config:[{t:"Graphiques", file:"analyse/graphes.py", code:GRAPH}],
 tester:[{cmd:"python3 analyse/graphes.py && ls resultats/synthese/*.png", sortie:"graphiques écrits dans resultats/synthese\nresultats/synthese/blocage.png  resultats/synthese/detection.png\nresultats/synthese/latence_debit.png  resultats/synthese/ttd.png", sens:[["4 images","chaque graphique répond à une question du sujet"]]}],
 pannes:[["Un scénario manque dans un graphique","Aucune exécution pour cet état","Vérifier runs.csv ; ne jamais compléter à la main."],["Les pourcentages ne correspondent pas au tableau","Graphique ancien","Toujours régénérer après correle.py."],["Pas de débit iperf3","Fichiers JSON absents","Rôle 5 : iperf/A.json dans le dossier de la campagne A, iperf/B-avec-ips.json et iperf/B-sans-ips.json dans celui de la campagne B."]],
 fini:["Les 4 graphiques produits par script","Chaque « attendu » de la section 10.1 a son verdict mesuré (confirmé / infirmé)","Tableau final relu par un autre rôle"],
 livrer:"Commit de <code>resultats/synthese/</code> et message au rôle 1 : « indicateurs et graphiques prêts pour le rapport »."
},
/* 7 */ {titre:"Commenter 4 à 6 captures Wireshark", quand:"27–28 nov.",
 but:"Des captures commentées qui montrent, paquet par paquet, la différence entre A et B.",
 pourquoi:"Section 7.10 : les PCAP fournissent les captures Wireshark commentées demandées par le sujet. Elles illustrent les figures 6 et 7 avec des paquets réels.",
 prereq:{etapes:["analyse-4","attaques-6"], modules:[10,1,9]},
 config:[{t:"Captures à produire et filtres d'affichage", code:`1. S-05 état A — A-S05-01_postgres.pcap        filtre : tcp.port == 5432
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
   Montrer : les fragments de 8 octets produits par nmap -f.`},{t:"Comment annoter", code:`Wireshark › Edit › Packet Comment… sur les paquets clés, puis File › Save As… au format pcapng (les commentaires y sont gardés).
Pour le rapport : capture d'écran de la liste des paquets + 3 à 5 lignes d'explication sous l'image
(que montre-t-on, quel paquet regarder, quelle conclusion pour A ou B).`}],
 tester:[{cmd:"tshark -r captures/1-S05-A.pcapng -Y 'frame.comment' -T fields -e frame.number -e frame.comment", sortie:"1\tSYN de client-03 vers PostgreSQL : aucun pare-feu sur le chemin\n2\tSYN-ACK : la base accepte la connexion", sens:[["commentaires présents","les annotations sont enregistrées dans le fichier remis"]]}],
 pannes:[["Les commentaires disparaissent","Enregistré au format pcap","Enregistrer en pcapng."],["HTTP illisible","Capture prise sur le WAN (TLS)","Utiliser la capture côté API (saut nginx → API en clair)."]],
 fini:["4 à 6 captures pcapng commentées","Chaque capture a sa légende dans le rapport"],
 livrer:"Dossier <code>rapport/captures/</code> (pcapng + images) et message au rôle 1."
}
],
demo:{intro:"Ce que vous montrez en 2 minutes : chaque chiffre vient d'un script et se vérifie à la main.", etapes:["Une ligne filterlog brute, puis la même ligne dans events.csv (UTC).","Le schéma fenêtre [t0, t1 + 10 s] et le calcul d'un TTD sur une exécution réelle.","Le tableau des indicateurs A vs B et un graphique.","Une capture Wireshark commentée (S-05 en A)."], secours:"images des graphiques et des captures dans le rapport."},
questions:[
 ["Comment calculez-vous le taux de détection ?","Pour chaque exécution, on cherche les alertes Suricata dans [t0, t1 + 10 s] qui concernent l'adresse de l'attaquant (ou un relais déclaré, comme nginx pour S-03). Détecté = au moins une alerte. Le taux est la moyenne sur les N exécutions d'un scénario et d'un état."],
 ["Pourquoi ne pas filtrer sur la cible, comme dans le pseudo-code de la conception ?","Parce que la cible de S-02 et S-03 est nginx, que la sonde légitime interroge en permanence : une alerte causée par la sonde pendant la fenêtre aurait compté comme détection. Le filtre sur l'attaquant évite ce biais (écart E08)."],
 ["Comment jugez-vous qu'une attaque est bloquée ?","Sur l'effet réel : ports ouverts dans la sortie de nmap, ligne renvoyée par psql, « secret » dans la réponse, disponibilité de la sonde ; et pour S-04 et S-05, présence ou non d'un SYN-ACK de la base dans le PCAP côté cible."],
 ["Qu'est-ce qu'un faux positif dans vos chiffres ?","Une alerte hors de toute fenêtre d'attaque, rapportée à la durée de trafic légitime. La mesure de 2 h de trafic légitime seul est la référence : pendant ce temps, toute alerte est un faux positif."],
 ["Pourquoi la médiane et le 95e centile pour la latence ?","Avec quelques valeurs extrêmes, la moyenne est trompeuse. La médiane décrit l'expérience typique, le p95 les lenteurs occasionnelles ; on compare A, B sans IPS et B avec IPS pour isoler le coût de Suricata."]
]
};
})();
