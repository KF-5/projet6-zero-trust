#!/usr/bin/env python3
"""C11 — corrélation et indicateurs (section 9.4, figure 13),
avec les corrections E08 (filtre sur l'attaquant) et E09 (relais déclarés).
Usage :
  correle.py resultats/A/<date> resultats/B/<date>

Dépendances : python3-pandas, python3-yaml, tshark
"""

import argparse
import os
import re
import subprocess
import pandas as pd

DELTA = pd.Timedelta(seconds=10)  # tolérance après la fin de l'attaque
RELAIS = {"S-03": [("10.20.0.10", "10.20.0.70")]}  # nginx relaie la requête de l'attaquant vers l'API


def objectif_atteint(sc, sortie, sla_fenetre):
    """L'effet réel (critère de blocage, section 9.4)."""
    if sc == "S-01":
        return "/open/" in sortie
    if sc in ("S-02", "P-01"):
        return bool(len(sla_fenetre)) and sla_fenetre["succes"].mean() < 0.99
    if sc == "S-03":
        return "secret" in sortie
    if sc in ("S-04", "S-05"):
        return re.search(r"\(1 (row|ligne)\)", sortie) is not None
    return None  # S-06 : détection seulement


def synack_dans_pcap(fichier, cible, attaquant):
    """« PCAP côté cible » : la base a-t-elle répondu À L'ATTAQUANT ?"""
    filtre = f"ip.src=={cible} && ip.dst=={attaquant} && tcp.flags.syn==1 && tcp.flags.ack==1"
    try:
        r = subprocess.run(
            ["tshark", "-r", fichier, "-Y", filtre],
            capture_output=True,
            text=True,
            timeout=120,
        )
        return bool(r.stdout.strip())
    except (OSError, subprocess.TimeoutExpired):
        return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dossiers", nargs="+")
    a = ap.parse_args()

    par_exec, globaux = [], []

    for d in a.dossiers:
        runs = pd.read_csv(f"{d}/runs.csv", parse_dates=["t0_utc", "t1_utc"])
        ev = pd.read_csv(
            f"{d}/events.csv",
            parse_dates=["ts_utc"],
            dtype={"sid": "string", "dst_port": "string"}
        )
        sla = pd.read_csv(f"{d}/sla.csv", parse_dates=["ts_utc"])

        alertes = ev[ev.source == "suricata"]
        en_fenetre = pd.Series(False, index=alertes.index)
        sla_attaque = pd.Series(False, index=sla.index)

        for _, r in runs.iterrows():
            debut, fin = r.t0_utc, r.t1_utc + DELTA
            f = alertes[(alertes.ts_utc >= debut) & (alertes.ts_utc <= fin)]
            en_fenetre |= alertes.index.isin(f.index)
            sla_attaque |= (sla.ts_utc >= debut) & (sla.ts_utc <= fin)

            # E08 : l'attaquant, pas la cible
            garde = (f.src_ip == r.src_ip) | (f.dst_ip == r.src_ip)
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
                if vu:
                    atteint = True  # la base a répondu : pas bloqué au réseau

            par_exec.append({
                "run_id": r.run_id,
                "scenario": r.scenario,
                "etat": r.etat,
                "detecte": len(A) > 0,
                "ttd_ms": round((A.ts_utc.min() - r.t0_utc).total_seconds() * 1000) if len(A) else None,
                "bloque": None if atteint is None else not atteint
            })

        hors = alertes[~en_fenetre]  # faux positifs : hors de toute fenêtre
        duree_h = (sla.ts_utc.max() - sla.ts_utc.min()).total_seconds() / 3600
        https = sla.type == "https"
        ok = sla[https & ~sla_attaque]  # référence : hors fenêtres d'attaque
        sous = sla[https & sla_attaque]

        globaux.append({
            "etat": runs.etat.iloc[0],
            "fp_par_heure": round(len(hors) / duree_h, 2) if duree_h else None,
            "disponibilite_pct": round(100 * ok.succes.mean(), 2) if len(ok) else None,  # nominale
            "disponibilite_sous_attaque_pct": round(100 * sous.succes.mean(), 2) if len(sous) else None,
            "latence_mediane_ms": ok[ok.succes == 1].duree_ms.median() if len(ok[ok.succes == 1]) else None,
            "latence_p95_ms": ok[ok.succes == 1].duree_ms.quantile(0.95, interpolation="higher") if len(ok[ok.succes == 1]) else None
        })

    os.makedirs("resultats/synthese", exist_ok=True)
    df = pd.DataFrame(par_exec)
    df.to_csv("resultats/synthese/par_execution.csv", index=False)

    ind = df.groupby(["scenario", "etat"]).agg(
        n=("run_id", "count"),
        taux_detection=("detecte", "mean"),
        taux_blocage=("bloque", lambda s: s.dropna().astype(float).mean() if s.notna().any() else None),
        ttd_median_ms=("ttd_ms", "median")
    ).reset_index()
    ind.to_csv("resultats/synthese/indicateurs.csv", index=False)

    pd.DataFrame(globaux).to_csv("resultats/synthese/globaux.csv", index=False)

    print("=== Indicateurs par scénario ===")
    print(ind.to_string(index=False))
    print("\n=== Indicateurs globaux ===")
    print(pd.DataFrame(globaux).to_string(index=False))


if __name__ == "__main__":
    main()
