# Sonde SLA (composant C8) — rôle 3

La sonde joue les **utilisateurs légitimes** du laboratoire : elle interroge l'application chaque seconde et note si chaque requête a réussi, et en combien de temps. Ses mesures servent à trois choses :

- la **disponibilité** pendant les campagnes A et B (objectif ENF4 : au moins 99 %) ;
- les **faux positifs** de l'IDS : pendant 2 h de trafic légitime seul, toute alerte est un faux positif (rapport de conception, section 10.3) ;
- la **panne P-01** : disponibilité avant, pendant et après l'arrêt de Suricata.

## Ce que fait la sonde

| Client | Position | Requête HTTPS (1 par seconde) | Requête DNS (1 toutes les 5 s) |
| --- | --- | --- | --- |
| `client-ext` (203.0.113.10) | WAN, comme un client d'Internet | `https://203.0.113.1/api/produits` : entre par em0, traverse Suricata et le NAT FA1/F01 jusqu'à nginx | non |
| `client-01` (10.10.0.10) | LAN, comme un employé | `https://10.20.0.10/api/produits` : du LAN vers la DMZ (FA4, puis F05 en état B) | `dig @10.20.0.40 www.p6.lab` (FA4, puis F06) |

- Délai maximal : 2 s par requête. Au-delà, la requête est notée en échec.
- Le certificat auto-signé de nginx n'est pas vérifié (`verify=False`) : on mesure la disponibilité, pas la chaîne de certificats.
- La boucle se recale chaque seconde. Pendant un flood, des secondes peuvent « manquer » parce que des requêtes attendent leurs 2 s : c'est justement l'indisponibilité mesurée.

## Fichiers

| Fichier | Où il tourne | Rôle |
| --- | --- | --- |
| `sonde.py` | dans les conteneurs `client-ext` et `client-01` | la boucle de mesure ; écrit une ligne CSV par requête sur la sortie standard |
| `sonde.sh start \| stop [dossier]` | sur l'hôte Docker (p6-docker) | copie et lance `sonde.py` dans les deux conteneurs, puis l'arrête et rassemble `sla.csv` |

