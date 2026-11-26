# Version figée de la campagne (A et B)

Figée par le rôle 1 le 26/11/2026 après deux répétitions à blanc, validée par le rôle 5.
**La campagne A (lancée le 26/11 à 22 h 00) et la campagne B doivent utiliser exactement ces deux fichiers** ; seul `--etat` change.

| Fichier | SHA-256 |
|---|---|
| `campagne/orchestrateur.py` | `ec3829d950d1bc1a0894b709e561f5bded32599fffb6f11e36a2984ede85c60f` |
| `campagne/scenarios.json` | `71c02cf25255c0d7976e2833b244b3098cac45e7ef8550f6e77a4eae645b22be` |

Vérifier avant la campagne B (sur le collecteur) : `sha256sum ~/campagne-A/orchestrateur.py ~/campagne-A/scenarios.json`.

Versions d'origine du rôle 5 (26/11, 19 h 44) : `orchestrateur.py` `2740a4830e3d…7491748`, `scenarios.json` `3449709f4fac…50bd2cdb`.

## Les 5 corrections et leur raison (répétition à blanc du 26/11, 20 h 21)

| # | Fichier | Correction | Constat |
|---|---|---|---|
| 1 | orchestrateur | pause de 2 s avant l'arrêt des captures | PCAP postgres vides (« 12 packets received by filter, 0 captured ») : tcpdump reçoit les paquets par blocs d'environ 1 s et S-04/S-05 durent 0,2 s ; `correle.py` aurait conclu « bloqué » à tort, même en A |
| 2 | orchestrateur | effacer `/tmp/<run_id>_kali-wan.pcap` sur Kali après une copie **réussie** | `/tmp` de Kali = tmpfs de 714 Mo ; une capture de S-02 = 113 Mo ; saturation au 5e flood |
| 3 | S-03 | `id=1 OR 1=1` au lieu de `id=1' OR '1'='1` ; critère « la réponse contient plus d'un produit » | la colonne `id` est numérique (erreur de syntaxe SQL) ; la table ne contient pas « secret » |
| 4 | S-04 | `nc -zv -w 5 10.10.0.70 5432` au lieu de `nmap` ; `psql` gardé ; critère « la base répond (TCP établi, même si pg_hba refuse) » | `nmap` absent de l'image `p6/outils:1` ; `pg_hba` n'autorise que 10.20.0.70 et 10.10.0.100 : `psql` échoue en A comme en B |
| 5 | S-05 | idem S-04 (plus de balayage du /27) | idem |

Inchangé : la commande tcpdump de Kali (sudoers), la graine 2026, n = 10, pause_s = 30.

Résultat de la 2e répétition (`resultats/A/essai2`, 26/11, 21 h 51) : S-01 3 ports ouverts ; S-03 8 produits ; S-04 et S-05 « succeeded » + SYN-ACK de 10.10.0.70:5432 dans le PCAP + refus pg_hba ; S-02 code 124 ; 8 PCAP non vides ; `/tmp` de Kali vidé.

## Autre correction de plateforme le même soir

p6-docker n'était synchronisé sur aucun serveur NTP (≈ 11 s d'avance ; `chrony` absent). Corrigé avant la campagne A : `/etc/systemd/timesyncd.conf.d/p6.conf` (`NTP=10.99.0.10`, `FallbackNTP=` vide) ; écart mesuré −10,94 s puis 2 ms.

## À aligner avant la campagne B

- `correle.py` (rôle 6) : critère S-03 « plus d'un produit » (par exemple `sortie.count('"nom"') > 1`) au lieu de `"secret" in sortie`.
- Règle Suricata de S-03 (rôle 3) : doit détecter `1 OR 1=1`.
