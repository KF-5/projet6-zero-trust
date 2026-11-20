#!/bin/bash
# Projet 6 - reception syslog (UDP 514) seulement depuis OPNsense (10.99.0.1) et p6-docker (10.99.0.20).
# Proposition Q16 / E44 (20 nov.) : Kali (10.99.0.66) est sur MGMT et pouvait envoyer des lignes au collecteur.
# NTP (UDP 123) reste ouvert a tout MGMT : Kali prend son heure sur le collecteur (setup-kali.sh, etape 3).
# A lancer sur p6-collecteur seulement :  sudo bash restreindre-syslog.sh
# Limite : sur un meme segment, une source UDP peut etre usurpee ; la preuve de reference reste le journal
# local d'OPNsense (E13). Ce filtre arrete le cas simple (lignes envoyees par Kali sous sa propre adresse).
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Lance ce script avec sudo."; exit 1; }
command -v nft >/dev/null || { echo "nftables absent : lancer d'abord restreindre-ssh.sh"; exit 1; }

cat > /etc/p6-syslog-filtre.nft <<'NFT'
# Projet 6 : syslog (UDP 514) seulement depuis OPNsense et p6-docker
table inet p6_syslog
delete table inet p6_syslog
table inet p6_syslog {
    chain entree {
        type filter hook input priority -10; policy accept;
        udp dport 514 ip saddr { 10.99.0.1, 10.99.0.20 } accept
        udp dport 514 counter log prefix "p6-syslog-refuse " drop
    }
}
NFT

cat > /etc/systemd/system/p6-syslog-filtre.service <<'UNIT'
[Unit]
Description=Projet 6 - syslog limite a OPNsense et p6-docker
After=network-pre.target
Before=rsyslog.service

[Service]
Type=oneshot
ExecStart=/usr/sbin/nft -f /etc/p6-syslog-filtre.nft
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
UNIT

systemctl daemon-reload
systemctl enable p6-syslog-filtre.service >/dev/null 2>&1
systemctl restart p6-syslog-filtre.service
echo "Filtre syslog actif sur $(hostname) :"
nft list table inet p6_syslog
echo "Controle depuis Kali (doit etre refuse) : logger -n 10.99.0.10 -P 514 -d test ; puis ici : journalctl -k | grep p6-syslog-refuse"
