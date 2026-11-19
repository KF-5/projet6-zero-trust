-- Donnees fictives du laboratoire
INSERT INTO categorie (nom) VALUES
  ('Peripheriques'), ('Reseau'), ('Stockage');

INSERT INTO produit (nom, prix, stock, categorie_id) VALUES
  ('Clavier',             25.00, 40, 1),
  ('Souris',              12.50, 75, 1),
  ('Ecran 24 pouces',    139.90, 12, 1),
  ('Switch 8 ports',      34.00, 20, 2),
  ('Routeur Wi-Fi 6',     89.00,  8, 2),
  ('Cable RJ45 3 m',       4.90, 200, 2),
  ('SSD 1 To',            69.00, 30, 3),
  ('Disque externe 2 To', 79.00, 15, 3);

-- Hashs bcrypt FICTIFS (ne correspondent a aucun mot de passe reel)
INSERT INTO utilisateur (login, hash_mdp, role) VALUES
  ('admin',  '$2b$12$k.Heas8VOcI.z0myHl2QlU0G4mzLmfzU.ydUF3MTplzoINzW9CrAb', 'admin'),
  ('salma',  '$2b$12$yvcQNVWXLq.gx2Ym4Fl55Wj28M0kyI5.wccpGUyG5QG01.I89.sW/', 'client'),
  ('youssef','$2b$12$N7PUuWIOY.uLa5hxdoVU/hytIb2.j8DmVUsFOsemnf97idqkuH5/c', 'client'),
  ('ines',   '$2b$12$ry4945J9iLUKUekJJJ834VekFEewRVegA3RjuoZr7/jqjSdxtxKtX', 'client');

INSERT INTO commande (produit_id, utilisateur_id, quantite) VALUES
  (1, 2, 1), (7, 3, 2), (4, 4, 1);
