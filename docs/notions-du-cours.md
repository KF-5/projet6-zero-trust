# Notions du cours « Frameworks de développement » dans P6 Explorer

Pour la soutenance du module .NET (GL3, INSAT) : chaque notion du cours, l'endroit précis où elle est utilisée, et pourquoi elle sert à cet endroit. Chemins relatifs au dossier `app/`. Les numéros de ligne sont ceux du commit du jalon 7 (20 novembre 2026).

## ASP.NET Core MVC

| Notion | Où | Pourquoi ici |
|---|---|---|
| Contrôleurs MVC | `P6Explorer.Web/Controllers/HomeController.cs:11`, `RolesController.cs`, `EtapesController.cs`, `SuiviControllers.cs` (Quiz, Discussions, Simulation, Glossaire, Admin, Compte) | une page par besoin de l'équipe ; chaque contrôleur appelle un service, jamais la base directement |
| Routage par attributs, URL partageable | `RolesController.cs` (`[Route("Roles")]`, `{numero}/Etapes/{etape}`) | `/Roles/2/Etapes/4?commande=3` désigne une commande précise, à coller dans un message |
| Vues Razor fortement typées | `Views/Roles/Etape.cshtml` (`@model EtapeVM`) et toutes les vues | le compilateur vérifie chaque propriété affichée |
| `_Layout.cshtml`, `_ViewStart.cshtml` | `Views/Shared/_Layout.cshtml`, `Views/_ViewStart.cshtml:2` | en-tête, navigation, bascule A/B, thème et message de confirmation communs à toutes les pages |
| Vues partielles | `_Schema`, `_Terminal`, `_PanneauExplication`, `_DecodeurOctets`, `_ListeEtapes`, `_Controles`, `_SchemaGuide`, `_Pagination`, `_ErreursModele` (`Views/Shared/`) ; appel : `Views/Roles/Etape.cshtml:65` | le schéma SVG et le panneau d'explication servent sur trois pages (étape, « Et si ? », démonstration) |
| ViewModels | `P6Explorer.Application/ViewModels/*.cs` | la vue ne voit jamais une entité EF : seulement ce qu'elle affiche |
| `ModelState`, `[ValidateAntiForgeryToken]` | `EtapesController.cs:43` (et chaque `[HttpPost]`) | formulaires d'édition : validation côté serveur et protection CSRF |
| `RenderSection` | `Views/Shared/_Layout.cshtml:111` ; `@section Scripts` dans `Views/Roles/Etape.cshtml:160` | `lecteur.js` n'est chargé que sur les pages d'étape, de simulation et de démonstration |
| `TempData` | `Views/Shared/_Layout.cshtml:75` ; rempli après chaque modification (`EtapesController.cs`) | « Étape enregistrée » affiché une seule fois, après la redirection |
| `ViewData` / `ViewBag` | `ViewData["Title"]` dans chaque action ; `ViewBag.Erreurs` lu par `Views/Shared/_ErreursModele.cshtml:2` | titre de l'onglet ; liste des erreurs du formulaire, comme au TP3 |
| Tag Helpers | `asp-for`, `asp-action`, `asp-validation-for` (`Views/Etapes/Modifier.cshtml:8`) | formulaires liés au ViewModel, sans écrire les noms de champs à la main |
| Tag Helper personnalisé | `P6Explorer.Web/TagHelpers/ZoneTagHelper.cs:9` (`<zone nom="DMZ" />`) | pastille de couleur et explication d'une zone, réutilisée dans les légendes |
| Filtre d'action personnalisé | `P6Explorer.Web/Filtres/ValiderModeleAttribute.cs:11` | renvoie la vue avec les erreurs si le formulaire est invalide, sans répéter `if (!ModelState.IsValid)` |
| Middleware personnalisé | `P6Explorer.Web/Middleware/JournalisationRequetes.cs:6` ; branché dans `Program.cs` | une ligne de journal par requête (méthode, chemin, utilisateur, statut, durée) |
| Session | `Program.cs:56` ; `P6Explorer.Web/Models/Session.cs` | l'état A/B choisi et la vitesse du lecteur suivent le visiteur de page en page |
| `IFormFile` | `P6Explorer.Web/Models/PreuveFormVM.cs:14` ; `EtapesController.Preuve` | captures d'écran de preuve : type (PNG/JPEG, signature vérifiée), taille (5 Mo), nom généré dans `wwwroot/preuves/` |
| Annotations de données | `P6Explorer.Application/ViewModels/EtapeViewModels.cs:170` (`[Range]`), `[Required]`, `[StringLength]`, `[Display]`, `[Compare]` | validation des formulaires ; les contraintes de la base restent dans l'API Fluent |

## Web API