Choix par rapport au guide : la requête DNS passe par la commande `dig` (paquet `dnsutils`, déjà dans l'image des postes) au lieu de la bibliothèque `python3-dnspython`, absente de l'image. Le nom interrogé est `www.p6.lab`, la zone réelle de bind9.

## Prérequis

- Image des postes `p6/outils:1` (rôle 4) : `python3`, `python3-requests`, `dnsutils` (`dig`), `procps` (`pkill`). Vérifié le 23/11 dans `plateforme/labo/docker/p6-etat-a/outils/Dockerfile`.
- Route `/api/produits` de l'API, servie par nginx sous `/api/`.
- Accès de l'orchestrateur en place (rôle 1, écart E40) : clé `~/.ssh/orchestrateur` du collecteur et alias `p6-docker`.

## Installation (une fois, puis après chaque modification de `sonde.sh`)

1. Copier les deux fichiers dans `/opt/projet6/sonde/` sur p6-docker. Depuis le PC d'un membre, par le rebond :

   ```
   scp -i $HOME\.ssh\p6labo -o ProxyCommand="ssh -i $HOME\.ssh\p6labo -W %h:%p p6labo@100.125.208.28" sonde\sonde.py sonde\sonde.sh karim@10.99.0.20:/opt/projet6/sonde/
   ```

2. Le rôle 1 relance `2-docker.sh` sur p6-docker (`ssh -t karim@10.99.0.20 "sudo bash ~/2-docker.sh"`). Le script copie `sonde.sh` en **`/usr/local/sbin/p6-sonde`**, appartenant à root (mode 755), et l'autorise sans mot de passe dans `/etc/sudoers.d/p6-orchestrateur`.

   Pourquoi une copie root : `/opt/projet6` appartient à karim. Autoriser `sudo` sur un script que karim peut modifier reviendrait à lui donner root.

   Vérification : `ls -l /usr/local/sbin/p6-sonde` → `-rwxr-xr-x 1 root root …` ; `sudo -l | grep p6-sonde` → `NOPASSWD`.

## Utilisation

Depuis le collecteur, compte `karim` (c'est ce que fait l'orchestrateur du rôle 5) :

```
ssh p6-docker 'sudo -n p6-sonde start'
#   … scénarios, ou 2 h de trafic légitime …
ssh p6-docker 'sudo -n p6-sonde stop /srv/sla/<dossier>'
```

- `start` affiche `sonde démarrée`.
- `stop` arrête la sonde dans les deux conteneurs, puis écrit `/srv/sla/<dossier>/sla.csv` (trié par heure, avec l'en-tête) et affiche le nombre de mesures. Sans dossier : `/srv/sla/`.
- Démarrer la sonde **avant** le premier scénario et l'arrêter **après** le dernier : elle doit couvrir toute la campagne.
- Rapatrier le résultat sur le collecteur : `scp karim@10.99.0.20:/srv/sla/<dossier>/sla.csv resultats/<etat>/<date>/`.

## Format de `sla.csv` (contrat commun)

```
ts_utc,client,cible,type,succes,duree_ms
2026-11-23T20:14:40.217Z,client-01,https://10.20.0.10/api/produits,https,1,53
2026-11-23T20:14:40.217Z,client-ext,https://203.0.113.1/api/produits,https,1,49
2026-11-23T20:14:40.270Z,client-01,10.20.0.40,dns,1,30
```

| Colonne | Contenu |
| --- | --- |
| `ts_utc` | début de la requête, ISO 8601 en UTC à la milliseconde (horloge de p6-docker, synchronisée par NTP sur le collecteur) |
| `client` | `client-ext` ou `client-01` |
| `cible` | l'URL interrogée, ou le serveur DNS |
| `type` | `https` ou `dns` |
| `succes` | `1` (HTTP 200, ou adresse reçue en DNS) ou `0` |
| `duree_ms` | durée en millisecondes ; vide si échec |

Disponibilité = nombre de lignes `succes = 1` / nombre de lignes, par client et par type (calcul du rôle 6, C11).

## Vérification (23 novembre 2026)

```
ssh p6-docker 'sudo -n p6-sonde start'; sleep 20; ssh p6-docker 'sudo -n p6-sonde stop /tmp/essai-sonde; head -n 8 /tmp/essai-sonde/sla.csv; echo "echecs : $(grep -c ",0,$" /tmp/essai-sonde/sla.csv)"'
```

Résultat : 47 mesures en environ 20 s, **0 échec** ; HTTPS de 29 à 53 ms pour les deux clients ; DNS en 30 ms ; horodatage UTC à la milliseconde.

## Dépannage

| Symptôme | Cause probable | Correction |
| --- | --- | --- |
| `sudo: a password is required` | `p6-sonde` absent ou sudoers non installé | relancer `2-docker.sh` (rôle 1) |
| `pkill: not found` | `procps` absent de l'image | l'ajouter au Dockerfile des postes (rôle 4) |
| Toutes les requêtes HTTPS de `client-01` échouent en état B | F05 (rôle 2) ou passerelle du conteneur (rôle 4) | vérifier `test_flux` (rôle 2) |
| Toutes les requêtes DNS échouent | bind9 arrêté, F06 absente, ou zone renommée | `docker exec client-01 dig @10.20.0.40 www.p6.lab +short` doit répondre `203.0.113.1` |
| Des secondes manquent dans `sla.csv` | requêtes en délai dépassé (2 s) | normal pendant un flood : c'est l'indisponibilité mesurée |

## Limites

- Deux clients seulement, et une seule route applicative (`/api/produits`) : la sonde mesure la disponibilité du service, pas les performances de toute l'application.
- La sonde ne vérifie pas le contenu des réponses, seulement le code HTTP 200.
- Pendant P-01, une requête qui démarre juste avant une coupure est comptée en échec même si la coupure dure moins de 2 s.
