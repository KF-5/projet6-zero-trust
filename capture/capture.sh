#!/bin/bash
# capture.sh — C10 : une capture par exécution, dans le réseau du conteneur cible
# Usage : capture.sh start|stop <run_id> <point>
#   <run_id> : A-S01-03, B-S05-10, TEST-S05-01…   <point> : nginx | api | postgres | bind9
set -eu
[ $# -eq 3 ] || { echo "usage : $0 start|stop <run_id> <point>" >&2; exit 2; }
ACTION=$1; RUN=$2; POINT=$3

# Le script tourne en root : on refuse tout nom bizarre (sécurité)
[[ "$RUN" =~ ^[A-Z]+-S[0-9]{2}-[0-9]{2}$ ]] || { echo "run_id invalide : $RUN" >&2; exit 2; }
case "$POINT" in nginx|api|postgres|bind9) CTR=$POINT ;; *) echo "point inconnu : $POINT" >&2; exit 2 ;; esac

DIR=/srv/captures; mkdir -p "$DIR"
PCAP="$DIR/${RUN}_${POINT}.pcap"      # le fichier de capture
PIDF="$DIR/.${RUN}-${POINT}.pid"      # mémorise le numéro de tcpdump, pour l'arrêter
ERRF="$DIR/.${RUN}-${POINT}.err"      # messages d'erreur de tcpdump

case "$ACTION" in
  start)
    [ ! -f "$PIDF" ] || { echo "capture déjà en cours : $RUN $POINT" >&2; exit 1; }
    PID=$(docker inspect -f '{{.State.Pid}}' "$CTR")
    [ "$PID" -gt 0 ] || { echo "conteneur $CTR arrêté" >&2; exit 1; }
    # setsid nohup : tcpdump continue même si la connexion SSH se ferme
    setsid nohup nsenter -t "$PID" -n \
      tcpdump -i eth0 -U -s 0 -Z root -w "$PCAP" >/dev/null 2>"$ERRF" &
    echo $! > "$PIDF"
    sleep 1
    kill -0 "$(cat "$PIDF")" 2>/dev/null || { echo "tcpdump n'a pas démarré :" >&2; cat "$ERRF" >&2; rm -f "$PIDF"; exit 1; }
    echo "capture démarrée : $PCAP"
    ;;
  stop)
    [ -f "$PIDF" ] || { echo "aucune capture en cours : $RUN $POINT" >&2; exit 1; }
    P=$(cat "$PIDF")
    kill -TERM "$P" 2>/dev/null || true
    for _ in 1 2 3 4 5; do kill -0 "$P" 2>/dev/null || break; sleep 1; done
    rm -f "$PIDF"
    ls -l "$PCAP"
    ;;
  *) echo "action inconnue : $ACTION" >&2; exit 2 ;;
esac