| Notion | Où | Pourquoi ici |
|---|---|---|
| `[ApiController]`, DTO | `P6Explorer.Web/Controllers/Api/EtapesApiController.cs:15` ; `P6Explorer.Application/Dto/LecteurDtos.cs` | `lecteur.js` reçoit en JSON les commandes, leurs effets et le calcul du moteur pour chaque paquet |
| Codes HTTP 200 / 204 / 400 / 404 | `EtapesApiController.cs` (`NotFound()`, `Ok()`, `BadRequest()`, `NoContent()`) | une étape inconnue rend 404 ; une simulation sans destination, 400 |
| AutoMapper (`Profile`, `CreateMap`) | `P6Explorer.Application/Mappings/ProfilP6.cs:11` | entités → ViewModels et DTO ; les champs calculés sont ignorés explicitement (`Ignore()`), la configuration est validée par un test |
| Minimal API | `P6Explorer.Web/Program.cs:102` (`/api/sante`) | vérifier la base avant une démonstration |
| Swagger | `P6Explorer.Web/Program.cs:65` | documentation de la Web API, en développement |

## EF Core 8 Code First

| Notion | Où | Pourquoi ici |
|---|---|---|
| `DbContext`, `DbSet` | `P6Explorer.Infrastructure/Persistance/P6DbContext.cs:15` | une seule base pour le contenu, la topologie, les données de l'équipe et Identity |
| API Fluent, une classe par entité | `P6DbContext.cs:57` (`ApplyConfigurationsFromAssembly`) ; dossier `Persistance/Configurations/` | clés, longueurs, index, cascades : tout est au même endroit, hors des classes du domaine |
| Relations 1-n | Rôle → Étape → Commande → Effet (`Configurations/ContenuConfigurations.cs`) | le cœur du contenu |
| Relation 1-1 | Étape ↔ Quiz, `ContenuConfigurations.cs:52` (`HasOne().WithOne().HasForeignKey<Quiz>()`) | un quiz par étape |
| Relation n-n | Étape ↔ Terme du glossaire, `ContenuConfigurations.cs:49` ; Tentative ↔ Choix par la table `Reponses` (`SuiviConfigurations.cs`) | un terme sert dans plusieurs étapes ; une tentative coche plusieurs choix |
| Héritage TPH | `Configurations/EffetsConfigurations.cs:19` (`HasDiscriminator`) ; JSON polymorphe : `P6Explorer.Domain/Commandes/Effets.cs:13` | 14 types d'effets dans une table, lus d'un coup pour une étape |
| Type owned | `Configurations/TopologieConfigurations.cs:59` (`AdresseIp` dans `Interface`) ; `TopologieConfigurations.cs:38` (`Services` en JSON, `ToJson`) | une adresse n'existe pas sans sa carte ; les ports en écoute sont lus en bloc par le moteur |
| Propriétés shadow | `P6DbContext.cs:64` (`DateCreation`, `DateModification`) | dates de modification affichées, absentes du modèle du domaine |
| Intercepteur (`SaveChangesInterceptor`) | `Persistance/IntercepteurHorodatage.cs:18` | remplit les dates et écrit l'historique (qui, quand, champ, avant, après) de chaque modification |
| Retour à une version précédente | `Persistance/AnnulationRevisions.cs` | « revenir avant ce changement » sur la page Historique (modification, création, suppression) |
| Migrations | `Persistance/Migrations/` (`Initiale`, `MoteurReseau`) | la base se crée depuis zéro avec `dotnet ef database update` |
| Données d'amorçage | `P6Explorer.Infrastructure/Amorcage/ServiceAmorcage.cs` | 47 étapes, commandes, topologies A et B, quiz et glossaire chargés une seule fois depuis `donnees/` |
| Chargement eager (`Include` / `ThenInclude`) | `Repositories/Repositories.cs:81` | une étape avec ses commandes, sous-commandes et effets en une fois |
| Chargement explicite | `Repositories/Repositories.cs:260` (`Entry(fil).Collection(f => f.Messages).Query().OrderBy(…).LoadAsync()`) | les messages d'une question ne sont lus qu'une fois le fil trouvé, dans l'ordre |
| Lazy loading non activé | `P6Explorer.Infrastructure/DependencyInjection.cs:31` | éviter les requêtes N+1 (justification : `docs/choix-techniques.md`) |

## LINQ

