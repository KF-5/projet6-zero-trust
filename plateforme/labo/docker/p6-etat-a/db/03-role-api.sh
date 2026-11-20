#!/bin/bash
# Compte utilise par l'API : aucun droit de creation ni de suppression de table
# (rapport, section 12.1). Le mot de passe vient de .env (variable API_DB_PASSWORD).
set -euo pipefail
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
     -v pw="$API_DB_PASSWORD" <<'SQL'
CREATE ROLE api_catalogue LOGIN PASSWORD :'pw';
GRANT SELECT ON categorie, produit, utilisateur TO api_catalogue;
GRANT SELECT, INSERT ON commande TO api_catalogue;
GRANT UPDATE (stock) ON produit TO api_catalogue;
GRANT USAGE ON SEQUENCE commande_id_seq TO api_catalogue;
SQL
