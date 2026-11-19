# Style de référence — P6 Explorer

Étude du site https://protocol-explorer-alpha.vercel.app/ (19 novembre 2026), faite avec Edge sans tête piloté par le protocole DevTools : captures en thème sombre et clair à 1440 et 820 px de large, lecture du CSS livré (`index-CwjpA-vk.css`, 76 Ko) et des styles calculés.

On reprend **l'esthétique**, pas le code, les textes ni le logo. Toutes les valeurs ci-dessous sont des observations ; les valeurs retenues pour P6 Explorer sont dans la section 8.

## 1. Impression d'ensemble

- Interface **d'outil technique**, pas de site vitrine : tout tient dans un écran, sans défilement de page. Chaque panneau a son propre défilement.
- **Une seule police, à chasse fixe** (Iosevka) pour tout, titres compris. Titres en capitales, graisse 800, lettres espacées.
- Coins **presque carrés** (rayon 2 px dans 29 règles sur 44), bordures fines de 1 px, aucune ombre portée sauf sur les menus et dialogues. Effet « plan technique ».
- Fond sombre bleu nuit **`#0b1020`**, encre bleu pâle, accents vifs réservés aux paquets et aux états.
- Le schéma réseau est en **perspective isométrique** (machines en volumes filaires) sur une grille de points. Les liens sont des tirets fins ; un lien actif « coule » (tirets animés).

## 2. Palette observée

Le site définit chaque couleur par une variable « clé » (`--k-fill-…`, `--k-text-…`, `--k-line-…`, `--k-accent-…`) redéfinie en thème sombre. Valeurs principales :

| Rôle | Clair | Sombre |
|---|---|---|
| Fond de page (`--bg`) | `#fdfdfe` | `#0b1020` |
| Encre forte (titres) | `#00238a` | `#b7c6f3` |
| Encre normale | `#0032a0` | `#afc4f2` |
| Encre douce (descriptions) | `#5a73a8` | `#acb8d3` |
| Encre discrète (légendes) | `#7d96c6` | `#859cc9` |
| Surfaces de panneau (sombre) | — | `#142038` à `#1d2f51` (bleus très sombres, 6 paliers) |
| Accent actif (bouton Lecture, onglet choisi) | `#1f5fe0` | même teinte, fond plein |
| DNS | bleu `#0a78f5` | |
| TCP | vert `#00a86b` (sombre `#00ef98`) | |
| Requête | ambre `#f8b900` | |
| Réponse | violet `#7656e0` | |
| Couches du décodeur | Ethernet gris `#546588`, IPv4 bleu `#1166e3`, TCP vert `#12a56b` | |

Thème : `<meta name="color-scheme" content="light dark">`, deux `theme-color` (`#fdfdfe` / `#0b1020`), bouton soleil / lune dans l'en-tête, choix mémorisé.

## 3. Typographie

- Police unique : **Iosevka** (woff2 hébergé par le site, graisses 400 à 800, `font-display: swap`). Pile de repli : `ui-monospace, monospace`.
- Corps : 16 px à la racine, mais l'interface utilise surtout **11 à 13,5 px** (tailles les plus fréquentes : 12, 11, 11,5 px).
- Titres : capitales, graisse 800, espacement 0,05 à 0,085 em. Logo 20 px, titres de panneau 17–20 px, nom du protocole choisi 26 px.
- Lettres-chiffres tabulaires : les colonnes (temps, adresses, tailles) s'alignent naturellement grâce à la chasse fixe.

## 4. Mise en page

Écran large (≥ 1360 px) : grille à trois colonnes + bande basse.

