-- Base du catalogue - modele physique (rapport, section 12.1)
CREATE TABLE categorie (
  id  SERIAL PRIMARY KEY,
  nom VARCHAR(60) NOT NULL UNIQUE
);
CREATE TABLE produit (
  id           SERIAL PRIMARY KEY,
  nom          VARCHAR(100) NOT NULL,
  prix         NUMERIC(10,2) NOT NULL CHECK (prix >= 0),
  stock        INTEGER NOT NULL DEFAULT 0 CHECK (stock >= 0),
  categorie_id INTEGER NOT NULL REFERENCES categorie(id)
);
CREATE TABLE utilisateur (
  id       SERIAL PRIMARY KEY,
  login    VARCHAR(40) NOT NULL UNIQUE,
  hash_mdp CHAR(60) NOT NULL,            -- bcrypt, donnees fictives
  role     VARCHAR(20) NOT NULL DEFAULT 'client'
);
CREATE TABLE commande (
  id             SERIAL PRIMARY KEY,
  produit_id     INTEGER NOT NULL REFERENCES produit(id),
  utilisateur_id INTEGER NOT NULL REFERENCES utilisateur(id),
  quantite       INTEGER NOT NULL CHECK (quantite > 0),
  date_commande  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_produit_categorie ON produit(categorie_id);
