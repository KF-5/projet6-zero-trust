#!/bin/bash
# sonde/sonde.sh start | stop [dossier]   — sur l'hôte Docker (p6-docker).
# Installé en copie root sous /usr/local/sbin/p6-sonde par 2-docker.sh (rôle 1) ; appelé par
# l'orchestrateur (rôle 5) avec : ssh p6-docker sudo -n p6-sonde start | stop /srv/sla/<dossier>
set -eu
D=/opt/projet6/sonde
case "${1:-}" in
start)
  for c in client-ext client-01; do docker cp "$D/sonde.py" "$c:/tmp/sonde.py"; done
  docker exec -d client-ext sh -c 'python3 /tmp/sonde.py client-ext https://203.0.113.1/api/produits > /tmp/sla.csv 2>/dev/null'
  docker exec -d client-01  sh -c 'python3 /tmp/sonde.py client-01 https://10.20.0.10/api/produits 10.20.0.40 > /tmp/sla.csv 2>/dev/null'
  echo "sonde démarrée" ;;
stop)
  for c in client-ext client-01; do docker exec "$c" pkill -f sonde.py || true; done
  OUT=${2:-/srv/sla}; mkdir -p "$OUT"
  { echo "ts_utc,client,cible,type,succes,duree_ms"
    for c in client-ext client-01; do docker exec "$c" cat /tmp/sla.csv; done | sort; } > "$OUT/sla.csv"
  echo "$(($(wc -l < "$OUT/sla.csv") - 1)) mesures dans $OUT/sla.csv" ;;
*)
  echo "usage : $0 start | stop [dossier]" >&2; exit 2 ;;
esac
