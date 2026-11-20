#!/bin/bash
# Projet 6 - configuration de la VM Kali (attaquant externe, composant C7)
#   eth0 = p6-wan  -> 203.0.113.66/24, passerelle 203.0.113.1 (OPNsense WAN)
#   eth1 = VMnet9  -> 10.99.0.66/24 (MGMT, pilotage par le collecteur et Windows)
#   eth2 = NAT     -> temporaire (installation), a debrancher ensuite dans VMware
# Usage : sudo bash setup-kali.sh     (relancable sans risque)
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Lance ce script avec sudo."; exit 1; }
etape() { echo; echo "=== $* ==="; }

etape "1/6 Adresses permanentes (eth0 WAN, eth1 MGMT)"
cat > /etc/network/interfaces.d/p6-lab <<'EOF'
# WAN simule (p6-wan) - interface d'attaque
auto eth0
iface eth0 inet static
    address 203.0.113.66/24
    # metric 100 : tant que la carte NAT (DHCP, metric 0) est branchee, Internet passe par elle ;
    # une fois debranchee, la seule route par defaut restante pointe vers OPNsense.
    gateway 203.0.113.1
    metric 100

# MGMT (VMnet9) - pilotage uniquement, pas de passerelle
auto eth1
iface eth1 inet static
    address 10.99.0.66/24
EOF
cat /etc/network/interfaces.d/p6-lab
if grep -q '^source /etc/network/interfaces.d/\*' /etc/network/interfaces; then
  echo "OK : interfaces.d est bien lu."
else
  echo "source /etc/network/interfaces.d/*" >> /etc/network/interfaces
  echo "Ligne 'source interfaces.d' ajoutee a /etc/network/interfaces."
fi
if systemctl is-active --quiet NetworkManager; then
  echo "ATTENTION : NetworkManager est actif ; il ne gere pas les cartes declarees dans interfaces.d."
fi

etape "2/6 Outils du rapport (nmap, hping3, nikto, curl, psql) + chrony + tcpdump"
DEBIAN_FRONTEND=noninteractive apt-get update -qq
DEBIAN_FRONTEND=noninteractive apt-get install -y -qq \
  nmap hping3 nikto curl postgresql-client dnsutils netcat-openbsd tcpdump chrony openssh-server >/dev/null
echo "Paquets installes."

etape "3/6 Heure : client NTP du collecteur (10.99.0.10)"
cat > /etc/chrony/conf.d/p6-lab.conf <<'EOF'
# Projet 6 : l'heure du labo vient du collecteur
server 10.99.0.10 iburst prefer
EOF
systemctl restart chrony

etape "4/6 SSH : uniquement sur MGMT (jamais sur le WAN d'attaque)"
ip addr show dev eth1 | grep -q '10.99.0.66/' || ip addr add 10.99.0.66/24 dev eth1
ip link set eth1 up
mkdir -p /etc/ssh/sshd_config.d
cat > /etc/ssh/sshd_config.d/p6-lab.conf <<'EOF'
# Projet 6 : SSH ecoute seulement sur l'adresse MGMT
ListenAddress 10.99.0.66
PermitRootLogin no
EOF
# Au demarrage, attendre que le reseau soit pret avant de lancer SSH (adresse MGMT presente)
mkdir -p /etc/systemd/system/ssh.service.d
cat > /etc/systemd/system/ssh.service.d/p6-reseau.conf <<'EOF'
[Unit]
Wants=network-online.target
After=network-online.target
EOF
systemctl daemon-reload
sshd -t && echo "Configuration SSH valide."
systemctl enable ssh >/dev/null 2>&1
systemctl restart ssh

etape "5/6 Demarrage en mode texte (pas d'interface graphique)"
systemctl set-default multi-user.target

etape "6/6 Activation des adresses"
ip link set eth0 up
ip addr show dev eth0 | grep -q '203.0.113.66/' || ip addr add 203.0.113.66/24 dev eth0
ip addr show dev eth1 | grep -q '10.99.0.66/'   || ip addr add 10.99.0.66/24 dev eth1
ip link set eth1 up

echo
echo "=== Etat ==="
ip -br addr
ip route
ss -tlnp | grep ':22 ' || true
echo
echo "Termine. Prochaine etape : debrancher la carte NAT (carte reseau 3) dans VMware."
