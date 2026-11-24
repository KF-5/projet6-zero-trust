# Prompt Claude Code : section « Vidéo » de P6 Explorer (plans P-1 à P-8)

> À coller tel quel dans Claude Code, lancé à la racine du dépôt `projet6-zero-trust`.

---

## Contexte

Tu travailles sur **P6 Explorer** (`app/`), l'application ASP.NET Core MVC (.NET 8, EF Core, LocalDB) du Projet 6 « Architecture Zero Trust ». Elle rejoue graphiquement les commandes du labo sur un schéma isométrique (`Views/Shared/_Schema.cshtml`) animé par `wwwroot/js/lecteur.js`, avec un moteur réseau (`P6Explorer.Domain/Moteur/`) qui calcule le chemin et le verdict de chaque paquet (`POST /api/simulation`).

L'équipe doit rendre une **vidéo d'installation et de tests** (≤ 10 min). Huit plans de cette vidéo, **P-1 à P-8**, doivent être filmés dans P6 Explorer. Aujourd'hui, certains peuvent l'être avec les pages existantes, d'autres non. Ta mission : ajouter une section **« Vidéo »** où chaque plan est une **scène prête à filmer** : on ouvre la page, on appuie sur Espace, la scène se joue seule, avec la bonne durée, puis on arrête l'enregistrement.

Ce travail sert aussi de projet pour le cours « Frameworks de développement » (.NET) : respecte l'architecture en couches existante (Domain / Application / Infrastructure / Web), l'injection de dépendances, les ViewModels et les tests xUnit.

## Avant d'écrire du code : lis

