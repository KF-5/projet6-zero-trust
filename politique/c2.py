#!/usr/bin/env python3
"""C2 — moteur de politique (rapport, sections 7.2 et 9.2, figure 11).
Lit flows.csv, segments.csv, hosts.csv ; valide ; écrit regles-<etat>.json, regles-<etat>.md et tests.csv."""
import argparse, csv, json, sys

def lire(chemin):
    with open(chemin, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))

PORT_REPR = {"WAN": ("tcp", 443), "DMZ": ("tcp", 443), "LAN": ("tcp", 22), "DMZ-WEB": ("tcp", 443),
             "DMZ-DNS": ("udp", 53), "DMZ-APP": ("tcp", 5000), "LAN-USERS": ("tcp", 22),
             "LAN-DB": ("tcp", 5432), "LAN-ADMIN": ("tcp", 22)}      # port « représentatif » d'un segment
NAT_PUBLIC = "203.0.113.1"                                             # adresse vue depuis le WAN (FA1/F01, FA2/F02, FA6/F09)
IPERF = ("FA6", "F09")                                                 # flux activés seulement pendant les mesures (section 4.2)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--etat", choices=["A", "B"], required=True)
    ap.add_argument("--dossier", default="politique")
    a = ap.parse_args()
    d, E = a.dossier, a.etat
    flows = [f for f in lire(f"{d}/flows.csv") if f["etat"] == E]
    segs = {s["nom"]: s for s in lire(f"{d}/segments.csv") if E in s["etat"] and s["nom"] != "MGMT"}
    hosts = {h["hote"]: h for h in lire(f"{d}/hosts.csv")}
    seg = lambda x: hosts[x]["segment_" + E] if x in hosts else x       # hôte -> segment ; segment -> lui-même

    # ---------- validation ----------
    defaut = [f for f in flows if f["src"] == "any" and f["dst"] == "any"]    # F99 ou FA5
    autorises = [f for f in flows if f not in defaut]
    erreurs, avert = [], []
    for f in autorises:
        for x in [f["src"]] + f["dst"].split(";"):
            if x not in ("any", "opnsense") and seg(x) not in segs:
                erreurs.append(f"{f['id']} : « {x} » n'est ni un segment ni un hôte de l'état {E}")
        if not f["justification"].strip():
            erreurs.append(f"{f['id']} : justification absente")
        if "any" in (f["dst"], f["port"], f["proto"]):
            avert.append(f"{f['id']} : « any » contraire au moindre privilège")
    for i, f in enumerate(autorises):
        for g in autorises[i + 1:]:
            if seg(f["src"]) == seg(g["src"]) and set(f["dst"].split(";")) & set(g["dst"].split(";")) \
               and (f["port"] == "any" or g["port"] == "any" or set(f["port"].split(";")) & set(g["port"].split(";"))):
                avert.append(f"{f['id']} et {g['id']} se recouvrent")
    if len(defaut) != 1:
        erreurs.append("il faut exactement une ligne de refus par défaut (any,any)")
    for e in avert: print("AVERTISSEMENT", e)
    if erreurs:
        for e in erreurs: print("ERREUR", e)
        sys.exit(1)

    # ---------- génération des règles ----------
    def adresse(x, src_seg):
        if x == "any": return "any"
        if x == "opnsense": return segs[src_seg]["passerelle"]            # console : passerelle du segment source
        if x in hosts: return hosts[x]["ip"]
        return segs[x]["reseau"]
    def specificite(f):                                                   # du plus spécifique au plus général
        s = 0 if f["src"] in hosts else 1
        dd = 0 if all(x in hosts or x == "opnsense" for x in f["dst"].split(";")) else (2 if f["dst"] == "any" else 1)
        return (s, dd, f["port"] == "any")
    regles = {}
    for f in sorted(autorises, key=specificite):
        iface = seg(f["src"])                                             # interface d'entrée = segment source
        regles.setdefault(iface, []).append({
            "flux": f["id"], "action": "pass", "log": True, "interface": iface,
            "actif": f["id"] not in IPERF,                                # FA6 / F09 : créées mais désactivées
            "protocole": f["proto"], "source": adresse(f["src"], iface) if f["src"] in hosts else ("any" if iface == "WAN" else segs[iface]["reseau"]),
            "destination": [adresse(x, iface) for x in f["dst"].split(";")],
            "ports": f["port"].split(";"), "description": f"{f['id']} {f['justification']}"})
    for iface in segs:                                                    # refus par défaut, en dernier, journalisé
        regles.setdefault(iface, []).append({"flux": defaut[0]["id"], "action": "block", "log": True, "actif": True, "interface": iface,
            "protocole": "any", "source": "any", "destination": ["any"], "ports": ["any"], "description": defaut[0]["justification"]})
    with open(f"{d}/regles-{E}.json", "w", encoding="utf-8") as o:
        json.dump(regles, o, ensure_ascii=False, indent=2)
    with open(f"{d}/regles-{E}.md", "w", encoding="utf-8") as o:              # pour la saisie guidée dans l'interface
        o.write("| Interface | Ordre | Flux | Action | Proto | Source | Destination | Ports | Journal | Active |\n|---|---|---|---|---|---|---|---|---|---|\n")
        for iface, rs in regles.items():
            for n, r in enumerate(rs, 1):
                actif = "oui" if r["actif"] else "NON (seulement pendant les mesures iperf3)"
                o.write(f"| {iface} | {n} | {r['flux']} | {r['action']} | {r['protocole']} | {r['source']} | {', '.join(r['destination'])} | {', '.join(r['ports'])} | oui | {actif} |\n")

    # ---------- génération du plan de test ----------
    repr_hote = {}
    for h, v in hosts.items():
        if v["conteneur"] != "-": repr_hote.setdefault(v["segment_" + E], h)   # 1er hôte avec conteneur
    tests, n = [], 0
    def ajoute(src, ip, proto, port, attendu, ref):
        nonlocal n; n += 1
        tests.append([f"T{n:03d}", src, ip, proto, port, attendu, ref])
    def cible(src_seg, x):
        if seg(src_seg) == "WAN" and x in hosts and seg(x) != "WAN": return NAT_PUBLIC
        return adresse(x, src_seg)
    for f in autorises:
        s_seg = seg(f["src"]); src = f["src"] if f["src"] in hosts else repr_hote.get(s_seg)
        dsts = f["dst"].split(";")
        d0 = dsts[0] if dsts[0] != "any" else next(h for h in hosts if seg(h) not in (s_seg, "WAN") and hosts[h]["conteneur"] != "-")
        proto = "tcp" if f["proto"] in ("tcp", "tcp/udp", "any") else "udp"
        port = int(f["port"].split(";")[0]) if f["port"] != "any" else 443
        if f["id"] in IPERF:
            continue                                                        # flux iperf3 désactivés hors mesures
        ajoute(src, cible(s_seg, d0), proto, port, "pass", f["id"])
        if f["port"] != "any":
            mauvais = 22 if 22 not in [int(p) for p in f["port"].split(";")] else 23
            ajoute(src, cible(s_seg, d0), "tcp", mauvais, "block", f["id"])      # test « mauvais port »
    for a_ in segs:
        for b in segs:
            if a_ == b or b == "WAN" or a_ not in repr_hote or b not in repr_hote: continue
            if not any(seg(f["src"]) == a_ and any(x == "any" or seg(x) == b for x in f["dst"].split(";")) for f in autorises):
                proto, port = PORT_REPR[b]
                ajoute(repr_hote[a_], hosts[repr_hote[b]]["ip"], proto, port, "block", defaut[0]["id"])
    with open(f"{d}/tests.csv", "w", newline="", encoding="utf-8") as o:
        w = csv.writer(o); w.writerow(["id_test", "src_host", "dst_ip", "proto", "port", "attendu", "flux_ref"]); w.writerows(tests)
    print(f"État {E} : {sum(len(r) for r in regles.values())} règles sur {len(regles)} interfaces, {len(tests)} tests, {len(avert)} avertissement(s).")

if __name__ == "__main__":
    main()