```
┌──────────────── en-tête (logo · couches OSI 1–7 · recherche Ctrl K · dispositions · thème) ────────────────┐
│ Liste (≈ 300 px)       │ Schéma réseau (1fr)                         │ Vue d'ensemble (≈ 275–305 px) │
│ onglets filtrants      │ onglets RÉSEAU / SÉQUENCE                   │ titre + pastille « ACTIF »    │
│ protocole déplié :     │ ── ou, au clic sur un paquet : ──           │ paragraphe simple             │
│ 1..5 étapes numérotées │ panneau de décodage (couches + hexa)        │ tableau clé / valeur          │
│                        ├─────────────────────────────────────────────┴───────────────────────────────┤
│                        │ FLUX DE PAQUETS · légende · vitesse 1× · ◀ · ▶ LECTURE · AVANCER · ↺          │
│                        │ N° · TEMPS · SOURCE · DESTINATION · PROTOCOLE · INFO · TAILLE                  │
└────────────────────────┴───────────────────────────────────────────────────────────────────────────────┘
```

- Variables de mise en page : `--details-w: 276px / 304px`, `--flow-h: 228px` (hauteur de la bande des paquets).
- Panneaux séparés par 12–16 px de vide, chacun bordé de 1 px, sans fond contrasté fort.
- À 820 px, les colonnes s'empilent : le décodeur prend toute la largeur et passe en une colonne (champ, valeur, explication).
- Boutons de disposition dans l'en-tête : masquer la liste, masquer la bande du bas, plein écran, raccourcis clavier.

## 5. Composants

