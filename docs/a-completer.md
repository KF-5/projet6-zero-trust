# Commandes à compléter

Fichier **généré** par `P6Explorer.Importeur` (ne pas modifier à la main : relancer l'importeur). Il liste ce que l'importation automatique n'a pas su traduire en effets précis.

## Bilan de l'importation

| Rôle | Étapes | Commandes | dont exécutables | Sous-commandes (scripts dépliés) | Effets |
|---|---|---|---|---|---|
| 1 | 9 | 76 | 66 | 91 | 212 |
| 2 | 9 | 58 | 57 | 62 | 142 |
| 3 | 8 | 76 | 72 | 47 | 144 |
| 4 | 8 | 64 | 63 | 29 | 130 |
| 5 | 6 | 44 | 44 | 96 | 174 |
| 6 | 7 | 30 | 27 | 10 | 44 |
| **Total** | **47** | **348** | | **335** | **846** |

Chaque commande a au moins un effet visible. « Document » = tableau ou plan du guide, affiché sans être exécuté. Tous les effets proposés sont marqués « automatique » jusqu'à leur relecture.

## 1. Effet générique (0 commandes)

Aucun motif reconnu : la commande met seulement sa machine en valeur. À compléter (jalons 4 et 6).


## 2. Scripts dépliés en sous-commandes (14)

Prompt, règle 4 : chaque script long devient une suite de sous-commandes avec leurs effets. Source : le script réel de `plateforme/labo/` s'il existe, sinon le fichier complet du guide ; `test_flux.sh` est déplié depuis `donnees/politique/tests-A.csv` et `tests-B.csv`.

- `c2.py` : 3 sous-commandes
- `capture.sh` : 5 sous-commandes
- `correle.py` : 3 sous-commandes
- `orchestrateur.py` : 7 sous-commandes
- `parse.py` : 7 sous-commandes
- `reseaux-B.sh` : 9 sous-commandes
- `restreindre-ssh.sh` : 7 sous-commandes
- `setup-kali.sh` : 28 sous-commandes
- `setup.sh` : 10 sous-commandes
- `sonde.sh` : 10 sous-commandes
- `test_flux.sh` : 42 sous-commandes
- `tester.sh` : 3 sous-commandes
- `tests-cloisonnement.sh` : 19 sous-commandes
- `tests.sh` : 12 sous-commandes

Scripts restant à déplier (0) : aucun.

## 3. Remarques

- Les paquets ont un verdict « à calculer » : le moteur réseau (jalon 3) donnera le chemin, le NAT, le verdict et la règle selon l'état A ou B.
- r3-e0 (mini-laboratoire WSL sur le PC d'un membre) : rien ne circule dans le laboratoire ; effets ramenés à une mise en valeur du PC du membre (plan, décision 3). Le schéma du mini-laboratoire n'est pas dessiné : il reprend les adresses du laboratoire, que la carte principale montre déjà.
