# Commandes à compléter

Fichier **généré** par `P6Explorer.Importeur` (ne pas modifier à la main : relancer l'importeur). Il liste ce que l'importation automatique n'a pas su traduire en effets précis.

## Bilan de l'importation

| Rôle | Étapes | Commandes | dont exécutables | Effets |
|---|---|---|---|---|
| 1 | 9 | 76 | 66 | 113 |
| 2 | 9 | 58 | 57 | 75 |
| 3 | 8 | 76 | 72 | 85 |
| 4 | 8 | 64 | 63 | 80 |
| 5 | 6 | 44 | 44 | 61 |
| 6 | 7 | 30 | 27 | 34 |
| **Total** | **47** | **348** | | **448** |

Chaque commande a au moins un effet visible. « Document » = tableau ou plan du guide, affiché sans être exécuté. Tous les effets proposés sont marqués « automatique » jusqu'à leur relecture.

## 1. Effet générique (4 commandes)

Aucun motif reconnu : la commande met seulement sa machine en valeur. À compléter (jalons 4 et 6).

- `r3-e0-c23` — `docker compose exec attaquant sh -c 'for i in $(seq 120); do curl -s -o /dev/null http://203.0.113.1/; sleep …`
- `r3-e6-c02` — `DEBUT=$(date -u +%FT%TZ)`
- `r3-e6-c04` — `FIN=$(date -u +%FT%TZ)`
- `r5-e1-c08` — `nmap --version | head -1; hping3 --version | head -1; nikto -Version | head -2`

## 2. Scripts à déplier en sous-commandes (14)

Prompt, règle 4 : chaque script long devient une suite de sous-commandes avec leurs effets (jalons 3 à 6).

- `./politique/test_flux.sh` — `r1-e8-c03`, `r2-e7-c05`, `r2-e9-c03`
- `./reseaux-B.sh` — `r4-e8-c05`
- `./tester.sh` — `r3-e0-c25`, `r3-e0-c26`
- `/opt/projet6/capture/capture.sh` — `r4-e7-c02`, `r4-e7-c04`
- `/opt/projet6/sonde/sonde.sh` — `r3-e3-c04`, `r3-e6-c03`, `r5-e5-c03`, `r5-e5-c05`, `r5-e6-c03`, `r5-e6-c06`
- `~/setup-kali.sh` — `r1-e4-c08`
- `~/tests-cloisonnement.sh` — `r1-e5-c10`
- `analyse/correle.py` — `r6-e5-c02`
- `analyse/parse.py` — `r1-e8-c05`, `r3-e6-c06`, `r5-e4-c04`, `r5-e5-c08`, `r5-e6-c10`, `r6-e3-c03`
- `campagne/orchestrateur.py` — `r1-e8-c04`, `r3-e5-c01`, `r3-e7-c05`, `r5-e3-c02`, `r5-e4-c01`, `r5-e5-c04`, `r5-e6-c04`, `r5-e6-c05`
- `politique/c2.py` — `r1-e8-c02`, `r2-e6-c03`, `r2-e6-c04`, `r2-e7-c03`, `r2-e9-c01`
- `restreindre-ssh.sh` — `r1-e5-c08`
- `setup.sh` — `r4-e6-c02`
- `tests.sh` — `r1-e4-c09`

## 3. Remarques

- Les paquets ont un verdict « à calculer » : le moteur réseau (jalon 3) donnera le chemin, le NAT, le verdict et la règle selon l'état A ou B.
- r3-e0 (mini-laboratoire WSL sur le PC d'un membre) : effets ramenés à une mise en valeur du PC ; schéma propre à dessiner au jalon 6 (plan, décision 3).