1. `app/README.md` et `docs/notions-du-cours.md`.
2. `P6Explorer.Web/Views/Shared/_Schema.cshtml`, `_Layout.cshtml`, `_PanneauExplication.cshtml`, `Views/Simulation/Index.cshtml` et `Demonstration.cshtml`.
3. `P6Explorer.Web/wwwroot/js/lecteur.js`, en particulier les modes `simulation` et `demo` et les fonctions `nettoyer`, `cadrer`, `mettreAuPoint`, `allumer`, `bulle`, `animerPaquet`, `afficherResultat`, `aFilterlog`.
4. Le contrôleur `SimulationController` et le contrôleur d'API `/api/simulation`.
5. `donnees/topologie/etat-A.json` et `etat-B.json` (identifiants des nœuds, liens, règles).
6. `donnees/politique/flows.csv`, `segments.csv`, `hosts.csv`, `tests-A.csv`, `tests-B.csv` et `LISEZ-MOI.md`.
7. Le fichier de configuration `Amorcage:DossierDonnees` (comment l'application trouve `donnees/`).

Puis **écris ton plan** (fichiers créés et modifiés, une ligne chacun) et attends ma validation avant de coder.

## Règles générales (non négociables)

- **Ne rien inventer.** Chaque verdict affiché vient du moteur (`/api/simulation`) ; chaque chiffre vient d'un fichier de `donnees/` ou de `resultats/`. Une valeur qui n'est pas réelle est **toujours** accompagnée d'un bandeau visible « ILLUSTRATION — données fictives ».
- **Ne pas dupliquer le moteur ni le lecteur.** Les scènes réutilisent `_Schema`, `_PanneauExplication`, `/api/simulation` et les fonctions de `lecteur.js`. Tout ajout dans `lecteur.js` est **additif** : un nouveau mode `video`, sans changer le comportement des modes `etape`, `simulation` et `demo`.
- **Aucune page existante ne change de comportement.** Seul ajout visible ailleurs : un lien « Vidéo » dans la navigation et le pied de page.
- **Sécurité à l'écran** (ces pages seront filmées) :
  - Les pages `/Video` ne doivent **jamais** afficher les adresses réservées (`AdressesReservees`, adresses Tailscale `100.x`), **même si l'utilisateur est connecté** : force `MembreConnecte = false` dans le `SchemaVM` de ces pages.
  - Aucun nom de compte, aucune adresse e-mail (`admin@p6.lab`…), aucun bouton de connexion en mode tournage.
  - Pages en `[AllowAnonymous]`, en lecture seule (aucune écriture en base).
- Noms identiques à ceux du labo : p6-opnsense, p6-docker, p6-collecteur, p6-kali ; FA1–FA6 (état A), F01–F09 et F99 (état B) ; S-05 ; adresses 203.0.113.x, 10.10.0.x, 10.20.0.x, 10.99.0.x.
- Code et textes en français, comme le reste de l'application.

## Ce qu'il faut construire

### 1. Les scènes décrites dans un fichier, pas dans le code

Crée `donnees/video/scenes.json`, modifiable sans recompiler. Il décrit les 8 scènes. Chaque scène contient : `id` (« P-1 »…), `titre`, `sequence` (numéro de la séquence de la vidéo), `duree_s` (durée cible), `etat` de départ (A ou B), `cible` (booléen : afficher « Ce qui suit est la cible »), et une liste d'`etapes` jouées dans l'ordre. Types d'étapes à gérer :

| Type | Paramètres | Effet |
|---|---|---|
| `pause` | `duree_s` | attendre |
| `legende` | `texte`, `duree_s` | bandeau de texte en bas de l'écran (gros, lisible) |
| `titre` | `texte`, `duree_s` | grand titre centré sur fond sombre |
| `cadrer` | `noeuds` (liste d'identifiants), `duree_s` | zoom/caméra sur ces nœuds (`cadrer` / `mettreAuPoint`) |
| `allumer` | `noeuds`, `texte?` | allume les nœuds, bulle facultative |
| `surligner_zone` | `zones` (WAN, DMZ, LAN, MGMT, TRUNK…) | met des zones en avant, atténue le reste |
| `simulation` | `source`, `destination`, `protocole`, `port`, `etat`, `legende?` | appelle `POST /api/simulation` et anime le paquet avec la règle qui décide |
| `bascule_etat` | `etat` | remplace le schéma par celui de l'autre état (fondu 0,5 s) |
| `cote_a_cote` | — | affiche les schémas A et B côte à côte |
| `matrice` | `etat`, `ligne_mise_en_avant?` | joue la vue matrice (section 4) |
| `resultats` | — | joue la vue résultats (section 5) |
| `resume` | — | joue l'écran de conclusion (section 6) |

Crée dans le Domain les types correspondants et, dans l'Infrastructure, un service qui lit et **valide** ce fichier (8 scènes, identifiants uniques, types connus, identifiants de nœuds présents dans la topologie, somme des durées d'étapes à ±2 s de `duree_s`). Une erreur de validation s'affiche en clair sur `/Video`, pas en exception.

### 2. Les pages

- `GET /Video` : la liste des 8 scènes (id, titre, séquence, durée, état, « prête » ou « illustration » ou « cible »), avec un lien « Ouvrir en mode tournage ».
- `GET /Video/{id}` (ex. `/Video/P-5`) : la scène en **mode tournage** :
  - **sans** l'en-tête, la navigation ni le pied de page (layout dédié `_LayoutTournage.cshtml`) ;
  - une **scène fixe de 1920 × 1080**, mise à l'échelle pour tenir dans la fenêtre (letterbox), fond sombre uni ;
  - polices agrandies (lisibles sur un portable sans zoomer : au moins 28 px pour les légendes, 22 px pour les étiquettes) ;
  - **2 s d'image immobile** au début, puis rien ne bouge tant qu'on n'a pas appuyé sur **Espace** ;
  - Espace = lecture / pause, **R** = recommencer la scène, **→** = scène suivante, **←** = précédente ; aucune touche ne fait apparaître de commande à l'écran ;
  - le curseur de la souris disparaît après 2 s d'immobilité ;
  - petit coin discret en haut à gauche : « P-5 » (désactivable avec `?propre=1` pour la prise finale) ;
  - si `cible: true` : bandeau permanent « Ce qui suit est la cible » ;
  - à la fin de la scène, la dernière image reste figée (pas de retour automatique au début).
- Ajoute « Vidéo » dans la navigation principale et dans le pied de page, à côté de « Démonstration ».

### 3. Les 8 scènes à écrire dans `scenes.json`

Durées issues du plan de tournage. Vérifie chaque verdict avec le moteur **avant** de l'écrire dans une légende ; si le moteur donne un autre résultat que celui attendu ci-dessous, **ne force rien** : signale-le dans ton rapport final.

**P-1 — Ouverture : le problème (séquence 1, 25 s, état A)**
*Existe en partie (mode simulation) : il faut l'enchaîner en une scène.*
1. `titre` « Un seul pare-feu à l'entrée » (3 s).
2. `surligner_zone` WAN, DMZ, LAN ; `legende` « Une PME : un pare-feu périmétrique protège l'entrée » (4 s).
3. `simulation` kali → 203.0.113.1, Http, 80, état A — entre par la redirection FA1 jusqu'à nginx ; `legende` « L'attaquant entre par la DMZ ».
4. `simulation` dmz-pivot → 10.10.0.70, PostgreSql, 5432, état A — passe par FA3 ; `legende` « Depuis la DMZ, il atteint la base de données ».
5. `simulation` client-03 → 10.10.0.70, PostgreSql, 5432, état A ; `legende` « À l'intérieur, il circule librement ».
6. `pause` jusqu'à 25 s.
Si `kali` ou `dmz-pivot` ne sont pas acceptés comme source par le moteur, choisis la source la plus proche qui existe et dis-le.

**P-2 — L'architecture (séquence 2, 30 s)**
*Existe (schéma de l'accueil + bascule A/B) : à dupliquer et mettre en scène.*
1. Schéma de l'état A complet (4 s), `legende` « Quatre machines virtuelles sur un seul PC ».
2. `allumer` successivement opnsense, docker, collecteur, kali (2,5 s chacun) avec une bulle : « p6-opnsense · pare-feu », « p6-docker · 10 services », « p6-collecteur · journaux », « p6-kali · attaquant ».
3. `surligner_zone` WAN, LAN, DMZ, MGMT (4 s), `legende` « Quatre réseaux : WAN, LAN, DMZ, MGMT ».
4. `cote_a_cote` (12 s) : à gauche « État A — pare-feu périmétrique », à droite « État B — micro-segmentation + IPS ».

**P-3 — VLAN et services par zone (séquence 4, 15 s, état A)**
*Existe (schéma) : à dupliquer et cadrer.*
1. `cadrer` opnsense + le trunk (4 s), `legende` « Un seul câble, deux VLAN : 10 (LAN) et 20 (DMZ) ».
2. `surligner_zone` DMZ + `allumer` nginx, bind9, api, dmz-pivot (5 s), `legende` « DMZ · 10.20.0.0/24 : site web, DNS, API ».
3. `surligner_zone` LAN + `allumer` client-01, client-03, postgres, admin (5 s), `legende` « LAN · 10.10.0.0/24 : postes et base de données ».

**P-4 — La politique comme donnée (séquence 5, 20 s)**
*N'existe pas : nouvelle vue « matrice » (section 4).*
1. `matrice` état A (8 s), ligne mise en avant : FA3 (« Toute la DMZ vers la base »).
2. `matrice` état B (10 s), ligne mise en avant : F04 (« Seul accès autorisé à la base »).
3. `legende` « Un fichier décrit la politique ; les règles et les tests en sont déduits » (2 s, superposée).

**P-5 — S-05 en état A (séquence 6, 15 s, état A)**
*Existe (« Et si ? ») : à dupliquer.*
1. `cadrer` client-03 + postgres (2 s).
2. `simulation` client-03 → 10.10.0.70, PostgreSql, 5432, état A ; `legende` « S-05 : un poste compromis atteint la base ».
3. `legende` « Même réseau : le paquet ne traverse pas le pare-feu — lignes dans ses journaux : N » où **N est calculé** à partir du résultat du moteur (journalisation du pare-feu, voir `aFilterlog`), jamais écrit en dur.

**P-6 — Passage à six segments ; S-05 arrêté (séquence 7, 25 s, état B, `cible: true` tant que l'état B n'est pas construit)**
*Existe en partie (bascule + « Et si ? ») : à enchaîner.*
1. Schéma de l'état A (2 s), puis `bascule_etat` B (2 s), `legende` « État B : chaque service dans son propre segment ».
2. `surligner_zone` des segments B un par un (DMZ-WEB, DMZ-DNS, DMZ-APP, LAN-USERS, LAN-DB, LAN-ADMIN, ~1,3 s chacun) avec leur VLAN (21, 22, 23, 11, 12, 13), lus dans `segments.csv`.
3. `simulation` client-03 → 10.10.0.70, PostgreSql, 5432, état B ; `legende` « Le même mouvement latéral est bloqué et journalisé (règle F99) » — la règle affichée est celle renvoyée par le moteur.
4. `pause` jusqu'à 25 s.
Le champ `cible` se règle dans `scenes.json` : l'équipe le passera à `false` quand l'état B réel sera filmé.

**P-7 — Lecture des résultats A contre B (séquence 8, 20 s)**
*N'existe pas : nouvelle vue « résultats » (section 5).*

**P-8 — Conclusion (séquence 9, 15 s)**
*N'existe pas : nouvel écran « résumé » (section 6).*

### 4. Nouvelle vue « matrice de flux » (P-4)

But : montrer que **la politique est une donnée** : un tableau lisible qui devient des règles et des tests.

- Source : `donnees/politique/flows.csv`, `segments.csv`, `hosts.csv`, `tests-A.csv`, `tests-B.csv` (lus au démarrage par un service de l'Infrastructure, chemin via `Amorcage:DossierDonnees`). Aucun chiffre en dur.
- Affichage, pour l'état demandé :
  - **une grille** : lignes = sources, colonnes = destinations, telles qu'elles apparaissent dans `flows.csv` pour cet état (zones, segments ou hôtes ; `any` est affiché « tout ») ;
  - une case **verte** porte l'identifiant du flux (FA1, F04…) et son port ; une case vide est **grise** et marquée « refus par défaut » (FA5 en A, F99 en B) ;
  - en dessous, trois compteurs calculés : nombre de flux autorisés, nombre de tests générés (lignes de `tests-<état>.csv`), dont « pass » et « block ».
- Animation (mode `matrice` de la scène) : la grille apparaît ligne par ligne (≈ 0,3 s par ligne), puis la ligne `ligne_mise_en_avant` s'allume ; un panneau à droite montre alors **la ligne de `flows.csv`** (source, destination, proto, port, justification) puis **les tests qui la vérifient**, lus dans `tests-<état>.csv` (`flux_ref` = cet identifiant), avec leur attendu pass/block.
- Mentionne en petit sous la grille : « Fichiers : politique/flows.csv → c2.py → règles + tests.csv ». Ne génère pas toi-même de syntaxe de règle OPNsense : on montre les vraies lignes des fichiers.
- Rends aussi cette vue accessible hors tournage : `GET /Politique?etat=A|B`, avec un lien dans la navigation. Elle sert à expliquer la politique aux membres de l'équipe.

### 5. Nouvelle vue « résultats A contre B » (P-7)

Les vrais résultats n'existent pas encore : le rôle 6 les produit le samedi 28 novembre. La vue doit donc fonctionner **avant** (en illustration) et **après** (avec les vraies données), sans changer de code.

- Source : `resultats/synthese/synthese.csv`, au format suivant (crée aussi `resultats/synthese/FORMAT.md` qui le décrit, pour le rôle 6) :

  ```
  indicateur,libelle,etat,valeur,unite
  taux_detection,Taux de détection,A,…,%
  taux_detection,Taux de détection,B,…,%
  temps_detection,Temps moyen de détection,A,…,s
  temps_detection,Temps moyen de détection,B,…,s
  effet_debit,Débit avec la sécurité active,A,…,Mbit/s
  effet_debit,Débit avec la sécurité active,B,…,Mbit/s
  ```

- **Si le fichier existe et est valide** : trois paires de barres A / B (une par indicateur), animées l'une après l'autre (≈ 5 s chacune), la valeur écrite au bout de chaque barre, l'unité affichée. Sous le graphique : « Source : resultats/synthese/synthese.csv ». Le nombre d'indicateurs suit le fichier (ne suppose pas qu'il y en a exactement trois).
- **Si le fichier est absent ou invalide** : même vue avec des valeurs d'exemple tirées de `donnees/video/resultats-exemple.csv`, **et** un bandeau rouge permanent, en diagonale ou en haut, « ILLUSTRATION — données fictives », impossible à rater à l'image. La page `/Video` marque alors P-7 et P-8 « illustration ».
- Pas de bibliothèque de graphiques externe : SVG généré côté serveur ou dans `lecteur.js`, dans le style du site (`site.css`, polices IBM Plex).

### 6. Nouvel écran « résumé » (P-8)

Écran de fin en trois temps (15 s au total) :

1. **A contre B (6 s)** : deux colonnes, chaque ligne apparaît l'une après l'autre. Toutes les valeurs sont **calculées** depuis les fichiers :
   - segments : nombre de segments de l'état A et de l'état B (`segments.csv`, hors WAN et MGMT) ;
   - flux autorisés et tests générés (section 4) ;
   - S-05 (client-03 → postgres:5432) : verdict du moteur en A puis en B ;
   - détection : « IDS : signale » / « IPS : bloque » (texte fixe, mis dans `scenes.json`) ;
   - si `synthese.csv` existe : une ligne par indicateur ; sinon ces lignes sont absentes (pas d'illustration ici).
2. **L'équipe (5 s)** : six cartes « Rôle 1 — Plateforme », « Rôle 2 — Pare-feu », « Rôle 3 — IDS/IPS », « Rôle 4 — Services », « Rôle 5 — Attaques », « Rôle 6 — Analyse », plus « Plateforme graphique ». Libellés lus dans la base (rôles) ; prénoms facultatifs via un champ `equipe` de `scenes.json` (vide par défaut).
3. **Fin (4 s)** : la phrase « Le Zero Trust ne se décrète pas, il se prouve. » puis « Tout pour reproduire le labo : README.md » (texte modifiable dans `scenes.json`).

## Tests à ajouter (`P6Explorer.Tests`)

- `scenes.json` : se charge, 8 scènes P-1 à P-8, identifiants de nœuds tous présents dans la topologie, durées cohérentes.
- Pour chaque étape `simulation` des scènes : le moteur rend un verdict ; vérifie en particulier P-5 (client-03 → postgres, état A) = **passe** et P-6 (même paquet, état B) = **bloqué**.
- Matrice : nombre de tests affichés = nombre de lignes de `tests-A.csv` (8) et de `tests-B.csv` (42) ; chaque `flux_ref` des tests existe dans `flows.csv`.
- Résultats : sans `synthese.csv`, la page contient le bandeau « ILLUSTRATION » ; avec un fichier valide de test, elle ne le contient pas et affiche les valeurs du fichier.
- Pages : `/Video`, `/Video/P-1` à `/Video/P-8` et `/Politique` répondent 200 ; **même connecté**, leur HTML ne contient aucune adresse `100.` (Tailscale), aucun `@` d'adresse e-mail et aucun nom de compte.
- Les tests existants passent toujours.

## Fin du travail

1. `dotnet build app/P6Explorer.sln` → 0 avertissement, 0 erreur ; `dotnet test app/P6Explorer.sln` → tout passe.
2. Lance l'application et ouvre chaque scène : vérifie qu'elle se joue seule, à la bonne durée (±2 s), sans erreur dans la console.
3. Ajoute au `app/README.md` une section « Tourner les plans de la vidéo » : adresse de chaque scène, touches, `?propre=1`, comment passer `cible` à `false`, où le rôle 6 dépose `synthese.csv`, et les réglages d'enregistrement (OBS, capture de fenêtre, navigateur en plein écran F11, zoom 100 %, 1920 × 1080, 30 i/s).
4. Commite sur une branche `video-plans` (messages en français), sans pousser sur `main`.
5. Donne-moi un rapport court : pour chaque scène P-1 à P-8, « prête », « illustration » ou « cible », sa durée mesurée, et tout écart entre le moteur et ce qui était attendu ci-dessus.
