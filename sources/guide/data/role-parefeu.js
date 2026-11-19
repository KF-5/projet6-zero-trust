/* Rôle 2 : pare-feu et segmentation (C1, C2). */
window.G = window.G || {}; G.roles = G.roles || {};
const R2_C2 = `#!/usr/bin/env python3
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
            if seg(f["src"]) == seg(g["src"]) and set(f["dst"].split(";")) & set(g["dst"].split(";")) \\
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
        o.write("| Interface | Ordre | Flux | Action | Proto | Source | Destination | Ports | Journal | Active |\\n|---|---|---|---|---|---|---|---|---|---|\\n")
        for iface, rs in regles.items():
            for n, r in enumerate(rs, 1):
                actif = "oui" if r["actif"] else "NON (seulement pendant les mesures iperf3)"
                o.write(f"| {iface} | {n} | {r['flux']} | {r['action']} | {r['protocole']} | {r['source']} | {', '.join(r['destination'])} | {', '.join(r['ports'])} | oui | {actif} |\\n")

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
    main()`;
const R2_TF = `#!/bin/bash
# test_flux — exécute tests.csv depuis l'espace réseau de chaque conteneur source (écart E23).
# À lancer sur l'hôte Docker, en root :  ./test_flux.sh politique/tests.csv politique/hosts.csv
set -u
TESTS=\${1:-politique/tests.csv}; HOSTS=\${2:-politique/hosts.csv}
printf "%-6s %-11s %-15s %-4s %-5s %-6s %-6s %s\\n" ID SOURCE DESTINATION PROTO PORT ATTENDU OBTENU VERDICT
# tr -d '\\r' : un CSV enregistré sous Windows (fin de ligne CRLF) fausserait la dernière colonne
tail -n +2 "$TESTS" | tr -d '\\r' | while IFS=, read -r id src dst proto port attendu ref; do
  ctr=$(tr -d '\\r' < "$HOSTS" | awk -F, -v h="$src" '$1==h{print $5}')   # nom du conteneur source (hosts.csv)
  pid=$(docker inspect -f '{{.State.Pid}}' "$ctr" 2>/dev/null)      # processus du conteneur
  if [ -z "$pid" ] || [ "$pid" = "0" ]; then                        # conteneur absent ou arrêté : c'est un écart
    printf "%-6s %-11s %-15s %-4s %-5s %-6s %-6s %s\\n" "$id" "$src" "$dst" "$proto" "$port" "$attendu" "-" "ÉCART (conteneur introuvable)"
    continue
  fi
  if [ "$proto" = "udp" ] && [ "$port" = "53" ]; then
    nsenter -t "$pid" -n dig @"$dst" www.labo.test +time=2 +tries=1 >/dev/null 2>&1 && r=pass || r=block
  else
    out=$(nsenter -t "$pid" -n nc -zv -w 2 "$dst" "$port" 2>&1); rc=$?
    if [ $rc -eq 0 ]; then r=pass                                   # connexion établie
    elif echo "$out" | grep -qi refused; then r=pass               # RST reçu : le pare-feu a laissé passer (port fermé)
    else r=block; fi                                               # délai dépassé : paquet jeté par le pare-feu
  fi
  [ "$r" = "$attendu" ] && v=CONFORME || v=ÉCART
  printf "%-6s %-11s %-15s %-4s %-5s %-6s %-6s %s\\n" "$id" "$src" "$dst" "$proto" "$port" "$attendu" "$r" "$v"
done | tee /tmp/test_flux.txt
echo "Conformes : $(grep -c CONFORME /tmp/test_flux.txt) · Écarts : $(grep -c ÉCART /tmp/test_flux.txt)"
! grep -q ÉCART /tmp/test_flux.txt`;

