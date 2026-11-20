#!/bin/bash
# Projet 6 - SSH accepte uniquement depuis Windows (10.99.0.2) et le collecteur (10.99.0.10).
# Rapport, section 6 : "chaque VM n'accepte en SSH que ces deux adresses".
# A lancer sur p6-docker, p6-collecteur et p6-kali :  sudo bash restreindre-ssh.sh
# Met en place une petite table nftables separee (p6_ssh) : elle ne touche pas aux regles de Docker.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Lance ce script avec sudo."; exit 1; }
command -v nft >/dev/null || DEBIAN_FRONTEND=noninteractive apt-get install -y -qq nftables >/dev/null

cat > /etc/p6-ssh-filtre.nft <<'EOF'
# Projet 6 : SSH (TCP 22) seulement depuis Windows et le collecteur
table inet p6_ssh
delete table inet p6_ssh
table inet p6_ssh {
    chain entree {
        type filter hook input priority -10; policy accept;
        iifname "lo" accept
        tcp dport 22 ip saddr { 10.99.0.2, 10.99.0.10 } accept
        tcp dport 22 counter log prefix "p6-ssh-refuse " drop
    }
}
EOF

cat > /etc/systemd/system/p6-ssh-filtre.service <<'EOF'
[Unit]
Description=Projet 6 - SSH limite a Windows et au collecteur
After=network-pre.target
Before=ssh.service

[Service]
Type=oneshot
ExecStart=/usr/sbin/nft -f /etc/p6-ssh-filtre.nft
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable p6-ssh-filtre.service >/dev/null 2>&1
systemctl restart p6-ssh-filtre.service
echo "Filtre SSH actif sur $(hostname) :"
nft list table inet p6_ssh
