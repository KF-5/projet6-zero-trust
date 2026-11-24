# Décision IPS — test de plateforme netmap (rôle 3)

**Décision (23 novembre 2026) : Suricata est déployé en IPS en ligne, mode netmap natif, sur les cartes `em` d'OPNsense sous VMware.** Les replis prévus par le rapport de conception (section 5.3 et risque n° 1 de la section 16.2) ne sont pas nécessaires : ni `dev.netmap.admode=2` (mode émulé), ni mode Divert, ni IDS seul avec blocage par pf. L'écart E41 est levé.

## Plateforme testée

| Élément | Valeur |
| --- | --- |
| Pare-feu | OPNsense 26.7 (amd64), VM `p6-opnsense` sous VMware Workstation Pro |
| Moteur | Suricata 8.0 intégré à OPNsense |
| Carte testée | `em0` (WAN, 203.0.113.1), Intel 82545EM émulée par VMware (pilote `em`, iflib) |
| Réglage netmap | `dev.netmap.admode = 0` (automatique ; natif utilisé) |
| Déchargements | aucun : options de em0 et em1 = `VLAN_MTU, JUMBO_MTU, HWSTATS, MEXTPG` |
| Réglages Suricata | Services › Intrusion Detection › Administration : Enabled, **Capture mode = Netmap (IPS)**, interface WAN seule, Promiscuous décoché, aucune règle chargée (le test porte sur le transport des paquets, pas sur la détection) |
| Modes proposés par OPNsense 26.7 (liste « Capture mode ») | `PCAP live mode (IDS)`, `Netmap (IPS)` (retenu), `Divert (IPS)` : le repli Divert reste disponible sans réinstallation |

Point de retour pris avant le test : instantané `karim-20261121-…-avant-netmap` de p6-opnsense.

## Protocole

1. État des lieux en lecture seule (21/11, 19 h 59) : version, `admode`, messages du noyau, options des cartes, Suricata arrêté.
2. Activation de l'IPS sur le WAN seul (21/11, Suricata démarré à 20:17:38).
3. Pendant 10 minutes, depuis Kali (203.0.113.66) : une requête `curl -sk https://203.0.113.1/` par seconde, à travers em0 et le NAT FA1 déjà autorisé. Aucune règle de pare-feu temporaire n'a été nécessaire.
4. Pendant la boucle : `nmap -sS -Pn -p 1-200 203.0.113.1`.
5. Contrôle des erreurs (noyau et journaux de Suricata) après la boucle, puis après 46 heures de fonctionnement.

## Résultats

| Mesure | Résultat |
| --- | --- |
| Prise en charge netmap du pilote | native : `em0: netmap queues/slots: TX 1/1024, RX 1/1024` ; `iflib_netmap_config txr 1 rxr 1 txd 1024 rxd 1024` |
| Démarrage de Suricata | `Threads created -> W: 2 FM: 1 FR: 1 Engine started` ; seul avertissement : « no rules were loaded » (attendu) |
| Trafic web à travers em0 (10 min) | **600 requêtes sur 600 réussies** (HTTP 200), environ 6 ms chacune |
| Scan nmap à travers em0 | 53/tcp et 80/tcp ouverts, 198 ports filtrés par pf, latence 0,7 ms |
| Endurance | même processus Suricata pendant environ 46 heures (du 21/11 20 h 17 au 23/11 18 h 50) |
| Erreurs | aucune erreur netmap dans `dmesg` ; 0 ligne « error » dans les journaux de Suricata du 21 au 23 novembre |

## Conséquences pour l'équipe

- **Rôle 3** : la suite du guide s'applique sans repli. IPS netmap sur em0 (WAN) puis sur em1 (parent du trunk) à l'étape 8, en état B.
- **Rôle 5** : P-01 se mesure avec Suricata en ligne sur netmap. Les commandes d'arrêt et de redémarrage seront validées et transmises (`docs/p01.md`).
- **Rôle 6** : la mesure de latence « B avec IPS / B sans IPS » a un sens, puisque l'IPS est réellement en coupure.
- **Remarque sur l'état A** : après le test, Suricata (sans aucune règle) est resté actif sur em0 du 21/11 à 20 h 17 jusqu'au 23/11 au moins. Il n'a rien détecté ni bloqué. Si des mesures de la campagne A ont eu lieu pendant cette période, la latence a pu être légèrement modifiée par le passage dans netmap : à mentionner dans les résultats.
- **Rapport final** (section 3.4) : citer ce test comme validation du risque n° 1. Capture : `rapport/captures/ids-01-netmap.png`.
