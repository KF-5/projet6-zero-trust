#!/bin/bash
# Projet 6 - etape 7 : test de cloisonnement depuis Kali (attaquant externe)
# Usage : bash tests-cloisonnement.sh      (aucune modification, lecture seule)
# "attendu" = ce que la conception exige (rapport : ENF1 confinement, section 4.2, hypotheses MGMT)
ok=0; ko=0
t() {  # $1 description, $2 attendu (passe|bloque), $3... commande
  local d="$1" a="$2"; shift 2
  if timeout 6 "$@" >/dev/null 2>&1; then o=passe; else o=bloque; fi
  if [ "$o" = "$a" ]; then r=OK; ok=$((ok+1)); else r=ECART; ko=$((ko+1)); fi
  printf "%-6s %-62s attendu=%-7s obtenu=%s\n" "$r" "$d" "$a" "$o"
}

echo "--- 1. Confinement : rien ne sort du labo (ENF1) ---"
t "ping box domestique 192.168.1.1"                  bloque ping -c 2 -W 2 192.168.1.1
t "ping Internet 8.8.8.8"                            bloque ping -c 2 -W 2 8.8.8.8
t "DNS Internet (dig @8.8.8.8)"                      bloque dig +time=2 +tries=1 @8.8.8.8 debian.org
t "HTTP Internet (deb.debian.org par adresse)"       bloque curl -s -m 4 http://151.101.2.132/
t "ping Windows 10.99.0.2 (via MGMT)"                bloque ping -c 2 -W 2 10.99.0.2
t "Windows 10.99.0.2:445 (partages, via MGMT)"       bloque nc -z -w 3 10.99.0.2 445
t "Windows 10.99.0.2:3389 (bureau a distance)"       bloque nc -z -w 3 10.99.0.2 3389

echo "--- 2. MGMT : Kali ne doit administrer personne ---"
t "OPNsense 10.99.0.1:443 (console web via MGMT)"    bloque nc -z -w 3 10.99.0.1 443
t "OPNsense 10.99.0.1:22 (SSH via MGMT)"             bloque nc -z -w 3 10.99.0.1 22
t "p6-docker 10.99.0.20:22 (SSH via MGMT)"           bloque nc -z -w 3 10.99.0.20 22
t "collecteur 10.99.0.10:22 (SSH via MGMT)"          bloque nc -z -w 3 10.99.0.10 22

echo "--- 3. Flux de l'etat A vus depuis le WAN ---"
t "FA1 https://203.0.113.1/api/sante"                passe  curl -skf -m 5 https://203.0.113.1/api/sante
t "FA1 http://203.0.113.1/"                          passe  curl -sf -m 5 http://203.0.113.1/
t "FA2 DNS @203.0.113.1 www.p6.lab"                  passe  sh -c 'dig +short +time=3 +tries=1 @203.0.113.1 www.p6.lab | grep -qx 203.0.113.1'
t "FA5 OPNsense WAN 203.0.113.1:22"                  bloque nc -z -w 3 203.0.113.1 22
t "FA5 OPNsense WAN 203.0.113.1:5432"                bloque nc -z -w 3 203.0.113.1 5432
t "FA5 acces direct DMZ 10.20.0.10:80 (sans NAT)"    bloque nc -z -w 3 10.20.0.10 80
t "FA5 acces direct base 10.10.0.70:5432"            bloque nc -z -w 3 10.10.0.70 5432
t "FA6 iperf3 203.0.113.1:5201 (regle desactivee)"   bloque nc -z -w 3 203.0.113.1 5201

echo
echo "Resultat : $ok conforme(s), $ko ecart(s)."
