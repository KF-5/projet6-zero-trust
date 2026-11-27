#!/usr/bin/env python3
"""Faux positifs par sid sur une période de trafic légitime seul (section 9.3, utilisé par le rôle 3).
Usage :
  fp.py events.csv --debut 2026-11-24T08:00:00Z --fin 2026-11-24T10:00:00Z > fp_par_sid.csv
  fp.py events.csv --duree-h 2 (si events.csv ne contient que la période de mesure)
"""

import argparse
import collections
import csv
import datetime as dt

ap = argparse.ArgumentParser()
ap.add_argument("events")
ap.add_argument("--debut")
ap.add_argument("--fin")
ap.add_argument("--duree-h", type=float)
a = ap.parse_args()


def lire(s):
    return dt.datetime.fromisoformat(s.replace("Z", "+00:00"))


if a.debut and a.fin:
    duree = (lire(a.fin) - lire(a.debut)).total_seconds() / 3600
elif a.duree_h:
    duree = a.duree_h
else:
    ap.error("donner --debut et --fin, ou --duree-h")


def dedans(r):
    t = lire(r["ts_utc"])
    return (not a.debut or t >= lire(a.debut)) and (not a.fin or t <= lire(a.fin))


c = collections.Counter(
    r["sid"]
    for r in csv.DictReader(open(a.events, encoding="utf-8"))
    if r["source"] == "suricata" and dedans(r)
)

print("sid,alertes,fp_par_heure")
for sid, n in c.most_common():
    print(f"{sid},{n},{n / duree:.2f}")
