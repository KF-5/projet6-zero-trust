#!/usr/bin/env python3
"""Générateur de données de test complètes et réalistes pour les campagnes A et B.
Permet de valider immédiatement toute la chaîne C9 -> C11 -> Graphiques -> Rapport.
"""

import csv
import datetime as dt
import json
import os
import random

UTC = dt.timezone.utc


def generer_campagne(dossier, etat, date_str):
    os.makedirs(f"{dossier}/sorties", exist_ok=True)
    os.makedirs(f"{dossier}/pcap", exist_ok=True)
    os.makedirs(f"{dossier}/iperf", exist_ok=True)
    os.makedirs(f"{dossier}/brut/filterlog", exist_ok=True)

    t_base = dt.datetime.fromisoformat(f"{date_str}T09:00:00+00:00")
    t_courant = t_base

    scenarios = [
        ("S-01", "203.0.113.66", 15),  # SYN scan WAN
        ("S-02", "203.0.113.66", 20),  # SYN flood
        ("S-03", "203.0.113.66", 10),  # SQLi relayé
        ("S-04", "10.10.0.12", 5),     # Mouvement latéral
        ("S-05", "10.10.0.12", 8),     # Accès direct PostgreSQL
        ("S-06", "203.0.113.66", 12),  # Fragmentation nmap -f
    ]

    runs = []
    events_brut_filter = []
    events_brut_eve = []
    events_brut_nginx = []

    # 1. Génération de runs.csv et sorties/
    for sc, src_ip, duree in scenarios:
        for idx in range(1, 11):
            run_id = f"{etat}-{sc.replace('-', '')}-{idx:02d}"
            t0 = t_courant
            t1 = t0 + dt.timedelta(seconds=duree)
            t_courant = t1 + dt.timedelta(seconds=25)  # intervalle entre tests

            runs.append({
                "run_id": run_id,
                "scenario": sc,
                "etat": etat,
                "src_ip": src_ip,
                "t0_utc": t0.isoformat().replace("+00:00", "Z"),
                "t1_utc": t1.isoformat().replace("+00:00", "Z")
            })

            # Fichiers de sorties des outils
            sortie_path = f"{dossier}/sorties/{run_id}.txt"
            if sc == "S-01":
                if etat == "A":
                    txt = "PORT     STATE SERVICE\n80/tcp   open  http\n443/tcp  open  https\n53/udp   open  domain\n/open/"
                else:
                    txt = "PORT     STATE    SERVICE\n80/tcp   filtered http\n443/tcp  filtered https" if idx > 1 else "/open/"
            elif sc == "S-02":
                txt = "Flooding 10.20.0.10:443 with SYN packets..."
            elif sc == "S-03":
                if etat == "A":
                    txt = 'HTTP/1.1 200 OK\n{"data": "secret_token_admin_revealed_12345", "status": "secret"}'
                else:
                    txt = "HTTP/1.1 403 Forbidden\nAccess Blocked by Security Policy."
            elif sc in ("S-04", "S-05"):
                if etat == "A":
                    txt = " id | username | email \n----+----------+-------\n  1 | admin    | admin@local\n(1 row)"
                else:
                    txt = "psql: error: connection to server at \"10.10.0.70\", port 5432 failed: Connection refused"
            else:  # S-06
                txt = "Nmap scan report for 203.0.113.1\nHost is up (0.0021s latency).\nFragmented packets dispatched."

            with open(sortie_path, "w", encoding="utf-8") as sf:
                sf.write(txt)

            # Événements de logs
            if etat == "B":
                # En état B, Suricata / pf alertent
                ttd_delta_ms = {
                    "S-01": 412 + random.randint(-40, 40),
                    "S-02": 350 + random.randint(-50, 50),
                    "S-03": 1400 + random.randint(-150, 150),
                    "S-04": 25 + random.randint(-5, 5),
                    "S-05": 18 + random.randint(-3, 3),
                    "S-06": 120 + random.randint(-20, 20),
                }[sc]

                t_alerte = t0 + dt.timedelta(milliseconds=ttd_delta_ms)
                ts_iso = t_alerte.isoformat()

                if sc == "S-03":
                    # Alerte relayée (E09) : 10.20.0.10 -> 10.20.0.70
                    eve_entry = {
                        "timestamp": ts_iso,
                        "event_type": "alert",
                        "src_ip": "10.20.0.10",
                        "dest_ip": "10.20.0.70",
                        "dest_port": 5000,
                        "proto": "TCP",
                        "alert": {
                            "action": "blocked",
                            "signature_id": 9000030,
                            "signature": "ET WEB_SPECIFIC_APPS Possible SQL Injection Attempt"
                        }
                    }
                    events_brut_eve.append(json.dumps(eve_entry))
                elif sc in ("S-04", "S-05"):
                    # Alerte Suricata + filterlog
                    eve_entry = {
                        "timestamp": ts_iso,
                        "event_type": "alert",
                        "src_ip": src_ip,
                        "dest_ip": "10.10.0.70",
                        "dest_port": 5432 if sc == "S-05" else 5000,
                        "proto": "TCP",
                        "alert": {
                            "action": "blocked",
                            "signature_id": 9000020 if sc == "S-05" else 9000025,
                            "signature": "SURICATA Unauthorized lateral database access"
                        }
                    }
                    events_brut_eve.append(json.dumps(eve_entry))
                    # Ligne filterlog
                    fl_line = f"{ts_iso} opnsense filterlog[412]: 82,,,fae55933,em1,match,block,in,4,0x0,,64,1234,0,none,6,tcp,60,{src_ip},10.10.0.70,41234,5432,0,S,123456,,1024"
                    events_brut_filter.append(fl_line)
                else:
                    # S-01, S-02, S-06
                    eve_entry = {
                        "timestamp": ts_iso,
                        "event_type": "alert",
                        "src_ip": src_ip,
                        "dest_ip": "203.0.113.1",
                        "dest_port": 80,
                        "proto": "TCP",
                        "alert": {
                            "action": "blocked" if sc != "S-06" else "allowed",
                            "signature_id": 9000010,
                            "signature": f"SURICATA Scan or Flood detected ({sc})"
                        }
                    }
                    events_brut_eve.append(json.dumps(eve_entry))
            else:
                # En état A : Seuls quelques logs normaux ou autorisés
                ts_iso = (t0 + dt.timedelta(milliseconds=50)).isoformat()
                fl_line = f"{ts_iso} opnsense filterlog[412]: 77,,,fae55933,em0,match,pass,in,4,0x0,,64,1234,0,none,6,tcp,60,{src_ip},203.0.113.1,41234,80,0,S,123456,,1024"
                events_brut_filter.append(fl_line)

    # Ajout de quelques faux positifs en état B (hors fenêtres)
    if etat == "B":
        t_fp = t_base + dt.timedelta(minutes=3)
        eve_fp = {
            "timestamp": t_fp.isoformat(),
            "event_type": "alert",
            "src_ip": "10.99.0.20",
            "dest_ip": "10.99.0.10",
            "dest_port": 123,
            "proto": "UDP",
            "alert": {
                "action": "allowed",
                "signature_id": 2000001,
                "signature": "ET POLICY NTP traffic outside expected window"
            }
        }
        events_brut_eve.append(json.dumps(eve_fp))

    # Nginx access log
    for idx in range(30):
        t_ng = t_base + dt.timedelta(seconds=idx * 60)
        t_ng_str = t_ng.strftime("%d/%b/%Y:%H:%M:%S +0000")
        ng_line = f'203.0.113.66 - - [{t_ng_str}] "GET /api/produits HTTP/1.1" 200 112 "-" "Mozilla/5.0"'
        events_brut_nginx.append(ng_line)

    # 2. Sauvegarde runs.csv
    with open(f"{dossier}/runs.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["run_id", "scenario", "etat", "src_ip", "t0_utc", "t1_utc"])
        w.writeheader()
        w.writerows(runs)

    # 3. Génération sla.csv (sonde toutes les 5 secondes sur toute la période)
    t_fin_totale = t_courant + dt.timedelta(minutes=5)
    sla_entries = []
    t_s = t_base - dt.timedelta(minutes=2)
    while t_s <= t_fin_totale:
        # Vérifier si on est pendant une attaque
        sous_attaque_s02 = any(
            dt.datetime.fromisoformat(r["t0_utc"]) <= t_s <= dt.datetime.fromisoformat(r["t1_utc"])
            for r in runs if r["scenario"] == "S-02"
        )
        if sous_attaque_s02 and etat == "A":
            succes = 1 if random.random() < 0.813 else 0
            duree_ms = random.randint(120, 450) if succes else 5000
        elif etat == "B":
            succes = 1 if random.random() < 0.999 else 0
            duree_ms = random.randint(44, 75)
        else:
            succes = 1
            duree_ms = random.randint(38, 64)

        sla_entries.append({
            "ts_utc": t_s.isoformat().replace("+00:00", "Z"),
            "type": "https",
            "succes": succes,
            "duree_ms": duree_ms
        })
        t_s += dt.timedelta(seconds=5)

    with open(f"{dossier}/sla.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["ts_utc", "type", "succes", "duree_ms"])
        w.writeheader()
        w.writerows(sla_entries)

    # 4. Sauvegarde des fichiers bruts
    with open(f"{dossier}/brut/filterlog/filterlog.log", "w", encoding="utf-8") as f:
        f.write("\n".join(events_brut_filter) + "\n")

    with open(f"{dossier}/brut/eve.json", "w", encoding="utf-8") as f:
        f.write("\n".join(events_brut_eve) + "\n")

    with open(f"{dossier}/brut/nginx.log", "w", encoding="utf-8") as f:
        f.write("\n".join(events_brut_nginx) + "\n")

    # 5. Fichiers iperf
    if etat == "A":
        with open(f"{dossier}/iperf/A.json", "w", encoding="utf-8") as f:
            json.dump({"end": {"sum_received": {"bits_per_second": 941200000.0}}}, f)
    else:
        with open(f"{dossier}/iperf/B-sans-ips.json", "w", encoding="utf-8") as f:
            json.dump({"end": {"sum_received": {"bits_per_second": 938500000.0}}}, f)
        with open(f"{dossier}/iperf/B-avec-ips.json", "w", encoding="utf-8") as f:
            json.dump({"end": {"sum_received": {"bits_per_second": 892400000.0}}}, f)


def main():
    print("Génération des jeux de données d'essais pour Campagne A et Campagne B...")
    generer_campagne("resultats/A/2026-11-20", "A", "2026-11-20")
    generer_campagne("resultats/B/2026-11-25", "B", "2026-11-25")
    print("Données brutes créées avec succès dans resultats/A/2026-11-20 et resultats/B/2026-11-25.")


if __name__ == "__main__":
    main()
