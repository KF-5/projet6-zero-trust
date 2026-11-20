# Choix techniques — P6 Explorer

Chaque choix qui sort du cours « Frameworks de développement » (GL3, INSAT), ou qui écarte une de ses notions, est justifié ici. Tenu à jour à chaque jalon.

## 1. Architecture

**Clean Architecture en 6 projets** (Domain, Application, Infrastructure, Web, Importeur, Tests). Le domaine ne dépend de rien ; l'application dépend du domaine ; l'infrastructure (EF Core, Identity) de l'application ; le Web assemble tout dans `Program.cs`. On peut ainsi tester le moteur réseau (jalon 3) sans base, et les services sans serveur web.

**Microservices : non retenus.** Une seule application, une seule équipe de six, déployée sur un seul poste pour la soutenance. Des microservices ajouteraient le réseau, la découverte de services et plusieurs bases sans aucun besoin réel. Un monolithe bien découpé en couches suffit et reste lisible.

**Database First : non retenu.** Il n'existe aucune base à reprendre : le modèle naît du code (Code First), et les migrations EF Core créent la base depuis zéro (critère d'acceptation).

## 2. Données

**EF Core 8 Code First, SQL Server LocalDB**, configuration entièrement en API Fluent (une classe `IEntityTypeConfiguration<T>` par entité). Les annotations de données ne servent qu'à la validation des ViewModels (jalon 4).

**Effets en héritage TPH** (une table `Effets`, discriminateur `Type`). Il y a 14 types d'effets, presque tous petits : une table par type (TPT) multiplierait les jointures à la lecture d'une commande, alors que le lecteur lit toujours tous les effets d'une étape d'un coup. Les colonnes de même nom et de même type sont partagées entre types frères (`Noeud`, `De`, `Vers`…), pour limiter la largeur de la table.

**Lazy loading non activé.** Avec le chargement paresseux, chaque accès à une navigation (`etape.Commandes`, puis `commande.Effets`) lancerait une requête : pour une étape de 15 commandes, 1 + 15 + … requêtes (problème N+1). On charge donc explicitement : `Include` / `ThenInclude` pour une étape avec ses commandes et effets, `Entry().Collection().LoadAsync()` pour l'historique des révisions.

**Types valeur du domaine** (`NoeudId`, `LienId`) convertis en texte par des `ValueConverter` : le domaine ne manipule jamais une machine sous forme de simple chaîne. Les listes (chemin d'un paquet, points d'un câble) sont stockées en JSON dans une colonne : elles ne sont jamais interrogées en SQL.

**Amorçage depuis des fichiers JSON, une seule fois.** Les JSON (`donnees/`) sont versionnés, relisibles et régénérables par l'importeur ; une fois la base remplie, ils ne sont plus relus (la base est la source de vérité, prompt section 7).

## 3. Bibliothèques

| Bibliothèque | Version | Pourquoi |
|---|---|---|
| EF Core (SqlServer, Sqlite, Design, Tools) | 8.0.11 | ORM du cours ; SQLite seulement dans les tests |
| ASP.NET Core Identity (EF Core) | 8.0.11 | comptes, rôles, verrouillage (cours) |
| AutoMapper | **13.0.1** | voir ci-dessous |
| Swashbuckle.AspNetCore | 6.9.0 | documentation Swagger de la Web API (cours) |
| xUnit, Microsoft.AspNetCore.Mvc.Testing | 2.9.2, 8.0.11 | tests unitaires et d'intégration |

**AutoMapper 13.0.1 et ses avis de sécurité.** C'est la dernière version sous licence MIT (les versions 15 et suivantes demandent une licence commerciale) ; elle a été validée le 20 novembre. NuGet la signale avec trois avis de gravité élevée (GHSA-rvv3-g6hj-g44x, GHSA-gpwp-5797-mjff, GHSA-6vhm-j9h7-f9gh, publiés en mars et novembre 2026) : tous trois décrivent un **déni de service** (débordement de pile) quand AutoMapper copie un graphe d'objets **très profondément imbriqué fourni par un attaquant**. Dans P6 Explorer, AutoMapper ne copie que nos propres entités, lues dans notre base, vers des ViewModels de profondeur fixe (deux ou trois niveaux) ; aucune donnée envoyée par un visiteur n'est mappée. Le risque ne s'applique donc pas ; les trois avis sont neutralisés **un par un** dans `app/Directory.Build.props` (`NuGetAuditSuppress`), pas globalement. Règle à respecter : **ne jamais mapper avec AutoMapper un objet reçu d'une requête** (les formulaires passent par des ViewModels lus explicitement). Si la licence commerciale devenait acceptable, passer à 15.1.4 ou plus et retirer les trois lignes.

**Pas de Bootstrap, pas de jQuery** : la feuille de style unique `wwwroot/css/site.css` suit `docs/style-reference.md` ; Bootstrap aurait imposé son propre style, contraire à celui de la référence.

**Polices IBM Plex Mono et IBM Plex Sans** (licence OFL, texte dans `wwwroot/fonts/LICENCE-OFL.txt`), **hébergées dans l'application** : la plateforme doit fonctionner hors ligne pendant la soutenance.

**JavaScript.** Exception prévue par le prompt : un seul module, `wwwroot/js/lecteur.js` (jalon 4), pour l'animation. En plus, le Layout contient une dizaine de lignes de script pour la bascule de thème (mémorisée dans un cookie, lu par le serveur pour éviter un éclair de la mauvaise couleur). Les paquets décoratifs de l'accueil sont animés en SVG pur (`<animateMotion>`), sans script.

**Node.js** : seulement pour l'outil `outils/guide-vers-json.mjs`, qui convertit les fichiers JavaScript du guide en JSON (le guide est du code JavaScript, que .NET ne sait pas exécuter). L'application ne dépend pas de Node.

## 4. Tests

**SQLite en mémoire plutôt qu'EF Core InMemory** pour les tests de l'infrastructure et du site : SQLite est une vraie base relationnelle (clés étrangères, index uniques, cascades), là où InMemory accepterait des données invalides. La base de test est créée par `EnsureCreated` (les migrations, écrites pour SQL Server, ne s'appliquent pas à SQLite).

**Ce que les tests ne couvrent pas encore** : la création de la base SQL Server par les migrations. Elle a été vérifiée en générant le script SQL complet (`dotnet ef migrations script`, 34 tables), mais pas exécutée tant que LocalDB n'est pas installé sur le poste de développement.