| Notion | Où | Pourquoi ici |
|---|---|---|
| Syntaxe de requête, `join … on … equals`, `group by` | `Repositories/Repositories.cs:57` (commandes par rôle) ; `Repositories.cs:196` (meilleur score par membre et par étape) | compter et regrouper en une seule requête SQL |
| `Max`, `Count` | même requête (`g.Max(x => x.t.Score)`, `g.Count()`) | page Admin › Préparation de la soutenance |
| `OrderBy` / `ThenBy`, `Take` | `Repositories.cs:133`, `Repositories.cs:189` | ordre d'affichage stable ; 10 dernières tentatives |
| `Skip` / `Take` (pagination) | `Repositories.cs:40` | historique des révisions, questions-réponses, historique des simulations |
| LINQ sur objets dans le domaine | `P6Explorer.Domain/Moteur/Moteurs.cs:17` (`MoteurRoutage`), `MoteurParefeu`, `TopologieSimulee` | le moteur réseau interroge la topologie (cartes, règles) en LINQ |

## Architecture, services, injection de dépendances

| Notion | Où | Pourquoi ici |
|---|---|---|
| Clean Architecture (N-tiers) | 6 projets : `P6Explorer.Domain` (sans dépendance), `Application`, `Infrastructure`, `Web`, `Importeur`, `Tests` | le moteur réseau se teste sans base ; les services sans serveur web |
| Services et interfaces | `P6Explorer.Application/Services/*.cs` (`IEtapeService`, `IEditionService`, `IQuizService`, `ISimulationService`, `IProgressionService`, `IDiscussionService`, `IGlossaireService`) | toute la logique applicative hors des contrôleurs |
| `AddScoped<IService, Service>` | `P6Explorer.Application/DependencyInjection.cs:15` | une instance par requête |
| Repository générique | `Repositories/Repositories.cs:14` ; `AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>))` : `P6Explorer.Infrastructure/DependencyInjection.cs:34` | les entités simples (preuves, simulations) n'ont pas besoin d'un repository dédié |
| Repositories spécifiques | `IEtapeRepository`, `IQuizRepository`, `IDiscussionRepository`… (`P6Explorer.Domain/Interfaces/Repositories.cs`) | requêtes avec `Include` ou regroupements |
| async / await | partout dans les repositories, services et contrôleurs (`ToListAsync`, `SaveChangesAsync`…) | aucune requête bloquante |
| Patron MVC | contrôleurs + vues + ViewModels | séparation affichage / logique |

## Identity

| Notion | Où | Pourquoi ici |
|---|---|---|
| `IdentityDbContext<ApplicationUser>` | `P6DbContext.cs:15` ; `P6Explorer.Infrastructure/Identite/ApplicationUser.cs` (`NumeroRole`) | chaque membre a son numéro de rôle |
| `RoleManager` | `ServiceAmorcage.cs:171` | rôles `Admin` et `Membre` créés au démarrage |
| `UserManager` | `SuiviControllers.cs:235` (Admin › Comptes) | création des comptes des cinq membres |
| `SignInManager` | `SuiviControllers.cs:291` (connexion, verrouillage) | pages de connexion en français |
| Options d'Identity | `Program.cs:32` (mot de passe, verrouillage après 5 échecs) | comme au TP5 |
| Claims | `P6Explorer.Web/Securite/Securite.cs:36` (`FabriqueClaimsP6`, claim `NumeroRole`) | lue par la politique d'autorisation |
| Politique et `IAuthorizationHandler` | `Securite.cs:24` (`ModifierSonRole`) | un membre ne modifie que les étapes de son rôle ; l'admin modifie tout |
| `[Authorize]`, `[AllowAnonymous]`, `[Authorize(Roles = …)]` | `SuiviControllers.cs:214` (Admin) ; `RolesController` (public) ; `EtapesController` (membres) | consultation libre, suivi et édition réservés |
| User-secrets | `P6Explorer.Web.csproj` (`UserSecretsId`) ; `Amorcage:MotDePasseAdmin` | le mot de passe du compte admin n'est jamais dans le dépôt |

## Tests (xUnit)

| Notion | Où | Pourquoi ici |
|---|---|---|
| `[Theory]` / `[InlineData]` | `P6Explorer.Tests/MoteurReseau/TestsLaboratoireTests.cs:30` | les 12 tests de `tests.sh` et les 19 de `tests-cloisonnement.sh`, une ligne chacun |
| `[MemberData]` | `MoteurReseau/TestFluxTests.cs` | les 8 + 42 lignes de `tests.csv` produites par `c2.py` |
| Base de test | `P6Explorer.Tests/Outils/BaseDeTest.cs:18` (SQLite en mémoire) | relations et contraintes vérifiées, contrairement à InMemory |
| Tests d'intégration | `P6Explorer.Tests/Web/AccueilTests.cs:15` (`WebApplicationFactory`) | pages et API rendues par l'application complète |

## Deux notions écartées

**Database First** et **microservices** : voir `docs/choix-techniques.md`, section 1.
