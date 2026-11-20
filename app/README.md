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
2. la remplit avec les 47 étapes (`donnees/commandes/role-1.json` … `role-6.json`) et la topologie de l'état A (`donnees/topologie/etat-A.json`) ;
3. crée les rôles `Admin` et `Membre` et le compte admin.

Les démarrages suivants ne rechargent rien : **la base devient la source de vérité**.

Adresses utiles (port affiché par Visual Studio, par défaut `https://localhost:7103`) :

| Page | Adresse |
|---|---|
| Accueil | `/` |
| État de l'application et de la base | `/api/sante` |
| Documentation de l'API (développement) | `/swagger` |

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
dotnet run --project app/P6Explorer.Importeur         # → donnees/commandes/role-N.json et docs/a-completer.md
```

Les commandes réellement utilisées pour les étapes déjà faites sont décrites dans `donnees/corrections/etapes-faites.json`.

## 6. Organisation de la solution

| Projet | Rôle |
|---|---|
| `P6Explorer.Domain` | Entités, effets, types valeur (`NoeudId`, `AdresseIp`), interfaces des repositories. Aucune dépendance externe. |
| `P6Explorer.Application` | Services, ViewModels, profil AutoMapper. |
| `P6Explorer.Infrastructure` | `P6DbContext` (EF Core 8, Identity), configurations Fluent, migrations, repositories, intercepteur, amorçage. |
| `P6Explorer.Web` | Contrôleurs MVC, vues Razor, Tag Helper `<zone>`, Identity, `Program.cs` (injection de dépendances). |
| `P6Explorer.Importeur` | Console : guide → `donnees/commandes/role-N.json`. |
| `P6Explorer.Tests` | xUnit : données importées, topologie, amorçage (SQLite en mémoire), page d'accueil. |

## 7. En cas de problème

| Symptôme | Cause probable | Solution |
|---|---|---|
| « A network-related or instance-specific error… » au démarrage | LocalDB absent ou arrêté | `sqllocaldb info` ; sinon installer la charge « Développement web et ASP.NET » ; `sqllocaldb start MSSQLLocalDB` |
| « dossier donnees/commandes introuvable » | Application lancée hors du dépôt | Lancer depuis le dépôt cloné, ou régler `Amorcage:DossierDonnees` dans `appsettings.json` |
| Pas de compte admin | Mot de passe absent des user-secrets, ou trop faible | Étape 2, puis relancer (le compte est créé au démarrage suivant) |
| Avertissement du navigateur sur le certificat | Certificat de développement non approuvé | `dotnet dev-certs https --trust` |
