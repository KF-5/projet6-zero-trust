# Questions pour Karim (rôle 1)

Informations qui manquent ou se contredisent entre le guide, `ECARTS.md` et la plateforme réelle. Je ne devine pas : chaque question dit ce qui est bloqué et l'hypothèse suivie en attendant.

**Règle (20 nov.) :** la référence est `ECARTS.md` (E31 à E42), puis les scripts de `plateforme/labo/`. Ce qui reste incertain reste ici, et le projet continue avec l'hypothèse la plus prudente.

---

## Questions ouvertes

### Q9 — Noms des interfaces VLAN d'OPNsense en état B · ouvert

En état A, OPNsense 26.7 nomme les VLAN `vlan01` et `vlan02`. Les sorties attendues de l'état B (rôle 2 ét. 8 et 9) montrent `vlan0.11`, `vlan0.12`… L'état B n'est pas encore construit (prévu les 22–23 nov.).

**Hypothèse suivie :** afficher les noms du guide (`vlan0.11`…) avec la mention « à vérifier quand l'état B existera » ; à corriger dès que la configuration B est exportée.

### Q10 — LAN → MGMT et LAN → WAN en état A · ouvert (en partie répondu)

Répondu par `tests.sh` : un poste du LAN joint la console d'OPNsense par sa passerelle LAN (`client-03 → 10.10.0.1:443` passe, FA4, faiblesse voulue).

Reste incertain : un poste du LAN (10.10.0.10) qui vise une adresse **MGMT** (10.99.0.1, 10.99.0.20…) ou **Kali** (203.0.113.66). FA4 « LAN net → any » semble l'autoriser au niveau de pf.

**Hypothèse suivie (la plus prudente pour la démonstration) :** pf laisse passer par FA4 ; sur les VM, le filtre `p6_ssh` jette le port 22. **Précisé au jalon 3 :** pour les autres ports, la connexion échoue quand même, car p6-docker et p6-collecteur n'ont **aucune route de retour** vers 10.10.0.0/24 (pas de passerelle depuis que la carte NAT est débranchée) : le moteur rend « injoignable, pas de route de retour ». À confirmer par un test depuis client-03 (par exemple `nc -z -w 3 10.99.0.20 22`, `ping -c 2 10.99.0.10`) et `ip route` sur les deux VM.

### Q11 — Informations personnelles affichées · ouvert

Le prompt demande d'afficher l'adresse Tailscale 100.125.208.28, le nom de machine `best-technology`, le compte `p6labo` et le prénom Karim. Le dépôt est-il privé ?

**Hypothèse suivie (prudente) :** l'adresse Tailscale et le nom de machine ne sont affichés qu'aux **membres connectés** ; les visiteurs anonymes voient « adresse Tailscale de l'hôte (réservée aux membres) ».

### Q12 — Nom de la branche de travail · ouvert (sans blocage)

Le `README.md` prévoit `role-1-plateforme` ; le prompt demande `role-1-plateforme-graphique`. Je travaille sur `role-1-plateforme-graphique`. À renommer si tu préfères.

### Q13 — Réactivation du SSH d'OPNsense · ouvert (sans blocage)

Le SSH d'OPNsense est désactivé ; il sera réactivé sur MGMT, par clé seulement, pour l'orchestrateur du rôle 5. À quelle date, et pour quel compte (root avec `/bin/sh`, ou un compte dédié) ?

**Hypothèse suivie :** désactivé dans la topologie de référence ; une bascule « SSH OPNsense actif » (éteinte par défaut) permet de rejouer les commandes du rôle 5 telles que le guide les prévoit.

### Q14 — S-03 sur la vraie API : pas de colonne « secret », injection numérique · ouvert

Trouvé au jalon 2 en relisant `plateforme/labo/docker/p6-etat-a/api/app.py` et `db/` :

- la vraie API colle `id` **sans guillemets** (`… WHERE id = ` + ident) : l'injection qui marche est numérique (`id=1 OR 1=1`) ; celle du guide et de `scenarios.yaml` (`id=1' OR '1'='1`) provoque une **erreur SQL 500** ;
- la table réelle est `produit` (id, nom, prix, stock, categorie_id), **sans colonne `secret`** : le critère d'objectif de S-03 (« la sortie de curl contient « secret » », rôle 5 et `correle.py` du rôle 6) ne peut jamais être atteint en état A.

**Hypothèse suivie :** le rôle 4 étape 3 (faite) montre la requête numérique réelle ; les étapes des rôles 5 et 6 (à venir) gardent le texte du guide, avec l'écart signalé. À trancher avec les rôles 4, 5 et 6 avant la campagne A (changer la commande de S-03 et son critère, ou ajouter une donnée « secrète » à la base).

### Q15 — État B : la règle F01 rouvre l'écart E36 · ouvert (trouvé au jalon 3)

