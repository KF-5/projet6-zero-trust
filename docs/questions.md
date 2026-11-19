# Questions pour Karim (rôle 1)

Informations qui manquent ou se contredisent entre le guide, `ECARTS.md` et la plateforme réelle. Je ne devine pas : chaque question dit ce qui est bloqué et ce que je ferai en attendant.

État : ouvert / répondu (avec la date et la réponse).

---

### Q1 — Les scripts `tests.sh` et `tests-cloisonnement.sh` · **bloquant pour le jalon 3** · ouvert

Le moteur réseau doit retrouver les 12 résultats de `tests.sh` et les 19 de `tests-cloisonnement.sh` (critère d'acceptation principal). Ces scripts ne sont pas dans le dépôt ; je n'en connais que les résumés :

- `tests.sh` : « FA1, FA2 (par NAT), FA3, FA4 passent ; les faiblesses voulues de l'état A existent ; FA5 bloque le reste ».
- `tests-cloisonnement.sh` : box 192.168.1.1, 8.8.8.8, Internet ; Windows ping / 445 / 3389 ; console OPNsense, SSH docker et collecteur ; FA1, FA2 par 203.0.113.1 passent ; FA5, FA6 bloqués. En les comptant, j'arrive à 14 ou 15 tests, pas 19.

**Demande :** pousser dans le dépôt (sans secret) `D:\Labo-P6\docker\p6-etat-a\tests.sh`, `D:\Labo-P6\kali\tests-cloisonnement.sh`, et si possible `setup.sh`, `docker-compose.yml`, `setup-collecteur.sh`, `setup-kali.sh`, `restreindre-ssh.sh`, ainsi que la sortie complète de chaque test (`docs/preuve-confinement.txt`).

En attendant : je reconstruis les deux listes à partir des résumés, marquées « provisoire » dans les tests xUnit.

### Q2 — Outils absents sur le PC de travail · **bloquant pour le jalon 2** · ouvert

Sur ce PC : runtime .NET 8.0.21, mais **ni SDK .NET 8, ni Visual Studio 2022, ni SQL Server LocalDB**. Sans le SDK, je ne peux ni créer la solution, ni compiler, ni lancer `dotnet test`.

**Demande :** installer Visual Studio 2022 (charge « Développement web et ASP.NET », qui apporte le SDK .NET 8 et LocalDB), ou au minimum le SDK .NET 8 (`winget install Microsoft.DotNet.SDK.8`) et SQL Server Express LocalDB. Je peux lancer la commande `winget` moi-même si tu me le demandes.

### Q3 — Liste des « faiblesses voulues » de l'état A · ouvert

`tests.sh` vérifie que « les faiblesses voulues de l'état A existent ». Lesquelles exactement ? Je suppose : S-05 (client-03 → postgres:5432, même VLAN, sans filterlog) et S-04 (dmz-pivot → postgres:5432, autorisé par FA3). Y en a-t-il d'autres (LAN → WAN, LAN → MGMT par FA4 « LAN → any ») ?

### Q4 — Commandes du guide qui ne correspondent plus à la réalité · ouvert

Plusieurs commandes du guide n'ont pas été mises à jour après les écarts E38 à E42 :

| Dans le guide | Réalité | Où |
|---|---|---|
| `ssh root@10.99.0.1`, `root@10.99.0.20`, `root@10.99.0.66` | compte `karim` + `sudo` (E40) ; SSH d'OPNsense **désactivé** | rôle 1 ét. 8 ; rôle 2 ét. 2, 6, 7, 9 ; rôle 3 ét. 4, 5, 6 ; rôle 5 ét. 1, 3, 5, 6 ; rôle 6 ét. 1, 3 |
| `/var/log/remote/opnsense.log`, `docker.log` | `/var/log/p6/10.99.0.1.log` (E42) | rôle 2 ét. 5 ; rôle 6 ét. 2 |
| réseaux Docker `lan`, `dmz`, `wan`, projet `projet6` | `p6_lan`, `p6_dmz`, `p6_wan`, projet `p6` (E38) | rôle 2 ét. 3 ; rôle 4 ét. 2, 3, 6, 8 |
| Docker Engine 29 (`docker-ce`) | Docker 26.1.5, paquets Debian | rôle 4 ét. 1 |

Ma proposition : garder le **texte exact du guide** dans le terminal (le prompt l'impose), signaler l'écart dans l'explication, et faire suivre au schéma la **réalité** (par exemple : `ssh root@10.99.0.1` depuis le collecteur → le paquet atteint OPNsense, autorisé par « Admin du pare-feu », mais le service SSH est arrêté : refusé). D'accord ? Ou préfères-tu que j'affiche directement la commande corrigée ?

### Q5 — Route par défaut de Kali · ouvert

`docs/interfaces.md` et le rôle 5 (mise à jour du 17 nov.) : Kali a la **passerelle 203.0.113.1**. Le rôle 5, étape 1, dit pourtant « `ip route` attendu : seulement 203.0.113.0/24 et 10.99.0.0/24, **AUCUNE** ligne default » et « `ping 1.1.1.1` → Network is unreachable ». Laquelle est vraie ? Le moteur en dépend : avec la passerelle, `ping 8.8.8.8` part vers OPNsense et est jeté par FA5 (délai dépassé) ; sans elle, il échoue tout de suite sur Kali (« Network is unreachable »).

### Q6 — Zone DNS de bind9 : `labo.test` ou `p6.lab` ? · ouvert

Rôle 4, étape 5 : la mise à jour dit « zone `p6.lab` faisant autorité », le fichier `named.conf` et toutes les commandes (`dig www.labo.test`, sonde C8, `test_flux`) utilisent `labo.test`. Quel nom répond réellement ? (Le décodeur DNS affichera le nom de la requête.)

### Q7 — Image des postes : `projet6/outils` ou `p6/outils:1` ? · ouvert

Rôle 4, étape 6 (mise à jour) : « Image des postes : `p6/outils:1` ». Le Dockerfile et les commandes : `projet6/outils`. Le nom réel sert seulement à l'affichage ; je garde le texte du guide et note `p6/outils:1` dans l'explication, sauf avis contraire.

### Q8 — chrony : `local stratum 8` ou `10` ? · ouvert

Rôle 1 ét. 4 et rôle 6 ét. 1 (mise à jour) : `local stratum 8`. Le fichier de configuration du rôle 6 ét. 1 : `local stratum 10`. Lequel est sur le collecteur ? (Affiché dans le décodeur NTP : champ « stratum » de la réponse.)

### Q9 — Noms des interfaces VLAN d'OPNsense en état B · ouvert

En état A, OPNsense 26.7 nomme les VLAN `vlan01` et `vlan02`. Les sorties attendues de l'état B (rôle 2 ét. 8 et 9) montrent `vlan0.11`, `vlan0.12`… L'état B n'est pas encore construit (prévu les 22–23 nov.). J'affiche les noms du guide (`vlan0.11`…) avec la mention « à vérifier », et je corrigerai quand l'état B existera. D'accord ?

### Q10 — Accès LAN → MGMT et LAN → WAN en état A · ouvert

FA4 est « LAN net → any ». Un poste du LAN (10.10.0.10) qui vise 10.99.0.1 (OPNsense sur MGMT) ou 10.99.0.20 est donc autorisé par pf. Est-ce le comportement observé (le filtre `p6_ssh` bloquant alors seulement le port 22) ? Ou une règle bloque-t-elle MGMT depuis le LAN avant FA4 ? Même question pour LAN → 203.0.113.66 (Kali).

### Q11 — Informations personnelles affichées · ouvert

Le prompt demande d'afficher l'adresse Tailscale 100.125.208.28, le nom de machine `best-technology`, le compte `p6labo` et le prénom Karim. Le dépôt est-il privé ? Si l'application ou le dépôt deviennent publics, je propose de masquer l'adresse Tailscale (ce n'est pas un secret, mais elle identifie ton PC).

### Q12 — Branche de travail · ouvert (sans blocage)

Le `README.md` prévoit la branche `role-1-plateforme` ; le prompt demande `role-1-plateforme-graphique`. J'ai créé `role-1-plateforme-graphique` comme demandé par le prompt. À renommer si tu préfères.
