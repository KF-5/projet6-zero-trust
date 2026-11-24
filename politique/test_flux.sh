#!/bin/bash
# test_flux — exécute tests.csv depuis l'espace réseau de chaque conteneur source (écart E23).
# À lancer sur l'hôte Docker, en root :  ./test_flux.sh politique/tests.csv politique/hosts.csv
set -u
TESTS=${1:-politique/tests.csv}; HOSTS=${2:-politique/hosts.csv}
printf "%-6s %-11s %-15s %-4s %-5s %-6s %-6s %s\n" ID SOURCE DESTINATION PROTO PORT ATTENDU OBTENU VERDICT
# tr -d '\r' : un CSV enregistré sous Windows (fin de ligne CRLF) fausserait la dernière colonne
tail -n +2 "$TESTS" | tr -d '\r' | while IFS=, read -r id src dst proto port attendu ref; do
  ctr=$(tr -d '\r' < "$HOSTS" | awk -F, -v h="$src" '$1==h{print $5}')   # nom du conteneur source (hosts.csv)
  pid=$(docker inspect -f '{{.State.Pid}}' "$ctr" 2>/dev/null)      # processus du conteneur
  if [ -z "$pid" ] || [ "$pid" = "0" ]; then                        # conteneur absent ou arrêté : c'est un écart
    printf "%-6s %-11s %-15s %-4s %-5s %-6s %-6s %s\n" "$id" "$src" "$dst" "$proto" "$port" "$attendu" "-" "ÉCART (conteneur introuvable)"
    continue
  fi
  if [ "$proto" = "udp" ] && [ "$port" = "53" ]; then
    nsenter -t "$pid" -n dig @"$dst" www.p6.lab +time=2 +tries=1 >/dev/null 2>&1 && r=pass || r=block
  else
    out=$(nsenter -t "$pid" -n nc -zv -w 2 "$dst" "$port" 2>&1); rc=$?
    if [ $rc -eq 0 ]; then r=pass                                   # connexion établie
    elif echo "$out" | grep -qi refused; then r=pass               # RST reçu : le pare-feu a laissé passer (port fermé)
    else r=block; fi                                               # délai dépassé : paquet jeté par le pare-feu
  fi
  [ "$r" = "$attendu" ] && v=CONFORME || v=ÉCART
  printf "%-6s %-11s %-15s %-4s %-5s %-6s %-6s %s\n" "$id" "$src" "$dst" "$proto" "$port" "$attendu" "$r" "$v"
done | tee /tmp/test_flux.txt
echo "Conformes : $(grep -c CONFORME /tmp/test_flux.txt) · Écarts : $(grep -c ÉCART /tmp/test_flux.txt)"
! grep -q ÉCART /tmp/test_flux.txt