`regles-B.md` (sortie de `c2.py --etat B`) crée sur le WAN **F01 : pass any → 10.20.0.10, ports 80 et 443** et **F02 : any → 10.20.0.40:53**. Sur la plateforme réelle, les redirections FA1/FA2 sont en « Firewall rule = Pass » (E36) : un paquet redirigé n'est pas filtré, donc F01 et F02 ne servent à rien pour le trafic légitime, mais elles laissent **Kali joindre 10.20.0.10:80 directement, sans traduction** (Kali a le pare-feu pour passerelle) — exactement le trou corrigé en E36. Le test `tests-cloisonnement.sh` n°17 (« accès direct DMZ 10.20.0.10:80 ») échouerait en état B.

**Hypothèse suivie :** l'état B simulé garde les redirections de l'état A telles quelles (« Pass », le guide ne les modifie pas) et applique `regles-B.md` à la lettre ; le moteur montre donc le trou (test `Q15_en_B_la_regle_F01_laisse_joindre_nginx_sans_traduction`). À trancher avec le rôle 2 avant les 22–23 nov. : ne pas saisir F01/F02 sur le WAN (les redirections « Pass » suffisent), ou repasser les redirections en « Aucune » et garder F01/F02 en limitant la destination à ce qui a été redirigé.

### Q16 — Kali joint encore NTP et syslog du collecteur, et le ping des VM, par MGMT · ouvert (trouvé au jalon 3)

Le filtre `p6_ssh` (E35) ne vise que TCP 22. Depuis Kali (10.99.0.66, même segment MGMT, sans pare-feu entre eux) : `chronyd` du collecteur répond (`allow 10.99.0.0/24`), `rsyslog` accepte des lignes sur UDP 514, et p6-docker comme p6-collecteur répondent au ping. Aucun de ces cas n'est dans `tests-cloisonnement.sh`. Le plus gênant : **Kali peut écrire de fausses lignes `filterlog` dans `/var/log/p6/`**, que l'analyse (rôle 6) prendrait pour vraies.

**Hypothèse suivie :** le moteur montre la réalité (test `Q16_Kali_joint_encore_NTP_et_syslog_du_collecteur_et_le_ping_des_VM`). Durcissement proposé (E35 le prévoyait) : sur le collecteur, accepter UDP 123 et 514 seulement depuis 10.99.0.1, 10.99.0.20 et 10.99.0.2 ; ou, plus simple, une règle `nftables` sur Kali qui jette tout en sortie de eth1 sauf les réponses SSH.

---

## Questions répondues

### Q1 — Les scripts `tests.sh` et `tests-cloisonnement.sh` · répondu le 20 nov.

Les scripts sont dans `plateforme/labo/` (commit `3d7911b`, fusionné dans `role-1-plateforme-graphique`) : `docker/p6-etat-a/tests.sh` (12 tests), `kali/tests-cloisonnement.sh` (19 tests), plus `setup.sh`, `docker-compose.yml`, `setup-collecteur.sh`, `setup-kali.sh`, `restreindre-ssh.sh`, `acces/*.ps1` et le panneau. Ils servent au jalon 3 (liste exacte dans `docs/plan-plateforme.md`, section 5).

### Q2 — Outils du poste de travail · répondu le 20 nov.

SDK .NET **8.0.428** installé par `winget install Microsoft.DotNet.SDK.8`. Visual Studio 2022 Community avec la charge « Développement web et ASP.NET » (qui apporte LocalDB) est installé par l'équipe.

### Q3 — Faiblesses voulues de l'état A · répondu le 20 nov. (par `tests.sh`)

Trois faiblesses : `dmz-pivot → postgres:5432` (FA3 trop large), `client-03 → postgres:5432` (même L2, invisible pour pf), `client-03 → OPNsense 10.10.0.1:443` (FA4 : console d'administration joignable depuis le LAN).

### Q4 — Commandes du guide qui ne correspondent plus à la réalité · répondu le 20 nov.

- Étapes **marquées « fait »** : afficher la commande **réellement utilisée** (compte `karim` + `sudo`, jamais `root@` ; chemins et noms réels).
- Étapes **pas encore réalisées** : garder le texte du guide et signaler l'écart dans l'explication.
- **SSH d'OPNsense** désactivé pour l'instant (voir Q13).

### Q5 — Route par défaut de Kali · répondu le 20 nov.

Une seule route par défaut, via 203.0.113.1 (eth0) ; eth1 = 10.99.0.66 sur MGMT, sans passerelle. Confirmé par `setup-kali.sh`. L'affirmation « aucune ligne default » du rôle 5 ét. 1 est un reste de la conception, signalée dans l'explication.

### Q6 — Zone DNS · répondu le 20 nov.

`p6.lab` (`labo.test` est un reste de la conception). Dans `bind/db.p6.lab` : une seule zone publique, sans vues ; `www`, `api`, `fw`, `ns1` → 203.0.113.1.

### Q7 — Image des postes · répondu le 20 nov. (par `docker-compose.yml`)

Images réelles : `p6/outils:1` (postes et iperf3), `p6/api:1`, `nginx:1.27-alpine`, `postgres:17`, `internetsystemsconsortium/bind9:9.20`. Projet Compose `p6`.

### Q8 — chrony · répondu le 20 nov. (par `setup-collecteur.sh`)

`local stratum 8`, `allow 10.99.0.0/24`. Le `local stratum 10` du rôle 6 ét. 1 est un reste du guide, signalé dans l'explication.
