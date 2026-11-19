# Projet 6 — Zero Trust (GL3, INSAT)

Laboratoire qui compare deux états d'un même réseau d'entreprise simulé :
l'état A (périmétrique, PME classique) et l'état B (Zero Trust : segmentation fine, IDS/IPS).

- Guide de réalisation (étapes par rôle, contrats de données) : https://claude.ai/artifact/WHAkpWWUrYCnk2EVWdEZSY
- Écarts entre la conception et la réalisation : `ECARTS.md`
- Rôles et responsabilités : `docs/roles.md`
- Réseaux, VM et cartes réseau : `docs/interfaces.md`

## Branches

Une branche par rôle, fusion dans `main` par le rôle 1 après un test (test_flux ou exécution à blanc) :

| Rôle | Branche |
|---|---|
| 1 Chef de projet · plateforme | `role-1-plateforme` |
| 2 Pare-feu et segmentation | `role-2-parefeu` |
| 3 IDS/IPS et trafic légitime | `role-3-ids` |
| 4 Services applicatifs | `role-4-services` |
| 5 Validation et attaques | `role-5-attaques` |
| 6 Observabilité et analyse | `role-6-analyse` |

## Conventions

- Message de commit : `[rôle N] verbe + objet`, par exemple `[rôle 2] Ajoute F04 à flows.csv`.
- Un fichier a un seul propriétaire (page Contrats du guide) : `flows.csv` appartient au rôle 2, les autres proposent par message.
- Jamais de mot de passe ni de clé privée dans le dépôt : `.env` n'est pas versionné, un modèle `.env.exemple` l'est.
- Les captures (`*.pcap`) ne sont pas versionnées : elles sont archivées à part, en zip daté.

## Tags

- `etat-A`, `etat-B` : configurations figées (même contenu que les instantanés VMware du même nom).
- `campagne-A`, `campagne-B` : résultats archivés.
- `remise` : version rendue le 30 novembre 2026.

## Arborescence

| Dossier | Contenu | Rôle |
|---|---|---|
| `conception/uml` | diagrammes PlantUML du rapport de conception | 1 |
| `plateforme` | scripts de l'hôte et des VM (setup, filtre SSH, tests de cloisonnement) | 1 |
| `opnsense` | `config-etat-A.xml`, `config-etat-B.xml` | 2 |
| `politique` | `flows.csv`, moteur C2, `test_flux` | 2 |
| `ids` | règles locales Suricata, seuils | 3 |
| `sonde` | trafic légitime et sonde SLA (C8) | 3 |
| `services` | `docker-compose.yml`, nginx, API, bind9, PostgreSQL | 4 |
| `capture` | outil de capture C10 | 4 |
| `campagne` | `scenarios.yaml`, orchestrateur C6 | 5 |
| `analyse` | parseurs C9, corrélation C11, indicateurs | 6 |
| `resultats` | `A/`, `B/`, `synthese/` | 5, 6 |
| `rapport` | une section par rôle (`rapport/<role>.md`) | tous |
| `docs` | documents de travail communs | tous |

## Copier le dépôt dans les VM (elles n'ont pas Internet)

Depuis Windows, sur le PC du labo (SSH n'accepte que Windows 10.99.0.2 et le collecteur 10.99.0.10) :

```powershell
git bundle create projet6.bundle --all
scp projet6.bundle karim@10.99.0.10:~/
scp projet6.bundle karim@10.99.0.20:~/
```

Puis sur chaque VM : première fois
`sudo install -d -o karim -g karim /opt/projet6 && git clone ~/projet6.bundle /opt/projet6`,
les fois suivantes `cd /opt/projet6 && git pull ~/projet6.bundle main`.
