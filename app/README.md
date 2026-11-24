# P6 Explorer — lancer l'application

Application ASP.NET Core MVC (.NET 8) qui rejoue graphiquement les commandes du Projet 6. Ce guide suppose que tu n'as jamais lancé un projet .NET.

## 1. Installer (une seule fois)

| Outil | Comment | Vérifier |
|---|---|---|
| **Visual Studio 2022** (Community suffit) | Installateur Visual Studio, charge de travail **« Développement web et ASP.NET »**. Elle installe aussi le SDK .NET 8 et **SQL Server LocalDB**. | Visual Studio s'ouvre |
| SDK .NET 8 | Inclus ci-dessus (ou `winget install Microsoft.DotNet.SDK.8`) | `dotnet --list-sdks` affiche `8.0.…` |
| SQL Server LocalDB | Inclus ci-dessus | `sqllocaldb info` affiche `MSSQLLocalDB` |
| Outil EF Core (pour les migrations en ligne de commande) | `dotnet tool install --global dotnet-ef --version 8.0.11` | `dotnet ef --version` |
| Node.js 18 ou plus | seulement pour réimporter le guide (étape 5) | `node --version` |

## 2. Choisir le mot de passe du compte admin (une seule fois)

Le compte `admin@p6.lab` (rôle 1) est créé au premier démarrage. Son mot de passe ne doit **jamais** être écrit dans le code ni dans `appsettings.json` : il va dans les *user-secrets*, un fichier rangé dans ton profil Windows, hors du dépôt.

Dans un terminal, à la racine du dépôt :

```powershell
dotnet user-secrets set "Amorcage:MotDePasseAdmin" "<ton mot de passe>" --project app/P6Explorer.Web
```

Règles : au moins 10 caractères, avec une majuscule, une minuscule et un chiffre. Dans Visual Studio, on peut aussi faire : clic droit sur `P6Explorer.Web` › **Gérer les secrets utilisateur**, puis écrire :

```json
{ "Amorcage": { "MotDePasseAdmin": "<ton mot de passe>" } }
```

