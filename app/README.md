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
| Administration (rôle 1) | `/Admin` |
| État de l'application et de la base | `/api/sante` |
| Documentation de l'API (développement) | `/swagger` |

## 3 bis. Utiliser l'application

- **Lecteur** : ▶ (ou la barre d'espace) rejoue les commandes de l'étape ; ← et → passent d'une commande à l'autre ; la vitesse va de ×0,5 à ×4. Chaque paquet suit le chemin calculé par le moteur réseau, et le panneau de droite dit quelle règle décide ; « Trame octet par octet » montre l'en-tête de chaque couche.
- **État A / État B** (en haut à droite) : le schéma, les verdicts et les règles changent ; le choix est gardé pendant la visite.
- **Comptes** : connecte-toi en admin (`admin@p6.lab`), puis **Admin › Comptes de l'équipe** pour créer le compte de chaque membre avec son numéro de rôle. Un membre peut modifier les étapes de son rôle (bouton « Modifier l'étape », « + Commande », effets en JSON, preuves), voir l'historique et revenir en arrière, passer les quiz, cocher « J'ai compris » et poser des questions.
- **Quiz** : générés depuis le guide, au statut « à valider » ; le responsable du rôle les relit (lien « relire » sur chaque question). Seuil de réussite : `Amorcage:SeuilQuiz` dans `appsettings.json` (0,8 par défaut).

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
