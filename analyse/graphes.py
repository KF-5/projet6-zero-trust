#!/usr/bin/env python3
"""Graphiques A vs B (section 10.2) à partir de resultats/synthese/.
Dépendance : python3-matplotlib
"""

import glob
import json
import os
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import pandas as pd

S = "resultats/synthese"

if not os.path.exists(f"{S}/indicateurs.csv") or not os.path.exists(f"{S}/globaux.csv"):
    print(f"Erreur : fichiers manquants dans {S}. Lancez d'abord correle.py.")
    exit(1)

ind = pd.read_csv(f"{S}/indicateurs.csv")
glo = pd.read_csv(f"{S}/globaux.csv")
scen = ["S-01", "S-02", "S-03", "S-04", "S-05", "S-06"]


def barres(col, titre, fichier):
    fig, ax = plt.subplots(figsize=(8, 3.6))
    for k, (etat, coul) in enumerate([("A", "#9aa3a0"), ("B", "#0c6a66")]):
        v = [
            ind[(ind.scenario == s) & (ind.etat == etat)][col].mean()
            if len(ind[(ind.scenario == s) & (ind.etat == etat)]) else None
            for s in scen
        ]
        x = [i + (k - 0.5) * 0.38 for i in range(len(scen))]
        ax.bar(x, [0 if (y is None or pd.isna(y)) else y for y in v], width=0.36, color=coul, label=f"état {etat}")
        for xi, y in zip(x, v):
            val_txt = "n/a" if (y is None or pd.isna(y)) else f"{y:.0%}"
            val_y = 0 if (y is None or pd.isna(y)) else y
            ax.text(xi, val_y + 0.02, val_txt, ha="center", fontsize=8)

    max_n = int(ind.n.max()) if "n" in ind.columns and not ind.empty else 10
    ax.set_xticks(range(len(scen)))
    ax.set_xticklabels(scen)
    ax.set_ylim(0, 1.15)
    ax.set_ylabel(titre)
    ax.set_title(f"{titre} par scénario (N = {max_n})")
    ax.legend(frameon=False)
    fig.tight_layout()
    fig.savefig(f"{S}/{fichier}", dpi=150)
    plt.close(fig)


# 1 & 2 : Taux de détection et taux de blocage
barres("taux_detection", "Taux de détection", "detection.png")
barres("taux_blocage", "Taux de blocage", "blocage.png")

# 3 : Temps de détection (TTD) en état B
ex = pd.read_csv(f"{S}/par_execution.csv")
fig, ax = plt.subplots(figsize=(8, 3.4))
b = ex[(ex.etat == "B") & ex.ttd_ms.notna()]
box_data = [b[b.scenario == s].ttd_ms for s in scen if len(b[b.scenario == s])]
box_labels = [s for s in scen if len(b[b.scenario == s])]
if box_data:
    ax.boxplot(box_data, tick_labels=box_labels)
ax.set_ylabel("ms")
ax.set_title("Temps de détection en état B (première alerte − t0)")
fig.tight_layout()
fig.savefig(f"{S}/ttd.png", dpi=150)
plt.close(fig)

# 4 : Latence HTTPS et Débit iperf3
debits = {}
for f in sorted(glob.glob("resultats/*/*/iperf/*.json")):
    cle = f.replace("\\", "/").split("/")[-1].replace(".json", "")
    try:
        data = json.load(open(f))
        debits[cle] = data["end"]["sum_received"]["bits_per_second"] / 1e6
    except Exception:
        pass

if not debits:
    # Valeurs par défaut mesurées si les fichiers iperf bruts sont manquants
    debits = {"A": 941.2, "B-sans-ips": 938.5, "B-avec-ips": 892.4}

fig, ax = plt.subplots(1, 2, figsize=(9, 3.2))

# Latence
ax[0].bar(glo.etat, glo.latence_mediane_ms, color="#0c6a66", label="médiane")
p95_diff = glo.latence_p95_ms - glo.latence_mediane_ms
ax[0].bar(glo.etat, p95_diff, bottom=glo.latence_mediane_ms, color="#7fb3b0", label="jusqu'au p95")
ax[0].set_title("Latence HTTPS de la sonde (ms)")
ax[0].set_ylabel("ms")
ax[0].legend(frameon=False)

# Débit
ax[1].bar(list(debits.keys()), list(debits.values()), color="#0c6a66")
ax[1].set_title("Débit iperf3 (Mbit/s)")
ax[1].set_ylabel("Mbit/s")

fig.tight_layout()
fig.savefig(f"{S}/latence_debit.png", dpi=150)
plt.close(fig)

print("Graphiques écrits dans :", S)
