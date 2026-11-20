#!/bin/bash
# Projet 6 - deploiement des services de l'etat A sur la VM p6-docker.
# Usage (dans le dossier p6-etat-a) :  sudo bash setup.sh
# Relancable sans risque : ne regenere ni les mots de passe ni le certificat s'ils existent.
set -euo pipefail
cd "$(dirname "$0")"

etape() { echo; echo "=== $* ==="; }
[ "$(id -u)" -eq 0 ] || { echo "Lance ce script avec sudo."; exit 1; }

etape "1/5 Verification des cartes reseau"
for c in ens33 ens34; do
  ip link show "$c" >/dev/null 2>&1 || { echo "Carte $c absente."; exit 1; }
  ip link set "$c" up
  echo "$c : $(cat /sys/class/net/$c/operstate)"
done
command -v docker >/dev/null || { echo "Docker n'est pas installe."; exit 1; }

etape "2/5 Mots de passe (.env)"
if [ ! -f .env ]; then
  umask 077
  {
    echo "POSTGRES_PASSWORD=$(openssl rand -hex 16)"
    echo "API_DB_PASSWORD=$(openssl rand -hex 16)"
  } > .env
  echo ".env cree (droits 600, mots de passe aleatoires, jamais affiches)."
else
  echo ".env deja present, conserve."
fi

etape "3/5 Certificat TLS auto-signe pour nginx"
mkdir -p nginx/certs
if [ ! -f nginx/certs/p6.crt ]; then
  openssl req -x509 -newkey rsa:2048 -nodes -days 365 \
    -keyout nginx/certs/p6.key -out nginx/certs/p6.crt \
    -subj "/CN=www.p6.lab" \
    -addext "subjectAltName=DNS:www.p6.lab,DNS:api.p6.lab,IP:203.0.113.1,IP:10.20.0.10" 2>/dev/null
  chmod 644 nginx/certs/p6.key   # lisible par nginx dans le conteneur (labo uniquement)
  echo "Certificat cree."
else
  echo "Certificat deja present, conserve."
fi
chmod 755 db/03-role-api.sh

etape "4/5 Construction des images et demarrage (quelques minutes la 1re fois)"
docker compose build
docker compose up -d

etape "5/5 Etat des conteneurs"
sleep 5
docker compose ps --format "table {{.Name}}\t{{.Status}}"
echo
echo "Termine. Lance ensuite :  sudo bash tests.sh"