G.roles.parefeu = {
intro:`<p><b>OPNsense 26.7 a changé plusieurs pages</b> : affectation des interfaces (nouvelle page), règles (nouvelle interface, anciennes pages déplacées dans un greffon), « Port Forward » renommé « Destination NAT ». Les chemins marqués « à vérifier » doivent être confirmés sur votre écran. Si un menu n'existe pas, cherchez l'option par son nom dans la barre de recherche de l'interface.</p><p><b>Mise à jour du 17 novembre.</b> Les étapes 1 à 5 (installation, WAN/MGMT, VLAN 10 et 20, NAT, règles FA, journalisation) ont été réalisées par le rôle 1 pendant la préparation de l'hôte : vérifie-les, prends tes captures, puis enchaîne sur l'étape 6 (moteur C2). Cartes d'OPNsense sous VMware : <code>em0</code> = WAN 203.0.113.1, <code>em1</code> = trunk (porte <code>vlan01</code> LAN et <code>vlan02</code> DMZ), <code>em2</code> = MGMT 10.99.0.1, <code>em3</code> = carte NAT débranchée. Il n'y a <b>pas</b> d'interface CONF : l'interface web s'ouvre sur <code>https://10.99.0.1</code> depuis Windows, ou sur <code>https://127.0.0.1:8443</code> par le tunnel. Demande au rôle 1 ton propre compte OPNsense (le mot de passe root n'est pas partagé).</p>`,
steps:[
/* 1 */ {titre:"Installer OPNsense 26.7 et affecter les interfaces", quand:"14 nov.", fait:true,
 maj:`<p>Fait par le rôle 1 le 14 novembre, dans VMware. Ce qui diffère du texte d'origine : console de la VM dans VMware (ou VNC 5901 par le tunnel) au lieu de noVNC ; cartes <code>em0…em3</code> au lieu de <code>vtnet0…vtnet3</code> ; pas de carte CONF : le « LAN » d'OPNsense a été placé sur <code>em2</code> (MGMT, 10.99.0.1) puis renommé MGMT, ce qui n'ouvre l'interface web que du côté gestion.</p>`,
 but:"OPNsense 26.7 installé dans p6-opnsense, chaque carte affectée à la bonne interface, l'interface web joignable depuis le réseau de gestion seulement.",
 pourquoi:"Section 4.1 : le pare-feu est le seul point de passage entre zones (C1). L'interface web ne doit jamais être joignable depuis le WAN ni depuis les zones évaluées.",
 prereq:{etapes:["chef-3"], modules:[5,8]},
 schema:"proxmox-topo",
 ui:[
  {path:"Console de p6-opnsense (VMware, ou TightVNC sur 127.0.0.1::5901)", faire:"Ce qui a été fait : démarrage sur l'ISO, connexion <code>installer</code> / <code>opnsense</code>, clavier français, installation <b>UFS</b> sur le disque de 20 Go (da0), mot de passe root (connu du rôle 1 seulement), arrêt, ISO éjectée dans VMware."},
  {path:"Console OPNsense › 1) Assign interfaces", faire:"Pas de LAGG, pas de VLAN à ce stade. WAN = <code>em0</code>, LAN = <code>em2</code> (devenu MGMT)."},
  {path:"Console OPNsense › 2) Set interface IP address", faire:"LAN (MGMT) : <code>10.99.0.1/24</code>, sans DHCP ni IPv6. WAN : <code>203.0.113.1/24</code>."},
  {path:"Assistant web (https://10.99.0.1)", faire:"Nom <code>fw-p6</code>, domaine <code>p6.lab</code>, fuseau Africa/Tunis, Override DNS décoché ; WAN statique (passerelle fictive 203.0.113.254), « Block private » et « Block bogon » décochés ; DHCP du LAN désactivé."}
 ],
 config:[{t:"Vérifier une carte par sa MAC (shell OPNsense, option 8)", code:`ifconfig em2 | grep ether      # 00:0c:29:57:1f:b1 = carte 3 de VMware, reliée à VMnet9`}],
 tester:[
  {cmd:"# Console OPNsense, option 8 (shell)\nifconfig -l", sortie:"em0 em1 em2 em3 lo0 pflog0 pfsync0 vlan01 vlan02", sens:[["em0 … em3","les 4 cartes VMware sont reconnues"],["vlan01 vlan02","LAN et DMZ (étape 3)"],["pflog0","l'interface interne qui alimente filterlog"]]},
  {t:"Depuis Windows (PC du laboratoire)", cmd:"Test-NetConnection 10.99.0.1 -Port 443", sortie:"TcpTestSucceeded : True", sens:[["True","l'interface web répond sur MGMT ; depuis Kali, le même test échoue (tests-cloisonnement.sh)"]]}
 ],
 pannes:[
  ["L'interface web ne répond plus depuis Windows","Règle « Admin du pare-feu » (port source ≠ any) ou anti-lockout","Console › 8 Shell › <code>pfctl -d</code>, corriger la règle, Apply, <code>pfctl -e</code> (rôle 1, étape 5)."],
  ["Les cartes ne sont pas dans l'ordre attendu","Comparer les MAC (<code>ifconfig em0</code>) avec le fichier .vmx","Réaffecter à la console (option 1)."]
 ],
 fini:["OPNsense 26.7 installé, ISO retirée","em0 = WAN, em2 = MGMT, em1 porte les VLAN","Interface web accessible depuis Windows (10.99.0.2) et par le tunnel, pas depuis Kali"],
 livrer:"Version exacte (System › Firmware) notée dans <code>docs/versions.md</code>. Capture <code>parefeu-01-version.png</code>."
},
/* 2 */ {titre:"Configurer WAN, MGMT, offloading, SSH et l'heure", quand:"14–15 nov.", fait:true,
 maj:`<p>Fait par le rôle 1 (14-16 novembre). Différences : MGMT est sur <code>em2</code> (ancien « LAN » renommé), le trunk sur <code>em1</code>. Le <b>SSH d'OPNsense est désactivé</b> : l'administration passe par l'interface web. Sur MGMT, une seule règle : « Admin du pare-feu », source alias <code>ADMINS</code> (10.99.0.2 Windows, 10.99.0.10 collecteur), destination This Firewall, TCP 22 et 443, <b>port source any</b>. Les règles « Default allow LAN » de MGMT sont désactivées et la règle anti-lockout aussi (elle ouvrait la console à Kali). Heure : serveur 10.99.0.10 seul. Déchargements CRC, TSO, LRO et VLAN Hardware Filtering désactivés, redémarrage fait. Test à faire : <code>Test-NetConnection 10.99.0.1 -Port 443</code> depuis Windows = True ; depuis Kali, <code>tests-cloisonnement.sh</code> le prouve bloqué.</p>`,
 but:"WAN en 203.0.113.1/24, MGMT en 10.99.0.1/24, offloading désactivé, SSH accessible depuis le collecteur seulement, heure synchronisée.",
 pourquoi:"Section 4.1 (adresses), section 5.3 (offloading désactivé pour le mode IPS), section 2.3 (MGMT : SSH depuis le collecteur, syslog et NTP vers lui). Le WAN simulé utilise une plage réservée : l'option bogons le bloquerait (écart E04).",
 prereq:{etapes:["parefeu-1"], modules:[4,7]},
 ui:[
  {path:"Interfaces › [WAN]", verif:true, faire:"IPv4 : statique, <code>203.0.113.1/24</code>. Passerelle amont : aucune. <b>Décocher</b> « Block private networks » et « Block bogon networks » (203.0.113.0/24 est une plage réservée, présente dans les listes bogons). IPv6 : aucun."},
  {path:"Interfaces › Assignments", verif:"nouvelle page en 26.7", faire:"Activer l'interface de <code>em2</code>, description <code>MGMT</code>, IPv4 statique <code>10.99.0.1/24</code>, sans passerelle. Activer celle de <code>em1</code>, description <code>TRUNK</code>, IPv4 : aucun (elle servira au rôle 3 pour l'IDS)."},
  {path:"Interfaces › Settings", faire:"Désactiver le déchargement matériel : cocher « Disable hardware CRC », « Disable hardware TSO », « Disable hardware LRO », et désactiver « VLAN Hardware Filtering ». Enregistrer puis redémarrer (manuel IPS d'OPNsense)."},
  {path:"System › Settings › Administration", verif:true, faire:"Secure Shell : activé, connexion root autorisée par clé seulement, interface d'écoute : MGMT (et CONF si besoin). Ajouter la clé publique du collecteur au compte root (System › Access › Users)."},
  {path:"Services › Network Time › General", verif:true, faire:"Serveur de temps : <code>10.99.0.10</code> (le collecteur, rôle 6). Supprimer les serveurs publics."},
  {path:"Firewall › Rules › MGMT", verif:"nouvelle page des règles en 26.7", faire:"Une seule règle : pass, TCP, source <code>10.99.0.10</code>, destination « This Firewall », port 22, journalisée. Rien d'autre : syslog et NTP sont émis par OPNsense lui-même (trafic sortant du pare-feu autorisé)."}
 ],
 tester:[
  {t:"Depuis le collecteur", cmd:"ssh root@10.99.0.1 'ifconfig em0 | grep -E \"inet|options\"; ntpq -p 2>/dev/null | tail -1'", sortie:"\toptions=…<…,LINKSTATE>   (sans TXCSUM, TSO4, LRO)\n\tinet 203.0.113.1 netmask 0xffffff00 broadcast 203.0.113.255\n*10.99.0.10      .LOCL.  …", sens:[["options sans TXCSUM, TSO4, LRO","offloading bien désactivé"],["inet 203.0.113.1","adresse WAN correcte"],["*10.99.0.10","l'heure vient du collecteur"]]},
  {t:"Depuis Kali", cmd:"nc -zv -w 2 10.99.0.1 22\nnc -zv -w 2 10.99.0.1 443", sortie:"nc: connect to 10.99.0.1 port 22 (tcp) timed out\nnc: connect to 10.99.0.1 port 443 (tcp) timed out", sens:[["timed out","OPNsense a jeté le paquet : seuls Windows (10.99.0.2) et le collecteur (10.99.0.10) peuvent administrer le pare-feu. Le filtre local de Kali (table p6_ssh) ne bloque que l'entrée : il ne fausse pas ce test"]]}
 ],
 pannes:[
  ["Kali ne joint rien sur 203.0.113.1","Firewall › Log Files › Live View : lignes « bogon » ou « private »","Décocher les deux options sur WAN (écart E04)."],
  ["SSH refusé depuis le collecteur","Clé absente ou SSH n'écoute pas sur MGMT","Ajouter la clé, choisir MGMT comme interface d'écoute."],
  ["Les options TXCSUM restent visibles","Redémarrage non fait","Redémarrer p6-opnsense après le changement."]
 ],
 fini:["WAN 203.0.113.1/24, sans bogons, sans passerelle","MGMT 10.99.0.1/24 ; TRUNK affecté sans adresse","Offloading désactivé (vérifié par ifconfig)","SSH : collecteur oui, Kali non","Heure synchronisée sur 10.99.0.10"],
 livrer:"Message au rôle 3 : « OPNsense prêt pour le test netmap : WAN et TRUNK affectés, offloading désactivé ». Export de la configuration (System › Configuration › Backups) commité dans <code>opnsense/config-base.xml</code>."
},
/* 3 */ {titre:"État A : créer les VLAN 10 et 20", quand:"16 nov.", fait:true,
 maj:`<p>Fait par le rôle 1. <code>vlan01</code> (étiquette 10, LAN 10.10.0.1/24) et <code>vlan02</code> (étiquette 20, DMZ 10.20.0.1/24) sur le parent <code>em1</code>. Côté hôte Docker, les sous-interfaces sont <code>ens33.10</code> et <code>ens33.20</code>. Pas de « pont VLAN-aware » à vérifier : le segment LAN <code>p6-trunk</code> de VMware laisse passer les étiquettes.</p>`,
 but:"Deux sous-interfaces VLAN sur em1 : LAN (10.10.0.1/24, VLAN 10) et DMZ (10.20.0.1/24, VLAN 20).",
 pourquoi:"Section 4.1 : DMZ 10.20.0.0/24 VLAN 20, LAN 10.10.0.0/24 VLAN 10, passerelles en .1. Section 6 : un trunk 802.1Q transporte tous les VLAN internes.",
 prereq:{etapes:["parefeu-2","chef-2"], texte:"Pour le test : le rôle 4 a créé ses réseaux macvlan de l'état A.", modules:[3,2]},
 schema:"trunk-tag", schemaSt:"A",
 ui:[
  {path:"Interfaces › Devices › VLAN", verif:"anciennement Interfaces › Other Types › VLAN", faire:"Ajouter : parent <code>em1</code>, tag <code>10</code>, description <code>LAN-A</code>. Puis parent <code>em1</code>, tag <code>20</code>, description <code>DMZ-A</code>."},
  {path:"Interfaces › Assignments", verif:true, faire:"Ajouter les deux périphériques VLAN, les activer. Description <code>LAN</code> → IPv4 statique <code>10.10.0.1/24</code>. Description <code>DMZ</code> → IPv4 statique <code>10.20.0.1/24</code>. Pas de passerelle."}
 ],
 tester:[
  {t:"Shell OPNsense", cmd:"ifconfig -l\nifconfig | grep -A3 'vlan: 10'", sortie:"em0 em1 em2 em3 lo0 pflog0 pfsync0 vlan01 vlan02\n\tvlan: 10 vlanproto: 802.1q vlanpcp: 0 parent interface: em1\n\tinet 10.10.0.1 netmask 0xffffff00 …", sens:[["vlan0.10","nom du périphérique VLAN (le format exact dépend de la version)"],["parent interface: em1","la sous-interface s'appuie bien sur le trunk"]]},
  {t:"Avec un conteneur de test du rôle 4", cmd:"# sur l'hôte Docker\ndocker run --rm --network lan --ip 10.10.0.50 projet6/outils ping -c 2 10.10.0.1\n# sur OPNsense, en même temps (les options avant le filtre)\ntcpdump -nei em1 -c 2 vlan 10 and icmp", sortie:"64 bytes from 10.10.0.1: icmp_seq=1 ttl=64 time=0.4 ms\n… ethertype 802.1Q (0x8100), length 102: vlan 10, p 0, ethertype IPv4, 10.10.0.50 > 10.10.0.1: ICMP echo request", sens:[["vlan 10","la trame arrive étiquetée sur le trunk"],["ttl=64","la passerelle répond (en état A, FA4 autorise le LAN vers le pare-feu)"]]}
 ],
 pannes:[
  ["Pas de réponse, et tcpdump ne voit rien","Paramètres de la VM dans VMware : la carte du trunk est-elle sur le segment <code>p6-trunk</code> ?","Mauvais segment : rôle 1, étape 2."],
  ["tcpdump voit la requête mais pas de réponse","Interface VLAN non activée ou sans adresse","Activer l'interface, vérifier 10.10.0.1/24."],
  ["« overlapping networks » à l'enregistrement","Une autre interface porte déjà ce réseau","Interfaces › Overview : aucune autre interface ne doit être en 10.10.0.0/24 ou 10.20.0.0/24."]
 ],
 fini:["VLAN 10 et 20 sur em1","LAN 10.10.0.1/24 et DMZ 10.20.0.1/24 actifs","Un conteneur de test ping sa passerelle, trame vue étiquetée sur em1"],
 livrer:"Message au rôle 4 : « VLAN 10 et 20 actifs, passerelles 10.10.0.1 et 10.20.0.1 ». Export de la configuration commité."
},
/* 4 */ {titre:"Configurer le NAT de destination (FA1, FA2, FA6)", quand:"16–17 nov.", fait:true,
 maj:`<p>Fait par le rôle 1 : FA1 HTTP, FA1 HTTPS et FA2 DNS actifs et testés, FA6 iperf3 désactivée (alias <code>CLIENT_EXT</code> et <code>PORT_IPERF3</code>). Choix final pour FA1 et FA2 : « Firewall rule » = <b>Pass</b>. Avec « Register rule », la règle de filtrage associée autorisait « any → 10.20.0.10:80 » : Kali, dont la passerelle est le pare-feu, joignait le serveur directement, sans traduction (trouvaille à citer dans ta section du rapport).</p>`,
 but:"Les ports publiés sur 203.0.113.1 redirigés vers nginx et bind9 ; la redirection iperf3 prête mais désactivée.",
 pourquoi:"Section 4.2 : FA1 (TCP 80, 443 → nginx, NAT depuis 203.0.113.1), FA2 (UDP/TCP 53 → bind9), FA6 (TCP 5201 depuis client-ext, activée seulement pendant les mesures). Identiques en état B (F01, F02, F09).",
 prereq:{etapes:["parefeu-3"], modules:[4]},
 schema:"nat-steps", schemaSt:"A",
 ui:[{path:"Firewall › NAT › Destination NAT", verif:"anciennement « Port Forward »", faire:"Ajouter quatre règles sur l'interface WAN, destination « WAN address » :<br>· TCP 80 → <code>10.20.0.10</code> port 80, description « FA1 / F01 web »<br>· TCP 443 → <code>10.20.0.10</code> port 443, « FA1 / F01 web »<br>· TCP/UDP 53 → <code>10.20.0.40</code> port 53, « FA2 / F02 dns »<br>· TCP 5201, source <code>203.0.113.10</code> → <code>10.20.0.10</code> port 5201, « FA6 / F09 iperf3 », <b>désactivée</b><br>Règle de filtrage associée : <b>aucune</b>. Les règles de filtrage sont écrites à l'étape 5 et générées par C2 : on garde la maîtrise de leur nom, de leur ordre et de leur journalisation."}],
 tester:[
  {t:"Shell OPNsense", cmd:"pfctl -s nat | grep rdr", sortie:"rdr on em0 inet proto tcp from any to 203.0.113.1 port = http -> 10.20.0.10\nrdr on em0 inet proto tcp from any to 203.0.113.1 port = https -> 10.20.0.10\nrdr on em0 inet proto { tcp udp } from any to 203.0.113.1 port = domain -> 10.20.0.40", sens:[["rdr on em0","redirection appliquée aux paquets entrant par le WAN"],["-> 10.20.0.10","adresse interne de nginx ; la règle de filtrage devra viser cette adresse"],["pas de ligne 5201","FA6 est bien désactivée"]]}
 ],
 pannes:[
  ["Kali obtient un délai dépassé sur 443","Rdr présent mais aucune règle de filtrage","Normal avant l'étape 5 : la règle FA1 doit autoriser 10.20.0.10:443."],
  ["La règle WAN autorise 203.0.113.1 et rien ne passe","La redirection est appliquée avant le filtrage","Écrire la règle vers l'adresse interne (module 4)."],
  ["La règle iperf3 est active hors mesure","<code>pfctl -s nat | grep 5201</code>","La désactiver : elle n'est allumée que pendant les mesures du rôle 3."]
 ],
 fini:["Trois redirections actives (80, 443, 53), une désactivée (5201)","Aucune règle de filtrage associée automatique"],
 livrer:"Export de la configuration commité. Message au rôle 3 : « redirection 5201 prête, à activer seulement pendant les mesures iperf3 »."
},
/* 5 */ {titre:"Écrire les règles FA et activer la journalisation", quand:"17 nov.", fait:true,
 maj:`<p>Fait par le rôle 1 : FA3 « DMZ net → POSTGRES:PORT_PGSQL » sur DMZ, FA4 « LAN net → any » sur LAN (recréée le 16), refus par défaut journalisé, envoi des journaux vers 10.99.0.10 UDP 514 (lignes <code>filterlog</code> reçues sur le collecteur). Preuve : <code>tests.sh</code> 12/12 sur l'hôte Docker. Référence de l'état A : instantané OPNsense <code>07-vnc</code>. Pense à exporter la configuration (System › Configuration › Backups) dans <code>opnsense/config-etat-A.xml</code>.</p>`,
 but:"La matrice FA appliquée, chaque règle journalisée, le refus par défaut journalisé, et filterlog envoyé au collecteur.",
 pourquoi:"Section 4.2 (matrice FA) et section 7.1 : première correspondance, pass journalisé, block final journalisé dans filterlog, export syslog vers C9.",
 prereq:{etapes:["parefeu-4","analyse-2"], modules:[5]},
 schema:"state-table", schemaSt:"A",
 ui:[
  {path:"Firewall › Rules › WAN", verif:"nouvelle page des règles", faire:"FA1 : pass, TCP, any → <code>10.20.0.10</code>, ports 80 et 443, journaliser. FA2 : pass, TCP/UDP, any → <code>10.20.0.40</code>, port 53, journaliser. FA6 : pass, TCP, <code>203.0.113.10</code> → <code>10.20.0.10</code>, port 5201, journaliser, <b>désactivée</b>."},
  {path:"Firewall › Rules › DMZ", verif:true, faire:"FA3 : pass, TCP, DMZ net → <code>10.10.0.70</code>, port 5432, journaliser."},
  {path:"Firewall › Rules › LAN", verif:true, faire:"FA4 : pass, tout protocole, LAN net → any, journaliser (la règle « LAN vers tout » de l'état A)."},
  {path:"Firewall › Settings › Advanced (ou System › Settings › Logging)", verif:"emplacement de l'option", faire:"Activer la journalisation des paquets bloqués par la règle par défaut : c'est FA5 (puis F99)."},
  {path:"System › Settings › Logging › Remote / Targets", verif:"page modifiée en 26.7.9", faire:"Nouvelle cible : transport UDP (ou TCP, écart E13), applications <code>filter (filterlog)</code> et <code>suricata</code>, hôte <code>10.99.0.10</code>, port 514. Garder aussi la journalisation locale si l'option est proposée."}
 ],
 tester:[
  {t:"Shell OPNsense", cmd:"pfctl -sr | grep -c pass\ntail -n 3 /var/log/filter/latest.log", sortie:"… (nombre de règles pass, dont les règles système)\n<134>1 2026-11-17T15:02:11+01:00 opnsense filterlog … 77,,,fae5…,em0,match,block,in,4,0x0,,64,…,tcp,…,203.0.113.66,203.0.113.1,40001,22,0,S,…", sens:[["match,block,in","paquet bloqué par la règle par défaut (FA5), en entrée"],["203.0.113.66 … 22","Kali vers le port 22 non publié"]]},
  {t:"Depuis Kali", cmd:"nc -zv -w 2 203.0.113.1 443\nnc -zv -w 2 203.0.113.1 22", sortie:"Connection to 203.0.113.1 443 port [tcp/https] succeeded!   (si nginx tourne)\nnc: connect to 203.0.113.1 port 22 (tcp) timed out", sens:[["succeeded","FA1 + redirection"],["timed out","refus par défaut, en silence (block) : nmap verra « filtered »"]]},
  {t:"Sur le collecteur (rôle 6)", cmd:"tail -n 2 /var/log/remote/opnsense.log", sortie:"… filterlog[…]: 77,,,fae5…,em0,match,block,in,4,…", sens:[["filterlog","les journaux arrivent bien sur le collecteur"]]}
 ],
 pannes:[
  ["Aucune ligne pour les paquets bloqués","Journalisation de la règle par défaut désactivée","Activer l'option (FA5 doit être journalisée)."],
  ["Le collecteur ne reçoit rien","<code>tcpdump -ni em2 port 514</code> sur OPNsense","Vérifier la cible syslog, et le pare-feu du collecteur (rôle 1, étape 5)."],
  ["Une règle semble ignorée","Elle est sur la mauvaise interface ou après une règle plus générale","Les règles s'écrivent sur l'interface d'entrée ; première correspondance (module 5)."]
 ],
 fini:["FA1 à FA4 actives et journalisées, FA6 désactivée","Le refus par défaut est journalisé","filterlog arrive sur le collecteur"],
 livrer:"Export <code>opnsense/config-etat-A.xml</code> (provisoire) commité. Message au rôle 6 : « filterlog part vers 10.99.0.10:514 ; voici une ligne d'exemple »."
},
/* 6 */ {titre:"Écrire le moteur de politique C2", quand:"17–18 nov.",
 but:"Un programme qui valide flows.csv, en déduit les règles dans le bon ordre et écrit tests.csv.",
 pourquoi:"Section 1 (décision 2 : la politique est une donnée), section 7.2 et figure 11 : valider, grouper par interface d'entrée, trier du plus spécifique au plus général, pass journalisé par flux, block final ; plan de test « doit passer », « doit être bloqué », « mauvais port ».",
 prereq:{texte:"Fichiers de la page <a href='#contrats'>Contrats</a> : flows.csv, segments.csv, hosts.csv.", modules:[5,6]},
 schema:"matrix-editor",
 config:[
  {t:"Programme complet", file:"politique/c2.py", code:R2_C2},
  {t:"Application des règles", verif:"API de la nouvelle page des règles", code:`# Méthode sûre (saisie guidée) : C2 écrit regles-B.md, un tableau ligne par ligne à reproduire
# dans Firewall › Rules, interface par interface, dans l'ordre indiqué. Comptez 30 min pour l'état B.
#
# Méthode automatique (facultative) : l'API d'OPNsense 26.7 gère les règles de la nouvelle page.
# Documentation : https://docs.opnsense.org (section « API », module firewall/filter).
# Créez une clé API (System › Access › Users › API keys) et testez d'abord UNE règle à la main
# avant d'automatiser. Le collecteur n'ayant que SSH vers OPNsense, appelez l'API en local :
#   ssh root@10.99.0.1 "curl -sk -u CLE:SECRET https://127.0.0.1/api/firewall/filter/searchRule"`}
 ],
 tester:[
  {cmd:"python3 politique/c2.py --etat A\npython3 politique/c2.py --etat B\nhead -5 politique/tests.csv", sortie:"AVERTISSEMENT FA4 : « any » contraire au moindre privilège\nÉtat A : 8 règles sur 3 interfaces, 8 tests, 1 avertissement(s).\nÉtat B : 16 règles sur 7 interfaces, 42 tests, 0 avertissement(s).\nid_test,src_host,dst_ip,proto,port,attendu,flux_ref\nT001,client-ext,203.0.113.1,tcp,80,pass,F01\nT002,client-ext,203.0.113.1,tcp,22,block,F01\nT003,client-ext,203.0.113.1,tcp,53,pass,F02\n…", sens:[["AVERTISSEMENT FA4","attendu : l'état A est volontairement large ; c'est la faiblesse mesurée"],["16 règles sur 7 interfaces","9 règles pass (F01 à F09) et un block final par interface ; ces nombres ont été obtenus avec les fichiers de la page Contrats"],["T002 … 22,block","test « mauvais port » de F01 : le port 22 de 203.0.113.1 doit rester fermé"],["tests.csv","C2 réécrit tests.csv pour l'état demandé : relancez-le avec --etat avant chaque test_flux"]]}
 ],
 pannes:[
  ["« n'est ni un segment ni un hôte »","Faute de frappe dans flows.csv","Le nom doit exister dans segments.csv ou hosts.csv (respecter les majuscules)."],
  ["Le test d'un flux WAN vise 10.20.0.10","Source WAN mal reconnue","La source doit être le segment « WAN » : C2 met alors l'adresse publique 203.0.113.1 dans le test."],
  ["Un flux autorisé est bloqué dans l'interface","Ordre de saisie différent de regles-*.md","Respecter l'ordre ; le block final toujours en dernier."]
 ],
 fini:["c2.py tourne pour A et B sans erreur","regles-A.md, regles-B.md, tests.csv produits et commités","Chaque interface a un block final journalisé"],
 livrer:"Commit de <code>politique/</code> (c2.py, CSV, regles-*.md, tests.csv). Message aux rôles 1 et 5 : « tests.csv disponible »."
},
/* 7 */ {titre:"Prouver l'état A avec test_flux", quand:"18–19 nov.",
 but:"100 % des tests de l'état A conformes, et la preuve que le mouvement latéral S-05 ne laisse aucune trace dans filterlog.",
 pourquoi:"Section 10.3, étape 1 : restaurer l'état et vérifier test_flux (100 % conforme) avant toute campagne. Jalon du 19 novembre.",
 prereq:{etapes:["parefeu-6","services-6"], modules:[5,11]},
 schema:"matrix-editor", schemaSt:"A",
 config:[{t:"Script à installer sur l'hôte Docker (exécuté en root)", file:"politique/test_flux.sh", code:R2_TF, note:"Dépendances sur l'hôte Docker : <code>apt install netcat-openbsd dnsutils</code> (pendant une fenêtre de configuration)."}],
 tester:[
  {t:"Depuis le collecteur (tests générés ici, exécutés sur l'hôte Docker)", cmd:"cd /opt/projet6 && python3 politique/c2.py --etat A\nscp politique/tests.csv politique/hosts.csv root@10.99.0.20:/opt/projet6/politique/\nssh root@10.99.0.20 'cd /opt/projet6 && ./politique/test_flux.sh politique/tests.csv politique/hosts.csv'", sortie:"ID     SOURCE      DESTINATION     PROTO PORT  ATTENDU OBTENU VERDICT\nT001   client-ext  203.0.113.1     tcp   443   pass   pass   CONFORME\nT002   client-ext  203.0.113.1     tcp   22    block  block  CONFORME\n…\nConformes : 8 · Écarts : 0", sens:[["CONFORME","le pare-feu applique la matrice pour ce test ; un « pass » peut aussi venir d'un refus du service (RST), qui prouve que le pare-feu a laissé passer"],["Écarts : 0","condition pour lancer la campagne A"]]},
  {t:"Preuve S-05 (état A)", cmd:"# hôte Docker\ndocker exec client-03 nc -zv -w 2 10.10.0.70 5432\n# OPNsense : couple source,destination tel qu'il apparaît dans la ligne filterlog\ngrep -c '10.10.0.12,10.10.0.70' /var/log/filter/latest.log", sortie:"Connection to 10.10.0.70 5432 port [tcp/postgresql] succeeded!\n0", sens:[["succeeded","la connexion aboutit"],["0","aucune ligne filterlog : le mouvement latéral est invisible pour pf (section 4.3)"]]}
 ],
 pannes:[
  ["Un test « pass » échoue","<code>tail /var/log/filter/latest.log</code> pendant le test","Règle manquante ou mal placée ; ou service non démarré (rôle 4)."],
  ["Un test « block » passe","Règle trop large, ou état existant","Vider les états de la source (<code>pfctl -k &lt;ip&gt;</code>) et relancer."],
  ["« ÉCART (conteneur introuvable) »","Conteneur arrêté, ou colonne conteneur de hosts.csv","<code>docker ps</code> ; aligner les noms sur compose.yml (rôle 4)."]
 ],
 fini:["test_flux A : 0 écart","Preuve S-05 sans filterlog sauvegardée dans docs/","config-etat-A.xml définitif commité"],
 livrer:"Commit de <code>opnsense/config-etat-A.xml</code> et du rapport de conformité <code>resultats/A/conformite.txt</code>. Message aux rôles 1 et 5 : « état A prêt, test_flux 100 % »."
},
/* 8 */ {titre:"État B : six segments et règles F01–F99", quand:"22–23 nov.",
 but:"Les VLAN 11, 12, 13, 21, 22, 23 avec leurs passerelles, et la matrice F appliquée interface par interface.",
 pourquoi:"Sections 5.1 et 5.2 : hôtes inchangés, seuls masque, passerelle et VLAN changent ; un flux autorisé = une justification ; tout le reste refusé et journalisé. Passer de A à B ne change jamais le câblage (section 6).",
 prereq:{etapes:["chef-7","attaques-5"], texte:"etat-A doit être figé avant toute modification.", modules:[2,3,5]},
 schema:"trunk-tag",
 ui:[
  {path:"Interfaces › Assignments", verif:true, faire:"Désactiver les interfaces LAN et DMZ de l'état A (ou retirer leur adresse) : sinon 10.10.0.1/26 chevaucherait 10.10.0.1/24."},
  {path:"Interfaces › Devices › VLAN", verif:true, faire:"Créer sur <code>em1</code> les tags 11, 12, 13, 21, 22, 23."},
  {path:"Interfaces › Assignments", verif:true, faire:"Affecter et activer, descriptions et adresses :<br>· LAN-USERS (11) <code>10.10.0.1/26</code> · LAN-DB (12) <code>10.10.0.65/27</code> · LAN-ADMIN (13) <code>10.10.0.97/27</code><br>· DMZ-WEB (21) <code>10.20.0.1/27</code> · DMZ-DNS (22) <code>10.20.0.33/27</code> · DMZ-APP (23) <code>10.20.0.65/27</code>"},
  {path:"Firewall › Rules › chaque interface", verif:true, faire:"Supprimer FA3 et FA4. Saisir les règles de <code>regles-B.md</code> dans l'ordre (F01–F09 ; F09 désactivée). Vérifier qu'aucune règle « net → any » ne subsiste."}
 ],
 config:[{t:"Après l'application : vider les états (console de p6-opnsense, pas en SSH)", code:`pfctl -F states     # supprime toutes les connexions en cours : les anciennes connexions de l'état A
                    # continueraient sinon à passer malgré les nouvelles règles
# Depuis SSH, cette commande couperait aussi votre propre session : utilisez la console de la VM (VMware, ou TightVNC sur 127.0.0.1::5901 par le tunnel).`}],
 tester:[{t:"Shell OPNsense", cmd:"ifconfig | grep -E 'vlan: (11|12|13|21|22|23)' | wc -l\nnetstat -rn -f inet | grep -E '10\\.(10|20)\\.0'", sortie:"6\n10.10.0.0/26        link#7   U   vlan0.11\n10.10.0.64/27       link#8   U   vlan0.12\n10.10.0.96/27       link#9   U   vlan0.13\n10.20.0.0/27        link#10  U   vlan0.21\n10.20.0.32/27       link#11  U   vlan0.22\n10.20.0.64/27       link#12  U   vlan0.23", sens:[["6","six sous-interfaces"],["routes connectées","OPNsense connaît les six réseaux sans route statique (module 4)"]]}],
 pannes:[
  ["« overlapping » à l'enregistrement de 10.10.0.1/26","L'interface LAN de l'état A porte encore 10.10.0.1/24","La désactiver d'abord."],
  ["Les conteneurs ne joignent plus leur passerelle","Réseaux Docker encore en /24 (rôle 4 pas encore basculé)","Coordonner : le rôle 4 bascule juste après vous (étape 8 du rôle 4)."],
  ["Un ancien flux passe encore","États hérités","<code>pfctl -F states</code> depuis la console."]
 ],
 fini:["Six interfaces VLAN avec les passerelles de la section 5.1","Règles F01–F08 actives, F09 désactivée, refus par défaut journalisé sur chaque interface","Plus aucune règle de l'état A","Table d'états vidée"],
 livrer:"Message au rôle 4 : « VLAN B actifs, à vous pour les réseaux Docker » ; au rôle 3 : « règles B en place »."
},
/* 9 */ {titre:"Prouver l'état B avec test_flux et livrer", quand:"23–24 nov.",
 but:"100 % des tests de l'état B conformes, S-05 désormais bloqué et journalisé, configuration B commitée.",
 pourquoi:"Jalon du 24 novembre (section 12) : test_flux conforme en B. Section 8.3 : S-05 doit être refusé et journalisé.",
 prereq:{etapes:["parefeu-8","services-8"], modules:[5]},
 schema:"matrix-editor",
 tester:[
  {t:"Depuis le collecteur", cmd:"cd /opt/projet6 && python3 politique/c2.py --etat B\nscp politique/tests.csv politique/hosts.csv root@10.99.0.20:/opt/projet6/politique/\nssh root@10.99.0.20 'cd /opt/projet6 && ./politique/test_flux.sh politique/tests.csv politique/hosts.csv' | tail -1", sortie:"État B : 16 règles sur 7 interfaces, 42 tests, 0 avertissement(s).\nConformes : 42 · Écarts : 0", sens:[["Écarts : 0","condition de la campagne B"]]},
  {t:"Preuve S-05 (état B)", cmd:"# hôte Docker\ndocker exec client-03 nc -zv -w 2 10.10.0.70 5432\n# OPNsense\ngrep '10.10.0.12,10.10.0.70' /var/log/filter/latest.log | tail -1", sortie:"nc: connect to 10.10.0.70 port 5432 (tcp) timed out\n… vlan0.11,match,block,in,4,…,tcp,60,10.10.0.12,10.10.0.70,…,5432,0,S,…", sens:[["timed out","la connexion n'aboutit plus"],["vlan0.11,match,block","bloqué par le refus par défaut sur l'interface LAN-USERS : c'est F99"]]}
 ],
 pannes:[
  ["T0xx « admin → console » échoue","F08 doit viser 10.10.0.97, la passerelle de LAN-ADMIN","Corriger la destination de F08."],
  ["Le DNS de client-01 échoue","F06 en TCP au lieu d'UDP","F06 : UDP 53 (écart E16)."],
  ["Tous les tests WAN échouent","Rôle 3 a activé Suricata en IPS sans réglage","Vérifier EVE (« blocked ») ; repasser en alert pendant test_flux."]
 ],
 fini:["test_flux B : 0 écart","Preuve S-05 bloqué + filterlog sauvegardée","<code>opnsense/config-etat-B.xml</code> commité"],
 livrer:"Commit de la configuration B et de <code>resultats/B/conformite.txt</code>. Message aux rôles 1, 3 et 5 : « état B conforme »."
}
],
demo:{intro:"Ce que vous montrez en 2 minutes : la politique est une donnée, et le pare-feu l'applique.", etapes:["Ouvrir <code>flows.csv</code> : une ligne F04, sa justification.","Lancer <code>c2.py --etat B</code> : règles et tests générés.","Lancer <code>test_flux</code> : 0 écart.","Montrer la ligne filterlog du blocage de S-05 sur l'interface LAN-USERS."], secours:"capture de la sortie de test_flux et de la ligne filterlog."},
questions:[
 ["Pourquoi la règle WAN vise-t-elle 10.20.0.10 et non 203.0.113.1 ?","Parce que pf applique la redirection (Destination NAT) avant d'évaluer les règles de filtrage : la règle voit déjà l'adresse interne. Une règle vers 203.0.113.1 ne correspondrait jamais au trafic redirigé."],
 ["Comment savez-vous que le pare-feu applique bien la matrice ?","test_flux rejoue tests.csv, généré par C2 à partir de flows.csv, depuis l'espace réseau de chaque conteneur source. Nous exigeons 0 écart avant chaque campagne, et nous vidons les états après chaque changement de règles."],
 ["Pourquoi les règles sont-elles écrites sur l'interface d'entrée ?","Les règles d'OPNsense filtrent en entrée de l'interface. F04 (API → base) s'écrit donc sur DMZ-APP. C'est aussi pourquoi C2 groupe les flux par segment source."],
 ["Que se passe-t-il pour une connexion ouverte quand vous changez une règle ?","Elle continue grâce à son état. C'est pourquoi on vide la table d'états après le passage en état B, et pourquoi l'orchestrateur exécute pfctl -k entre deux exécutions."],
 ["Pourquoi désactiver « Block bogon networks » sur le WAN ?","203.0.113.0/24 est une plage réservée à la documentation, présente dans les listes bogons. Cochée, l'option jetterait tout le trafic du WAN simulé avant nos règles. C'est une adaptation au laboratoire, documentée (écart E04)."]
]
};
