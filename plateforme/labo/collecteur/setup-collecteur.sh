#!/bin/bash
# Projet 6 - configuration du collecteur (VM p6-collecteur, MGMT 10.99.0.10)
# Role : serveur NTP du labo (chrony) et reception des journaux (rsyslog, UDP 514).
# Usage : sudo bash setup-collecteur.sh     (relancable sans risque)
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Lance ce script avec sudo."; exit 1; }
etape() { echo; echo "=== $* ==="; }

etape "1/4 Adresse MGMT permanente (ens32 = 10.99.0.10/24, sans passerelle)"
cat > /etc/network/interfaces.d/p6-mgmt <<'EOF'
# MGMT (VMnet9) - adresse fixe, pas de passerelle
auto ens32
iface ens32 inet static
    address 10.99.0.10/24
EOF
cat /etc/network/interfaces.d/p6-mgmt
grep -q '^source /etc/network/interfaces.d/\*' /etc/network/interfaces \
  && echo "OK : interfaces.d est bien lu." \
  || echo "ATTENTION : /etc/network/interfaces ne lit pas interfaces.d"

etape "2/4 Installation de chrony et rsyslog (via la carte NAT)"
DEBIAN_FRONTEND=noninteractive apt-get install -y chrony rsyslog tcpdump >/dev/null
echo "Paquets installes."

etape "3/4 NTP : le collecteur sert l'heure au reseau MGMT"
cat > /etc/chrony/conf.d/p6-lab.conf <<'EOF'
# Projet 6 : serveur NTP pour le reseau MGMT du labo
allow 10.99.0.0/24
# Le labo n'a pas d'Internet : le collecteur reste une reference locale
local stratum 8
EOF
systemctl restart chrony
sleep 2
chronyc tracking | grep -E 'Reference ID|Stratum|System time' || true

etape "4/4 rsyslog : reception des journaux sur UDP 514"
mkdir -p /var/log/p6
cat > /etc/rsyslog.d/10-p6-reception.conf <<'EOF'
# Projet 6 : reception des journaux du labo (OPNsense, p6-docker...)
module(load="imudp")
input(type="imudp" port="514" ruleset="p6distant")
template(name="P6ParHote" type="string" string="/var/log/p6/%FROMHOST-IP%.log")
ruleset(name="p6distant") {
    action(type="omfile" dynaFile="P6ParHote")
}
EOF
rsyslogd -N1 >/dev/null 2>&1 && echo "Configuration rsyslog valide." || { echo "Erreur de configuration rsyslog"; rsyslogd -N1; exit 1; }
systemctl restart rsyslog

echo
echo "=== Etat ==="
ip -br addr show ens32
ss -ulnp | grep -E ':123 |:514 ' || true
echo
echo "Termine. Le collecteur ecoute NTP (UDP 123) et syslog (UDP 514) ;"
echo "les journaux recus arriveront dans /var/log/p6/<adresse>.log"