Sans mot de passe, l'application démarre quand même : seul le compte admin n'est pas créé (un avertissement l'indique dans la fenêtre de sortie).

## 3. Lancer (F5)

1. Ouvre `app/P6Explorer.sln` dans Visual Studio.
2. Vérifie que le projet de démarrage est **P6Explorer.Web** (en gras dans l'Explorateur de solutions ; sinon clic droit › *Définir comme projet de démarrage*).
3. Appuie sur **F5**. La première fois, Visual Studio propose de faire confiance au certificat HTTPS de développement : accepte.

Au premier démarrage, l'application :

1. crée la base `P6Explorer` dans LocalDB en appliquant les **migrations** (dossier `P6Explorer.Infrastructure/Persistance/Migrations`) ;
2. la remplit avec les 47 étapes et leurs commandes (`donnees/commandes/role-1.json` … `role-6.json`), les topologies des états A et B (`donnees/topologie/`), un quiz par étape et le glossaire (`donnees/glossaire.json`) ;
3. crée les rôles `Admin` et `Membre` et le compte admin.

Les démarrages suivants ne rechargent rien : **la base devient la source de vérité**.

Adresses utiles (port affiché par Visual Studio, par défaut `https://localhost:7103`) :

| Page | Adresse |
|---|---|
| Accueil | `/` |
| Un rôle (ex. rôle 2, étape 4, 3e commande) | `/Roles/2/Etapes/4?commande=3` |
| « Et si ? » (simuler un paquet) | `/Simulation` |
| Démonstration pour le jury | `/Simulation/Demonstration` |
| Questions et réponses | `/Discussions` |
| Glossaire | `/Glossaire` |
| Politique comme donnée (matrice de flux, tests) | `/Politique?etat=A` ou `?etat=B` |
| Plans de la vidéo (P-1 à P-8) | `/Video` |
| Administration (rôle 1) | `/Admin` |
| État de l'application et de la base | `/api/sante` |
| Documentation de l'API (développement) | `/swagger` |

## 3 bis. Utiliser l'application

- **Lecteur** : ▶ (ou la barre d'espace) rejoue les commandes de l'étape ; ← et → passent d'une commande à l'autre ; la vitesse va de ×0,5 à ×4. Chaque paquet suit le chemin calculé par le moteur réseau, et le panneau de droite dit quelle règle décide ; « Trame octet par octet » montre l'en-tête de chaque couche.
- **État A / État B** (en haut à droite) : le schéma, les verdicts et les règles changent ; le choix est gardé pendant la visite.
- **Comptes** : connecte-toi en admin (`admin@p6.lab`), puis **Admin › Comptes de l'équipe** pour créer le compte de chaque membre avec son numéro de rôle. Un membre peut modifier les étapes de son rôle (bouton « Modifier l'étape », « + Commande », effets en JSON, preuves), voir l'historique et revenir en arrière, passer les quiz, cocher « J'ai compris » et poser des questions.
- **Quiz** : générés depuis le guide, au statut « à valider » ; le responsable du rôle les relit (lien « relire » sur chaque question). Seuil de réussite : `Amorcage:SeuilQuiz` dans `appsettings.json` (0,8 par défaut).

## 3 ter. Tourner les plans de la vidéo

Huit plans de la vidéo d'installation et de tests, **P-1 à P-8**, se filment dans P6 Explorer. Chacun est une scène prête à filmer : on l'ouvre, on appuie sur **Espace**, elle se joue seule pendant sa durée, puis la dernière image reste figée. La liste et le statut de chaque scène sont sur **`/Video`**.

| Plan | Adresse | Durée | Contenu |
|---|---|---|---|
| P-1 | `/Video/P-1` | 25 s | Ouverture : un seul pare-feu ; kali → nginx (FA1), dmz-pivot → base (FA3), client-03 → base |
| P-2 | `/Video/P-2` | 30 s | L'architecture : 4 VM, 4 réseaux, puis états A et B côte à côte |
| P-3 | `/Video/P-3` | 15 s | Trunk (VLAN 10 et 20), services de la DMZ et du LAN |
| P-4 | `/Video/P-4` | 20 s | Matrice de flux A (FA3) puis B (F04), lue dans `donnees/politique/` |
| P-5 | `/Video/P-5` | 15 s | S-05 en état A : client-03 atteint la base sans traverser le pare-feu |
| P-6 | `/Video/P-6` | 25 s | Passage aux six segments de l'état B ; S-05 bloqué par F99 |
| P-7 | `/Video/P-7` | 20 s | Résultats A contre B (`resultats/synthese/synthese.csv`) |
| P-8 | `/Video/P-8` | 15 s | Conclusion : A contre B, l'équipe, la phrase de fin |

**Touches** (aucune ne fait apparaître quoi que ce soit à l'image) : **Espace** lecture / pause · **R** recommencer la scène · **→** scène suivante · **←** précédente. À l'ouverture, la scène reste immobile au moins 2 s, puis attend Espace. Le curseur disparaît après 2 s sans bouger la souris.

**Prise finale** : ajouter `?propre=1` (par exemple `/Video/P-5?propre=1`) retire le petit « P-5 » du coin en haut à gauche ; il est conservé en passant d'une scène à l'autre avec → et ←.

**Statuts** sur `/Video` : *prête* (données réelles), *illustration* (P-7 et P-8 tant que `synthese.csv` manque : valeurs d'exemple et bandeau rouge « ILLUSTRATION — données fictives » à l'image), *cible* (bandeau « Ce qui suit est la cible »).

**Quand l'état B réel sera filmé** : dans `donnees/video/scenes.json`, passer `"cible": true` à `"cible": false` pour P-6. Le fichier est relu à chaque ouverture de page : pas besoin de recompiler ni de redémarrer. Une erreur dans ce fichier s'affiche en clair sur `/Video`.

**Résultats du rôle 6** : déposer `resultats/synthese/synthese.csv` (format : `resultats/synthese/FORMAT.md`, une ligne A et une ligne B par indicateur). Dès que le fichier est valide, P-7 montre les vraies valeurs (sans bandeau) et P-8 ajoute une ligne par indicateur ; sinon la raison est affichée sur `/Video`.

**Réglages d'enregistrement** : OBS Studio, source « Capture de fenêtre » sur le navigateur ; navigateur en plein écran (**F11**), zoom **100 %** (Ctrl+0) ; sortie **1920 × 1080**, **30 i/s**. La scène est dessinée en 1920 × 1080 et mise à l'échelle de la fenêtre (bandes noires si le rapport diffère). Thème sombre imposé.

**Modifier une scène** (`donnees/video/scenes.json`) : chaque scène a `id`, `titre`, `sequence`, `duree_s`, `etat` (de départ), `cible` et une liste d'`etapes` jouées dans l'ordre. Types : `pause` (`duree_s` ou `jusqu_a_s`), `legende`, `titre`, `cadrer` (`noeuds`, `zones` facultatif), `allumer` (`noeuds`, `texte` = bulle), `surligner_zone` (`zones` ; `"segment": true` écrit « NOM · VLAN · réseau » lu dans `segments.csv`), `simulation` (`source`, `destination`, `protocole`, `port`, `etat`, `vitesse` facultative de 0,25 à 4), `bascule_etat`, `cote_a_cote` (`gauche`, `droite`), `matrice` (`etat`, `ligne_mise_en_avant`), `resultats`, `resume`. Toute étape accepte `duree_s` et `legende`. Dans une légende, `{lignes_journal}`, `{regle}` et `{verdict}` sont remplacés par le résultat du moteur de la dernière simulation (jamais écrits en dur). La somme des durées doit tenir à ±2 s de `duree_s`.

**Vérifier une durée** : ouvrir `/Video/P-1?lecture=auto` (démarre seule après l'image immobile) ; la console du navigateur (F12) affiche la durée de chaque étape et « Scène P-1 terminée en 25.0 s ». Ne pas utiliser ce paramètre pour le tournage.

## 4. En ligne de commande (sans Visual Studio)

À la racine du dépôt :

```powershell
dotnet build app/P6Explorer.sln          # doit finir par « 0 Avertissement(s), 0 Erreur(s) »
dotnet test app/P6Explorer.sln           # tous les tests doivent passer
dotnet run --project app/P6Explorer.Web  # puis ouvrir l'adresse affichée
```

Créer la base sans lancer le site : `dotnet ef database update --project app/P6Explorer.Infrastructure --startup-project app/P6Explorer.Web`.

Repartir d'une base vide (les modifications faites dans l'application seront perdues) :

```powershell
dotnet ef database drop --project app/P6Explorer.Infrastructure --startup-project app/P6Explorer.Web
```

## 5. Réimporter le guide (rarement)

Les fichiers `donnees/commandes/role-N.json` sont produits à partir du guide. À ne refaire que si le guide change **et** que la base est recréée (sinon les modifications de l'équipe, en base, priment) :

```powershell
node outils/guide-vers-json.mjs                       # sources/guide/data/*.js → donnees/guide/role-N.json
node outils/schemas-vers-svg.mjs                      # schémas du guide → donnees/schemas/schemas.json
dotnet run --project app/P6Explorer.Importeur         # → donnees/commandes/role-N.json (commandes, sous-commandes, quiz) et docs/a-completer.md
```

L'importeur déplie aussi les scripts longs en sous-commandes : depuis les vrais scripts de `plateforme/labo/` quand ils existent, sinon depuis le guide ; `test_flux.sh` depuis `donnees/politique/tests-A.csv` et `tests-B.csv` (produits par `c2.py`, voir `donnees/politique/LISEZ-MOI.md`).

Les commandes réellement utilisées pour les étapes déjà faites sont décrites dans `donnees/corrections/etapes-faites.json`.

## 6. Organisation de la solution

| Projet | Rôle |
|---|---|
| `P6Explorer.Domain` | Entités, effets, types valeur, interfaces des repositories, **moteur réseau** (`MoteurRoutage`, `MoteurNat`, `MoteurParefeu`, `SimulateurPaquet`) et `DecodeurPaquet`. Aucune dépendance externe. |
| `P6Explorer.Application` | Services, DTO, ViewModels, profil AutoMapper, rendu sûr des textes (`TexteRiche`). |
| `P6Explorer.Infrastructure` | `P6DbContext` (EF Core 8, Identity), configurations Fluent, migrations, repositories, intercepteur (dates et historique), annulation, amorçage. |
| `P6Explorer.Web` | Contrôleurs MVC et Web API, vues Razor et partielles, `lecteur.js`, Identity, politique `ModifierSonRole`, middleware, filtre, `Program.cs`. |
| `P6Explorer.Importeur` | Console : guide → `donnees/commandes/role-N.json` (commandes, effets, sous-commandes, quiz). |
| `P6Explorer.Tests` | xUnit : moteur (12/12, 19/19, tests.csv A et B), décodeur, données, amorçage, édition et annulation, quiz, pages et API. |

La correspondance avec les notions du cours est dans `docs/notions-du-cours.md`.

## 7. En cas de problème

| Symptôme | Cause probable | Solution |
|---|---|---|
| « A network-related or instance-specific error… » au démarrage | LocalDB absent ou arrêté | `sqllocaldb info` ; sinon installer la charge « Développement web et ASP.NET » ; `sqllocaldb start MSSQLLocalDB` |
| « dossier donnees/commandes introuvable » | Application lancée hors du dépôt | Lancer depuis le dépôt cloné, ou régler `Amorcage:DossierDonnees` dans `appsettings.json` |
| Pas de compte admin | Mot de passe absent des user-secrets, ou trop faible | Étape 2, puis relancer (le compte est créé au démarrage suivant) |
| Avertissement du navigateur sur le certificat | Certificat de développement non approuvé | `dotnet dev-certs https --trust` |