| Composant | Ce qu'on observe | Reprise dans P6 Explorer |
|---|---|---|
| **Liste à étapes** | Protocole déplié en 5 étapes numérotées (pastille ronde, titre en capitales, sous-titre d'une ligne), trait vertical qui se remplit à mesure | Liste des étapes d'un rôle, et des commandes de l'étape choisie |
| **Onglets compteurs** | « TOUS 39 · BASE 11 · APPLICATION 19 » avec badge | Filtres de la vue rôle (« à faire », « fait », « questions ouvertes ») |
| **Schéma isométrique** | Machines en volumes filaires, nom + IP sous chaque machine, liens en tirets, grille de points en fond | **Pas repris** : on garde le schéma plan du guide (zones colorées), plus lisible pour 15 machines et projeté en salle ; on reprend le trait fin, les étiquettes nom + IP et la grille de points discrète |
| **Paquet animé** | Petite étiquette colorée (`packet-tag`) avec le nom du protocole, qui glisse le long du lien ; le lien parcouru s'anime (`lane-flow`, tirets décalés de 11 px en 0,55 s) | Paquet = étiquette colorée par protocole, chemin calculé ; couleur du verdict à l'arrivée |
| **Tableau de paquets** | Style Wireshark : n° dans une pastille colorée, temps relatif, source, destination, protocole, info, taille ; ligne active marquée d'un filet vertical à gauche (`inset 3px 0 0`) | Journal des paquets de la commande en cours |
| **Décodeur** | En-tête « 4 SYN-ACK · 0.041s · src → dst · TCP · 66 B », boutons ‹ › ×. Barre des couches proportionnelle à leur taille (Ethernet 14 · IPv4 20 · TCP 32). À gauche les champs (nom, valeur, phrase d'explication), à droite l'hexa (`0000 00 5e 00 …` + ASCII), octets colorés comme leur couche. Badge **EX.** sur les valeurs d'exemple ; « ·· » pour les octets non montrables | Repris presque tel quel (vue partielle `_DecodeurOctets`), survol synchronisé champ ↔ octets ; badge « EX. » pour les valeurs inventées (ports source, numéros de séquence, MAC) |
| **Vue d'ensemble** | Titre 26 px, sous-titre, pastille verte « ● ACTIF », paragraphe, tableau clé / valeur (COUCHE, PORT, TRANSPORT…) | Panneau d'explication : commande, ce qu'elle fait, règle qui décide, machine, shell |
| **Contrôles** | Vitesse « 1× » en liste déroulante, ‹ précédent, ▶ LECTURE (fond accent plein), AVANCER, ↺ réinitialiser | ⏮ ▶/⏸ ⏭, vitesse ×0,5 à ×4, curseur de chronologie |
| **Recherche** | Champ en haut, raccourci `Ctrl K` affiché dans une touche dessinée | Recherche d'une commande ou d'un terme du glossaire (jalon 7) |

## 6. Animations et rythme

Deux courbes, réutilisées partout :

```css
--ease-out: cubic-bezier(.16, 1, .3, 1);   /* entrées, déplacements : démarrage vif, arrivée douce */
--ease-in:  cubic-bezier(.5, 0, .75, 0);    /* sorties */
```

| Usage | Durée observée |
|---|---|
| Menus, dialogues (`menu-in`, `dialog-in` : fondu + glissement de 4–8 px + échelle 0,97) | 0,20–0,22 s |
| Changement d'étape (`step-in` : fondu + glissement de 6 px) | 0,38 s |
| Pastille d'étape terminée (`step-pop`, échelle 1,2) | 0,42 s |
| Trait d'avancement (`stroke-dashoffset`) | 0,35–0,40 s, décalé de 0,12 s |
| Panneaux qui glissent (`transform`) | 0,25–0,50 s |
| Barre de progression (`seg-fill`) | 0,7 s |
| Lien actif (`lane-flow`) | boucle de 0,55 s |
| Couleurs (fond, remplissage) | 0,2–0,3 s |
| Curseur du terminal (`caret`) | clignote toutes les 0,9 s (`steps(1)`) |

Trois blocs `@media (prefers-reduced-motion: reduce)` coupent toutes les animations et transitions (`animation: none !important; transition: none !important`). Une classe `.instant` fait de même quand on saute d'étape.

À la lecture, les paquets partent l'un après l'autre (≈ 1 à 1,5 s par paquet à ×1) ; le tableau se remplit au même rythme.

## 7. Ce qu'on ne reprend pas

- Le code, les textes, le logo, la police Iosevka (on n'a pas vérifié sa licence de redistribution pour ce fichier précis ; on prend une police libre équivalente, voir 8.2).
- Le schéma isométrique (voir section 5).
- Le tout-capitales pour les textes longs : nos lecteurs débutants lisent des phrases complètes en français ; les capitales restent pour les titres courts.

## 8. Ce que P6 Explorer retient

### 8.1 Couleurs

Base : les variables de `sources/guide/style.css`, **seul le fond change** pour le bleu nuit de la référence. Les couleurs de zone et de rôle sont celles du guide (imposées).

| Variable | Clair | Sombre (défaut) | Usage |
|---|---|---|---|
| `--bg` | `#F6F8FC` | `#0B1020` | fond de page (sombre = `theme-color` de la référence) |
| `--surface` | `#FFFFFF` | `#111A30` | panneaux |
| `--surface-2` | `#E9EEF7` | `#17223D` | en-têtes de panneau, lignes survolées |
| `--line` | `#C7D1E3` | `#26335A` | bordures 1 px |
| `--fg` | `#0F1A33` | `#DCE4F6` | texte |
| `--muted` | `#4F5E7D` | `#93A3C8` | texte secondaire (contraste AA vérifié au jalon 2) |
| `--acc` | `#0C6A66` | `#3DB5AC` | **OPNsense (sarcelle)**, bouton Lecture, focus |
| `--wan` | `#C0392B` | `#F07167` | zone WAN |
| `--dmz` | `#B7791F` | `#E8B04B` | zone DMZ |
| `--lan` | `#2E7D32` | `#66BB6A` | zone LAN |
| `--mgmt` | `#6D4FC2` | `#A78BFA` | zone MGMT |
| `--trunk` | `#1F6FD1` | `#64A5F5` | trunk 802.1Q |
| `--ids` | `#C2510F` | `#F08B52` | Suricata, alertes |
| `--ok` / `--bad` / `--warn` | `#1B7A3A` / `#A61B1B` / `#8A6100` | `#5CC27C` / `#F2696F` / `#E2B04A` | verdicts : passé, bloqué, à vérifier |
| `--r1` … `--r6` | `#4F46E5 #0C6A66 #C2510F #8A5A00 #B0185D #0E6A9E` | `#8B85F7 #3DB5AC #F08B52 #D9A441 #F06AA6 #5DB3E8` | cartes et pastilles de rôle |

Note : le guide n'a pas de variable dédiée à OPNsense ; la sarcelle demandée est `--acc` (même valeur que `--r2`, le rôle pare-feu). On ajoute un alias `--opn: var(--acc)`.

Couleurs de **protocole** des paquets (étiquettes et pastilles du tableau), choisies pour ne pas se confondre avec les zones : DNS bleu, TCP/HTTP(S) cyan, ICMP gris, SSH violet clair, syslog et NTP gris bleuté, PostgreSQL ambre clair. Le **verdict** prime toujours : un paquet bloqué devient `--bad` et s'arrête sur le pare-feu avec une croix.

Thème : sombre par défaut ; si l'utilisateur n'a jamais choisi, on suit `prefers-color-scheme` ; le choix est mémorisé (cookie lu par le Layout, pour éviter le flash au chargement). Implémentation : `:root[data-theme="light"]` / `[data-theme="dark"]`, plus `@media (prefers-color-scheme)` quand aucun choix n'est fait.

### 8.2 Polices

Hébergées dans `wwwroot/fonts/` (la plateforme doit marcher hors ligne en soutenance) :

- **IBM Plex Mono** (licence OFL, déjà utilisée par le guide) : titres, terminal, adresses, tableaux, hexa. Titres en capitales, graisse 700, espacement 0,06 em, comme la référence.
- **IBM Plex Sans** : phrases d'explication et formulaires, plus reposantes pour des paragraphes en français.

Tailles : 14 px pour le texte courant (12 px minimum partout, pour rester lisible projeté), 13 px dans les tableaux, titres de panneau 17 px.

### 8.3 Mise en page de la vue étape

```
┌ en-tête : P6 EXPLORER · Rôles 1–6 · État A|B · recherche · thème · connexion ┐
│ Étapes (≈ 300 px)     │ Schéma dynamique (SVG, 1fr)                            │
│ 1 ✔ Installer VMware  │ calques : zones · IP · règles · journaux · alertes     │
│ 2 ✔ Réseaux virtuels  │                                                        │
│ ▸ 4 Installer…        ├─────────────────────────────┬──────────────────────────┤
│   c01 scp …           │ Terminal rejoué              │ Explication / décodeur   │
│   c02 ssh -t …        │ karim@p6-docker:~$ ▍         │ ce que fait la commande  │
│                       │ sortie attendue              │ règle qui décide (FA1…)  │
│                       │                              │ octets (si paquet)       │
│                       ├─────────────────────────────┴──────────────────────────┤
│                       │ ⏮ ▶ ⏭ · ×0,5 … ×4 · ━━━━●━━━━━ c03/12 · Paquets (tableau) │
└───────────────────────┴────────────────────────────────────────────────────────┘
```

Portable 13 pouces (1280–1440 px) : cette grille. Vidéoprojecteur (souvent 1280×720 ou 1024×768) : un bouton « Présentation » masque la liste et agrandit le schéma, comme les boutons de disposition de la référence. En dessous de 900 px : tout s'empile.

### 8.4 Animations

Mêmes courbes `--ease-out` / `--ease-in` et mêmes durées que la section 6. Paquet : 0,9 s par saut à ×1 (un saut = un lien du chemin), pause de 0,25 s sur le pare-feu le temps d'afficher la règle. Terminal : 35 caractères par seconde à ×1, sortie affichée d'un bloc. Toute la vitesse est multipliée par le facteur choisi (×0,5 à ×4).

`prefers-reduced-motion: reduce` : pas de déplacement ; le paquet apparaît directement sur chaque machine du chemin, une étape par appui sur ⏭, et la commande s'affiche entière.
