#!/bin/bash
# Projet 6 - verification rapide de l'etat A depuis les conteneurs.
# Usage : sudo bash tests.sh
# "attendu" = comportement prevu par la matrice de l'etat A (rapport, section 4.2).
cd "$(dirname "$0")"
ok=0; ko=0

test_flux() {  # $1 description, $2 attendu (passe|bloque), $3 conteneur, $4... commande
  local desc="$1" attendu="$2" c="$3"; shift 3
  if timeout 8 docker exec "$c" "$@" >/dev/null 2>&1; then obtenu=passe; else obtenu=bloque; fi
  if [ "$obtenu" = "$attendu" ]; then r="OK"; ok=$((ok+1)); else r="ECART"; ko=$((ko+1)); fi
  printf "%-6s %-58s attendu=%-7s obtenu=%s\n" "$r" "$desc" "$attendu" "$obtenu"
}

echo "--- Services de base ---"
test_flux "api -> postgres:5432 (FA3, l'API lit la base)"           passe api \
  python3 -c 'import socket; socket.create_connection(("10.10.0.70", 5432), 3)'
test_flux "client-ext -> https://203.0.113.1/api/sante (FA1, NAT)" passe client-ext \
  curl -skf --max-time 5 https://203.0.113.1/api/sante
test_flux "client-ext -> http://203.0.113.1/ (FA1, NAT)"           passe client-ext \
  curl -sf --max-time 5 http://203.0.113.1/
test_flux "client-ext -> DNS 203.0.113.1 www.p6.lab (FA2, NAT)"    passe client-ext \
  sh -c 'dig +short +time=3 +tries=1 @203.0.113.1 www.p6.lab | grep -q 203.0.113.1'
test_flux "client-01 -> https://10.20.0.10/api/produits (FA4)"     passe client-01 \
  curl -skf --max-time 5 https://10.20.0.10/api/produits
test_flux "client-01 -> DNS 10.20.0.40 (FA4)"                      passe client-01 \
  sh -c 'dig +short +time=3 +tries=1 @10.20.0.40 www.p6.lab | grep -q 203.0.113.1'

echo "--- Faiblesses attendues de l'etat A (rapport, section 4.3) ---"
test_flux "dmz-pivot -> postgres:5432 (FA3 trop large)"            passe dmz-pivot \
  nc -z -w 3 10.10.0.70 5432
test_flux "client-03 -> postgres:5432 (meme L2, invisible pour pf)" passe client-03 \
  nc -z -w 3 10.10.0.70 5432
test_flux "client-03 -> OPNsense 10.10.0.1:443 (FA4 : admin joignable)" passe client-03 \
  nc -z -w 3 10.10.0.1 443

echo "--- Doit etre bloque (FA5) ---"
test_flux "client-ext -> postgres 10.10.0.70:5432 (WAN -> LAN)"    bloque client-ext \
  nc -z -w 3 10.10.0.70 5432
test_flux "dmz-pivot -> client-01 10.10.0.10:22 (DMZ -> LAN)"      bloque dmz-pivot \
  nc -z -w 3 10.10.0.10 22
test_flux "client-ext -> 203.0.113.1:22 (SSH du pare-feu depuis WAN)" bloque client-ext \
  nc -z -w 3 203.0.113.1 22

echo
echo "Resultat : $ok conforme(s), $ko ecart(s)."
echo "Note : 'dmz-pivot -> client-01:22' est bloque faute de service SSH, pas forcement par pf :"
echo "       verifier dans OPNsense (Firewall > Log Files > Live View) pour les lignes FA5."
