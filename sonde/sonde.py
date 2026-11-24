#!/usr/bin/env python3
"""C8 — sonde SLA (rapport de conception, section 8.8).
1 requête HTTPS par seconde ; si un serveur DNS est donné, 1 requête DNS toutes les 5 s.
Usage : sonde.py <client> <url> [serveur_dns]   — écrit des lignes sla.csv sur la sortie standard
(colonnes : ts_utc,client,cible,type,succes,duree_ms ; voir la page Contrats du guide).

Variante du guide : la requête DNS passe par la commande « dig » (paquet dnsutils, déjà dans
l'image p6/outils:1) au lieu de python3-dnspython, absent de l'image. Nom interrogé : www.p6.lab
(zone réelle de bind9 ; le guide écrivait www.labo.test)."""
import csv, datetime as dt, subprocess, sys, time
import requests, urllib3

urllib3.disable_warnings()                      # certificat auto-signé de nginx : vérification désactivée
client, url = sys.argv[1], sys.argv[2]
serveur = sys.argv[3] if len(sys.argv) > 3 else None
NOM_DNS = "www.p6.lab"
w = csv.writer(sys.stdout)

def horodatage():
    return dt.datetime.now(dt.timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")

def requete_dns():
    """Vrai si bind9 répond au moins une adresse en 2 s au plus (un seul essai)."""
    r = subprocess.run(["dig", "@" + serveur, NOM_DNS, "A", "+time=2", "+tries=1", "+short"],
                       capture_output=True, text=True, timeout=5)
    return r.returncode == 0 and r.stdout.strip() != ""

n = 0
while True:
    debut = time.monotonic()
    ts = horodatage()
    try:
        t = time.monotonic()
        ok = requests.get(url, timeout=2, verify=False).status_code == 200
        w.writerow([ts, client, url, "https", int(ok), round((time.monotonic() - t) * 1000) if ok else ""])
    except requests.RequestException:
        w.writerow([ts, client, url, "https", 0, ""])        # délai dépassé ou connexion refusée
    if serveur and n % 5 == 0:
        ts = horodatage()
        try:
            t = time.monotonic()
            ok = requete_dns()
            w.writerow([ts, client, serveur, "dns", int(ok), round((time.monotonic() - t) * 1000) if ok else ""])
        except (subprocess.SubprocessError, OSError):
            w.writerow([ts, client, serveur, "dns", 0, ""])
    sys.stdout.flush()
    n += 1
    time.sleep(max(0, 1 - (time.monotonic() - debut)))      # une itération par seconde
