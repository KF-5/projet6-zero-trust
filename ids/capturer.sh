#!/usr/bin/env bash
# Enregistre une capture par scénario dans pcap/ (mini-laboratoire du rôle 3, sous WSL).
# Pour chaque capture : tcpdump démarre sur le pont du réseau visé, l'attaque part du conteneur
# « attaquant », puis tcpdump est arrêté proprement (Ctrl+C simulé). Usage : ./capturer.sh
set -u
cd ~/projet6/ids
sudo -v || exit 1                                   # demande le mot de passe une seule fois
A="docker compose -f labo/docker-compose.yml exec -T attaquant"

_capture() {                                        # _capture <nom> <pont> <commande d'attaque…>
  local nom=$1 pont=$2; shift 2
  sudo tcpdump -i "$pont" -U -Z "$USER" -w "pcap/$nom.pcap" 2>/dev/null &
  local p=$!; sleep 2                               # laisse tcpdump démarrer
  $A "$@" > /dev/null 2>&1
  sleep 2; sudo kill -INT "$p"; wait "$p" 2>/dev/null
  printf '%-9s %-7s %s\n' "$nom" "$pont" "$(du -h "pcap/$nom.pcap" | cut -f1)"
}

# Sans argument : toutes les captures. Avec des noms (./capturer.sh legitime s06) : seulement celles-là.
CHOIX=" ${*:-s01 s02 s03 s05 s06 legitime} "
capture() {
  [[ "$CHOIX" == *" $1 "* ]] || return 0
  _capture "$@"
}
capture s01 br-wan nmap -sS -Pn -p 1-1024 203.0.113.1                                  # scan SYN
capture s02 br-wan timeout 3 hping3 -S --flood -p 443 203.0.113.1                      # SYN flood (3 s)
capture s03 br-dmz curl -s "http://10.20.0.70:5000/api/produit?id=1'%20OR%20'1'='1"    # injection SQL
capture s05 br-lan nmap -sS -Pn -p 5432 10.10.0.70                                     # LAN -> base
capture s06 br-wan nmap -f -sS -Pn -p 443 203.0.113.1                                  # fragments
# Trafic légitime comme la vraie sonde : 1 requête HTTPS par seconde vers 443 pendant 2 min
capture legitime br-wan sh -c 'for i in $(seq 120); do curl -sk -m 1 -o /dev/null https://203.0.113.1/; sleep 1; done'
