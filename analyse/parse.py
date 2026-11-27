#!/usr/bin/env python3
"""C9 — collecte et normalisation (section 7.9).
Rapatrie les journaux bruts, puis écrit events.csv au schéma commun.
Usage :
  parse.py --etat A [--date AAAA-MM-JJ] [--sans-collecte]
  parse.py --dossier resultats/A/essai [--sans-collecte]

Les événements sont limités à la période couverte par runs.csv et sla.csv (± 1 min), s'ils existent.
"""

import argparse
import csv
import datetime as dt
import glob
import json
import os
import re
import subprocess

UTC = dt.timezone.utc


def iso(d):
    """Horodatage commun : UTC, milliseconde, suffixe Z."""
    return d.astimezone(UTC).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def collecter(d):
    """Rapatrie les fichiers d'OPNsense et de l'hôte Docker (écart E13)."""
    os.makedirs(f"{d}/brut", exist_ok=True)
    os.makedirs(f"{d}/brut/filterlog", exist_ok=True)
    
    # Sources distantes
    for src, dst in [
        ("root@10.99.0.1:/var/log/filter/*.log", "filterlog/"),
        ("root@10.99.0.1:/var/log/suricata/eve.json*", ""),
        ("karim@10.99.0.20:/srv/logs/nginx/projet6.log", "nginx.log"),
    ]:
        try:
            subprocess.run(
                ["scp", "-q", "-o", "BatchMode=yes", src, f"{d}/brut/{dst}"],
                check=False,
                timeout=30,
            )
        except Exception as e:
            print(f"[Avertissement] Collecte distante {src} ignorée : {e}")


# filterlog : format RFC 5424 d'OPNsense (« filterlog 412 - [meta …] 82,,, »)
# ou format de fichier rsyslog (« filterlog[412]: 82,,, »)
RX_FL = re.compile(
    r"(\d{4}-\d\d-\d\dT[\d:.]+(?:Z|[+-]\d\d:?\d\d)).*?filterlog\S*\s+(?:\d+\s+-\s+(?:\[[^\]]*\]|-)\s+)?(\d+,.*)$"
)


def filterlog(chemin, w):
    for ligne in open(chemin, encoding="utf-8", errors="replace"):
        m = RX_FL.search(ligne.strip())
        if not m:
            continue
        f = m.group(2).split(",")
        if len(f) < 20 or f[8] != "4":
            continue  # IPv4 seulement
        proto = f[16]
        dport = f[21] if proto in ("tcp", "udp") and len(f) > 21 else ""
        ts_str = m.group(1).replace("Z", "+00:00")
        try:
            ts = dt.datetime.fromisoformat(ts_str)
        except ValueError:
            continue
        # Colonnes : ts_utc, source, src_ip, dst_ip, dst_port, proto, action, sid, message
        w.writerow([
            iso(ts),
            "pf",
            f[18],
            f[19],
            dport,
            proto,
            f[6],
            "",
            f"règle {f[0]} {f[3][:8]} {f[4]}"
        ])


def eve(chemin, w):
    for ligne in open(chemin, encoding="utf-8", errors="replace"):
        try:
            e = json.loads(ligne)
        except ValueError:
            continue
        if e.get("event_type") != "alert":
            continue
        a = e.get("alert", {})
        ts_str = e.get("timestamp", "")
        try:
            ts = dt.datetime.strptime(ts_str, "%Y-%m-%dT%H:%M:%S.%f%z")
        except ValueError:
            continue
        w.writerow([
            iso(ts),
            "suricata",
            e.get("src_ip", ""),
            e.get("dest_ip", ""),
            str(e.get("dest_port", "")),
            str(e.get("proto", "")).lower(),
            "drop" if a.get("action") == "blocked" else "alert",
            str(a.get("signature_id", "")),
            a.get("signature", "")
        ])


RX_NG = re.compile(r'^(\S+) \S+ \S+ \[([^\]]+)\] "(\S+) (\S+)[^"]*" (\d{3}) ')


def nginx(chemin, w):
    for ligne in open(chemin, encoding="utf-8", errors="replace"):
        m = RX_NG.match(ligne)
        if not m:
            continue
        try:
            ts = dt.datetime.strptime(m.group(2), "%d/%b/%Y:%H:%M:%S %z")
        except ValueError:
            continue
        w.writerow([
            iso(ts),
            "nginx",
            m.group(1),
            "10.20.0.10",
            "443",
            "tcp",
            m.group(5),
            "",
            f"{m.group(3)} {m.group(4)}"
        ])


def bornes(d):
    """Période de la campagne définie par runs.csv et sla.csv."""
    ts = []
    for nom, cols in (("runs.csv", ("t0_utc", "t1_utc")), ("sla.csv", ("ts_utc",))):
        p = f"{d}/{nom}"
        if os.path.exists(p):
            for r in csv.DictReader(open(p, encoding="utf-8")):
                for c in cols:
                    val = r.get(c)
                    if val:
                        try:
                            ts.append(dt.datetime.fromisoformat(val.replace("Z", "+00:00")))
                        except ValueError:
                            pass
    if not ts:
        return None
    marge = dt.timedelta(minutes=1)
    return iso(min(ts) - marge), iso(max(ts) + marge)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--etat", choices=["A", "B"])
    ap.add_argument("--date", default=dt.date.today().isoformat())
    ap.add_argument("--dossier", help="Dossier de résultats explicite (remplace --etat et --date)")
    ap.add_argument("--sans-collecte", action="store_true")
    a = ap.parse_args()

    if not a.dossier and not a.etat:
        ap.error("--etat ou --dossier est obligatoire")

    d = a.dossier or f"resultats/{a.etat}/{a.date}"

    if not a.sans_collecte:
        collecter(d)

    tmp = f"{d}/events.tmp"
    os.makedirs(d, exist_ok=True)
    with open(tmp, "w", newline="", encoding="utf-8") as o:
        w = csv.writer(o)
        for p in sorted(glob.glob(f"{d}/brut/filterlog/*")):
            filterlog(p, w)
        for p in sorted(glob.glob(f"{d}/brut/eve.json*")):
            if not p.endswith((".gz", ".bz2", ".xz")):
                eve(p, w)
        for p in sorted(glob.glob(f"{d}/brut/nginx*.log")):
            nginx(p, w)

    if not os.path.exists(tmp):
        print(f"0 événement dans {d}/events.csv (aucun fichier brut)")
        return

    lignes = sorted(csv.reader(open(tmp, encoding="utf-8")), key=lambda r: r[0])
    b = bornes(d)
    if b:
        lignes = [r for r in lignes if b[0] <= r[0] <= b[1]]

    with open(f"{d}/events.csv", "w", newline="", encoding="utf-8") as o:
        w = csv.writer(o)
        w.writerow(["ts_utc", "source", "src_ip", "dst_ip", "dst_port", "proto", "action", "sid", "message"])
        w.writerows(lignes)

    os.remove(tmp)
    print(f"{len(lignes)} événements dans {d}/events.csv")


if __name__ == "__main__":
    main()
