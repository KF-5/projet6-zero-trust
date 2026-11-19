# Prompt pour Claude Code — « P6 Explorer », plateforme graphique du Projet 6 Zero Trust

> À coller tel quel dans Claude Code, lancé à la racine du dépôt `KF-5/projet6-zero-trust`, après avoir :
> 1. décompressé `sources-guide.zip` dans `sources/guide/` ;
> 2. copié `etapes-par-role-P6.md` dans `sources/`.

---

## 1. Ta mission

Construis **P6 Explorer**, une application **ASP.NET Core MVC (.NET 8)** qui rejoue graphiquement tout le Projet 6 (Architecture Zero Trust : pare-feu périmétrique → micro-segmentation + IDS/IPS).

Ce projet a **deux objectifs** : être la plateforme graphique du Projet 6, et servir de **projet .NET** pour le module « Frameworks de développement » (GL3, INSAT). Le code doit donc mettre en valeur, proprement et sans artifice, les notions de ce cours (voir la section 7).

Principe central : **pour chaque rôle (1 à 6), pour chaque étape, chaque commande exécutée est traduite en un schéma dynamique**. Quand l'utilisateur avance d'une commande, le schéma de l'architecture réagit : la machine concernée s'allume, les paquets partent et suivent le vrai chemin (VLAN, NAT, pare-feu), sont acceptés ou rejetés par la bonne règle, et un panneau décode ce qui se passe, octet par octet si c'est un paquet.

Public visé : des étudiants **débutants en réseau** (les 6 membres du groupe, le jury). Toute l'interface est **en français**, en tutoiement, avec des phrases courtes et aucun terme technique sans une explication au survol.

## 2. Le style à suivre : https://protocol-explorer-alpha.vercel.app/

Avant d'écrire du code, **étudie ce site** (ouvre-le avec un navigateur sans tête, fais des captures, lis son CSS calculé) et note dans `docs/style-reference.md` : palette, polices, mise en page, composants, animations, rythme des transitions.

Ce que l'on sait déjà :
- base sombre bleu nuit (`theme-color` = `#0b1020`), thème clair ET sombre (`color-scheme: light dark`) ;
- des **paquets animés qui voyagent** sur un schéma réseau (Internet → routeur → poste) ;
- chaque paquet est **décodé octet par octet** (vue hexadécimale + champs nommés) ;
- des **leçons interactives** pas à pas.

Reprends cette esthétique (sobre, technique, sombre par défaut, animations fluides et lisibles), **sans copier** leur code, leurs textes ni leur logo.

Couleurs de zone **imposées** (déjà utilisées dans le guide et le rapport, garde-les pour la cohérence) : WAN rouge, DMZ ambre, LAN vert, MGMT violet, trunk bleu, IDS/Suricata orange, OPNsense sarcelle. Les variables exactes (clair et sombre) et les couleurs des rôles `--r1` à `--r6` sont dans `sources/guide/style.css` : réutilise-les et adapte seulement le fond au bleu nuit du site de référence.

## 3. Tes sources (lis-les en entier avant de concevoir)

**Commence par `sources/etapes-par-role-P6.md`** : c'est la source principale, à lire en entier. Elle contient les 47 étapes des 6 rôles, déjà extraites du guide en Markdown lisible : but, pourquoi, prérequis, clics dans les interfaces, fichiers et commandes complets, tests avec sortie attendue, pannes connues, critères de fin, livrables, démonstrations. Les fichiers `sources/guide/` ci-dessous sont l'original : sers-t'en pour le script d'extraction, les schémas SVG et les couleurs, ou pour vérifier un doute.

| Source | Contenu |
|---|---|
| `sources/etapes-par-role-P6.md` | **Les 47 étapes détaillées des 6 rôles (source principale)** |
| `sources/guide/data/role-chef.js` | Rôle 1 : chef de projet et plateforme (9 étapes) |
| `sources/guide/data/role-parefeu.js` | Rôle 2 : pare-feu et segmentation, C1 et C2 (9 étapes) |
| `sources/guide/data/role-ids.js` | Rôle 3 : IDS/IPS Suricata, C3, C4, C8 (8 étapes) |
| `sources/guide/data/role-services.js` | Rôle 4 : services applicatifs C5 et capture C10 (8 étapes) |
| `sources/guide/data/role-attaques.js` | Rôle 5 : validation et attaques, C6 et C7 (6 étapes) |
| `sources/guide/data/role-analyse.js` | Rôle 6 : observabilité et analyse, C9, C11, NTP |
| `sources/guide/data/home.js`, `contrats.js`, `dependances.js`, `rapport.js` | Architecture, contrats d'interface entre rôles, dépendances, plan du rapport |
| `sources/guide/schemas-*.js` | Schémas SVG existants (géométrie de l'architecture dans `schemas-base.js`) : réutilise la géométrie |
| `sources/guide/style.css` | Couleurs et polices du guide |
| `ECARTS.md`, `docs/roles.md`, `docs/interfaces.md`, `conception/uml/` | Écarts E01–E42, rôles, interfaces, 13 diagrammes UML |
| Scripts du dépôt (s'ils y sont) | `docker/p6-etat-a/`, `collecteur/setup-collecteur.sh`, `kali/setup-kali.sh`, `kali/tests-cloisonnement.sh`, `commun/restreindre-ssh.sh`, `acces/*.ps1`, `panneau/panneau.py` |

Structure d'une étape dans le guide : `titre, quand, fait, but, pourquoi, prereq, faire, cmd, code, sortie, tester, fini, pannes, livrer, schema, maj`. **Les commandes réelles sont dans `cmd`, `code` et `faire`, les sorties attendues dans `sortie`.** Ne les invente pas : extrais-les.

**La plateforme réelle prime sur la conception d'origine.** Le guide et `ECARTS.md` (E31 à E42) disent ce qui a changé : VMware Workstation Pro au lieu de Proxmox, cartes `em0–em3` / `ens32–ens35` / `eth0–eth1`, compte `karim` + `sudo` au lieu de `root@`, journaux dans `/var/log/p6/<ip>.log`, NAT FA1/FA2 en « Pass », anti-lockout désactivée, etc. Le schéma doit montrer la réalité.

## 4. L'architecture à représenter (état A, référence actuelle)

- **Hôte** : PC Windows (VMware Workstation Pro), Windows = 10.99.0.2 sur MGMT, Tailscale 100.125.208.28 (machine `best-technology`), OpenSSH compte `p6labo` (tunnels seulement), panneau Flask 127.0.0.1:8080, consoles VNC 5901–5904.
- **Réseaux VMware** : `p6-wan` (203.0.113.0/24), `p6-trunk` (802.1Q : VLAN 10 LAN, VLAN 20 DMZ), VMnet9 = MGMT 10.99.0.0/24 (hôte uniquement), NAT temporaire débranché.
- **p6-opnsense** (`fw-p6.p6.lab`) : em0 WAN 203.0.113.1 · em1 trunk (vlan01 LAN 10.10.0.1, vlan02 DMZ 10.20.0.1) · em2 MGMT 10.99.0.1.
- **p6-docker** (10.99.0.20, macvlan `p6_dmz` / `p6_lan` / `p6_wan`) : DMZ nginx .10 (+ iperf3), dmz-pivot .20, bind9 .40, api .70 ; LAN client-01 .10, client-03 .12, postgres .70, admin .100 ; WAN client-ext 203.0.113.10.
- **p6-collecteur** 10.99.0.10 : NTP (chrony) + syslog UDP 514.
- **p6-kali** : 203.0.113.66 (WAN, passerelle 203.0.113.1) et 10.99.0.66 (MGMT, SSH filtré).
- **Règles état A** : NAT FA1 (80/443 → 10.20.0.10), FA2 (53 → 10.20.0.40), FA6 iperf3 désactivée ; filtrage FA3 (DMZ → postgres:5432), FA4 (LAN → tout), « Admin du pare-feu » (ADMINS → This Firewall 22/443), FA5 = refus par défaut journalisé. Filtre nftables `inet p6_ssh` sur les VM (SSH seulement depuis 10.99.0.2 et 10.99.0.10).
- **État B** (rôle 2, étape 8) : six segments et règles F01–F99 ; le schéma doit pouvoir basculer A ↔ B.
- Résultats de référence : `tests.sh` = 12/12, `tests-cloisonnement.sh` = 19/19.

## 5. Le cœur : traduire chaque commande en effet visuel

Conçois un **petit langage d'effets** déclaratif, indépendant du rendu. Chaque commande du guide devient un objet de données :

```csharp
// P6Explorer.Domain/Commandes
public record Commande(
    string Id,                  // "r1-e5-c03"
    int Role, int Etape,
    NoeudId Ou,                 // machine où on tape (hote, opnsense, docker, kali, collecteur, conteneur…)
    Shell Shell,                // PowerShell, Bash, OpnsenseGui, OpnsenseShell, VmwareGui
    string Texte,               // la commande exacte, tirée du guide
    string Explication,         // ce que fait la commande, en une ou deux phrases simples
    string? Sortie,             // sortie attendue (tirée du guide)
    IReadOnlyList<Effet> Effets // ce que le schéma montre
);

// Hiérarchie d'effets : classe abstraite + records dérivés (sérialisés en JSON polymorphe
// avec [JsonPolymorphic] / [JsonDerivedType], stockés par EF Core en héritage TPH)
public abstract record Effet;
public record EtatNoeud(NoeudId Noeud, EtatVm Etat) : Effet;              // creer, allumer, eteindre, suspendre
public record EtatLien(LienId Lien, bool Branche) : Effet;
public record AttribuerIp(NoeudId Noeud, string Carte, string Ip) : Effet;
public record Paquet(NoeudId De, NoeudId Vers, Protocole Proto, int? Port,
                     IReadOnlyList<NoeudId>? Chemin, Nat? Nat,
                     Verdict Verdict, string? Regle, int? Vlan) : Effet;    // Regle : "FA1", "FA5", "p6_ssh"…
public record ChangementRegle(ActionRegle Action, string IdRegle, string Resume) : Effet;
public record Service(NoeudId Noeud, string Nom, bool Demarre, int? Port) : Effet;
public record Fichier(NoeudId Noeud, string Chemin, bool Cree) : Effet;
public record Instantane(NoeudId Vm, string Nom) : Effet;
public record Journal(NoeudId De, string Ligne) : Effet;                    // vers le collecteur
public record AlerteIds(string Signature, bool Blocage) : Effet;
public record ResultatTest(string Nom, bool Ok, string? Compteur) : Effet;  // "12/12"
```

Règles de traduction :
1. **Une commande = au moins un effet visible.** Si une commande ne change rien sur le schéma (ex. `cat`), mets en surbrillance la machine et le fichier concernés.
2. Le **chemin des paquets est calculé** à partir de la topologie et du routage réel (passerelle de chaque machine, VLAN, NAT d'OPNsense), pas dessiné à la main. Écris dans `P6Explorer.Domain` un moteur (`MoteurRoutage`, `MoteurNat`, `MoteurParefeu`, interrogés avec **LINQ**) qui évalue les règles de l'état courant (A ou B) et donne le verdict ; vérifie qu'il retrouve les **12 résultats de `tests.sh` et les 19 de `tests-cloisonnement.sh`** (c'est ton test d'acceptation principal).
3. Les commandes graphiques (clics dans OPNsense ou VMware) sont des « commandes » aussi, avec `shell: "opnsense-gui"` et le chemin de menu en `texte`.
4. Les scripts longs (ex. `tests.sh`, `setup.sh`) se déplient en sous-commandes, chacune avec ses effets.
5. **Aucun secret** : pas de mot de passe, pas de clé privée, pas de contenu de `.env`, `utilisateurs.json`, `secret.key`. Remplace par `••••••`.

Génère ces données avec un **outil d'import** (`P6Explorer.Importeur`, application console .NET 8) qui lit `sources/guide/data/*.js` (mêmes données que `sources/etapes-par-role-P6.md`, mais structurées ; tu peux d'abord les convertir en JSON avec un court script Node) et produit `donnees/commandes/role-N.json` ; au démarrage de l'application, un service d'amorçage charge ces JSON dans la base par EF Core. Ensuite, **complète les `effets` à la main ou de façon semi-automatique** (motifs reconnus : `ping`, `curl`, `nc`, `dig`, `ssh`, `scp`, `docker compose up`, `nmap`, `hping3`, `iperf3`, `psql`, `ip addr add`, `systemctl`, `nft`, `pfctl`, `vmrun`…). Garde une liste des commandes sans effet défini dans `docs/a-completer.md`.

## 6. L'interface

- **Accueil** : la carte complète de l'architecture (état A), animée en arrière-plan ; six cartes de rôle (couleur `--r1` à `--r6`) avec leur avancement.
- **Vue rôle** : liste des étapes à gauche (titre, date, pastille « fait » / « à faire »), et pour l'étape choisie :
  - en haut, **le schéma dynamique** (SVG) ;
  - en bas à gauche, **un terminal rejoué** qui affiche la commande, la tape caractère par caractère, puis la sortie attendue ;
  - en bas à droite, **le panneau d'explication** : ce que fait la commande, pourquoi, la règle de pare-feu qui décide, et pour un paquet la **décomposition octet par octet** (Ethernet, 802.1Q, IP, TCP/UDP, et en-tête applicatif simple : DNS, HTTP, TLS ClientHello, NTP, syslog) avec la vue hexadécimale synchronisée au survol.
- **Contrôles** : ▶ lecture / ⏸ / ⏭ commande suivante / ⏮ / vitesse ×0,5 à ×4 / curseur de chronologie / touches ← → espace. URL partageable par commande (`/Roles/2/Etapes/4?commande=3`, routage MVC).
- **Bascule état A ↔ B** et **mode « Et si ? »** : l'utilisateur choisit une source, une destination et un port, et voit le paquet passer ou être bloqué, avec la règle responsable (super pour la démonstration devant le jury).
- **Calques** activables : zones (WAN/DMZ/LAN/MGMT), adresses IP, règles, journaux vers le collecteur, alertes Suricata.
- **Glossaire** au survol (VLAN, trunk, NAT, macvlan, nftables, Tailscale, tunnel, ProxyJump, VNC, IDS, IPS, netmap…) : une phrase et une comparaison concrète.
- **Thème** sombre par défaut, clair disponible, suit le système, choix mémorisé. **Accessibilité** : `prefers-reduced-motion` (animations remplacées par des étapes), contraste AA, navigation au clavier, textes alternatifs des schémas.
- Responsive : utilisable sur un portable de 13 pouces et lisible projeté en salle.

## 7. Technique : .NET 8 et les notions du cours

Le cours suivi (module « Frameworks de développement », GL3 INSAT) couvre : **ASP.NET Core MVC** (contrôleurs, vues Razor, `_Layout.cshtml`, `_ViewStart.cshtml`, vues partielles, ViewModels, `ModelState`, `[ValidateAntiForgeryToken]`), **EF Core 8 Code First** (DbContext, `DbSet`, migrations, propriétés de navigation, relations 1-n et n-n, **API Fluent** dans `OnModelCreating`, données d'amorçage, intercepteurs), **LINQ** (syntaxe de requête et de méthode, `join … on … equals`, `group by`, `OrderBy`/`ThenBy`, `Count`, `Take`), **architecture à base de services** et **injection de dépendances** (`AddScoped<IService, Service>`), **Repository générique** (`IGenericRepository<T>` enregistré par `AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>))`), **ASP.NET Core Identity** (`IdentityDbContext<ApplicationUser>`, `UserManager`, `SignInManager`, `RoleManager`, `[Authorize]`), **Web API** (contrôleurs `[ApiController]`, DTO, **AutoMapper** avec `Profile` et `CreateMap`, codes HTTP 200/201/404), et les **patrons architecturaux** (MVC, N-tiers, **Clean Architecture**). Outils : **Visual Studio 2022**, SQL Server **LocalDB**, CLI `dotnet`.

**Utilise ces notions, chacune là où elle a un vrai rôle**, et n'introduis pas de technologie hors cours sans le justifier dans `docs/choix-techniques.md` (exception autorisée : un module JavaScript pour l'animation SVG, voir plus bas).

### Solution `P6Explorer.sln` (Clean Architecture, dossier `app/`)

| Projet | Contenu | Notions du cours |
|---|---|---|
| `P6Explorer.Domain` | Entités (`Role`, `Etape`, `Commande`, `Effet` et dérivés, `Noeud`, `Interface`, `Reseau`, `Regle`, `EtatReseau` A/B, `TermeGlossaire`, `Progression`, `Revision`, `Quiz`, `Question`, `Choix`, `Tentative`, `Reponse`, `Fil`, `Message`, `Simulation`), énumérations, **moteur réseau pur** (`MoteurRoutage`, `MoteurNat`, `MoteurParefeu`, `DecodeurPaquet`), interfaces (`IGenericRepository<T>`, `ICommandeRepository`…), exceptions métier. Aucune dépendance externe. | entités, héritage, interfaces, LINQ, SOLID |
| `P6Explorer.Application` | Services (`IRoleService`, `IEtapeService`, `IEditionService`, `IQuizService`, `ISimulationService`, `IProgressionService`, `IGlossaireService`, `IDiscussionService`), **DTO** et **ViewModels**, profils **AutoMapper**. Référence Domain. | services, DTO, AutoMapper, LINQ |
| `P6Explorer.Infrastructure` | `P6DbContext : IdentityDbContext<ApplicationUser>`, configurations **API Fluent** (une classe `IEntityTypeConfiguration<T>` par entité : clés, relations Role 1-n Etape 1-n Commande 1-n Effet, héritage **TPH** des effets avec discriminateur, n-n `Etape`↔`TermeGlossaire`, index), **migrations**, `GenericRepository<T>` + repositories spécifiques (`Include`/`ThenInclude`), service d'amorçage depuis les JSON, un **intercepteur** EF Core qui horodate les modifications. Référence Application. | EF Core 8 Code First, Fluent API, migrations, Repository générique, intercepteurs |
| `P6Explorer.Web` | **ASP.NET Core MVC** : contrôleurs `Home`, `Roles`, `Etapes` (lecture et CRUD), `Quiz`, `Simulation` (« Et si ? »), `Glossaire`, `Admin`, `Discussions` ; vues Razor fortement typées, `_Layout.cshtml` (barre de navigation, bascule de thème), **vues partielles** (`_Schema`, `_Terminal`, `_PanneauExplication`, `_DecodeurOctets`, `_ListeEtapes`) ; **contrôleurs Web API** (`api/etapes/{id}/commandes`, `api/simulation`) qui renvoient des DTO JSON pour l'animation ; **Identity** ; `Program.cs` avec toute l'injection de dépendances. | MVC, Razor, Layout, partielles, ViewModels, Web API, Identity, DI |
| `P6Explorer.Importeur` | Application console : lit les sources du guide et écrit `donnees/commandes/role-N.json`. | CLI `dotnet`, `System.Text.Json` |
| `P6Explorer.Tests` | **xUnit** : moteur (les 12 tests de `tests.sh` et les 19 de `tests-cloisonnement.sh` en `[Theory]`/`[InlineData]`), décodeur de paquets, services (base **EF Core InMemory** ou SQLite en mémoire), contrôleurs. | tests unitaires |

### La base de données : des données vivantes, pas un décor

Le contenu du guide est importé une seule fois. **Ensuite, la base devient la source de vérité** : les JSON ne servent plus qu'à l'amorçage initial. La base se justifie par quatre besoins réels de l'équipe, qui créent et modifient des données. Les quatre sont **obligatoires**.

**1. Contenu éditable par son responsable ** Le guide évolue (il a déjà été corrigé les 17 et 19 novembre).
- CRUD complet sur les étapes, les commandes (texte, explication, sortie attendue, ordre) et leurs effets, avec formulaires Razor, ViewModels, annotations de validation, `ModelState`, `[ValidateAntiForgeryToken]`.
- Autorisation : un membre ne modifie que les étapes de **son** rôle (`ApplicationUser.NumeroRole == Etape.RoleNumero`), vérifiée dans un `IAuthorizationHandler` avec une politique `ModifierSonRole` ; l'admin modifie tout.
- **Historique des modifications** : entité `Revision` (qui, quand, champ, ancienne et nouvelle valeur), remplie par l'**intercepteur** EF Core (`SaveChangesInterceptor`) ; page « Historique » par étape, avec retour à une version précédente.
- Statut d'une étape (`Brouillon`, `AValider`, `Validee`) et date de dernière modification affichée sur la vue rôle.

**2. Auto-évaluation par étape ** Pour que chaque membre puisse vérifier qu'il sait expliquer sa partie avant la soutenance.
- Entités `Quiz` (1 par étape) → `Question` (1-n) → `Choix` (1-n, un ou plusieurs corrects) ; `Tentative` (utilisateur, quiz, date, score) → `Reponse` (n-n entre Tentative et Choix).
- Amorçage : les « Questions probables du jury » de `sources/etapes-par-role-P6.md` (5 par rôle) deviennent des questions ; complète avec 2 ou 3 questions à choix multiples par étape, tirées du but, du pourquoi et des pannes de l'étape, au statut `AValider` pour que le responsable du rôle les relise.
- Seuil de réussite configurable (80 % par défaut) ; l'étape apparaît « maîtrisée » dans la progression quand le quiz est réussi.
- Page **Admin › Préparation de la soutenance** : pour chaque membre et chaque étape, meilleur score et date, calculés par des requêtes **LINQ** (`group by`, `join`, `Max`, `Count`) ; liste des étapes que personne ne maîtrise encore.

**3. Questions et réponses sur une commande.** Un membre qui ne comprend pas une commande pose une question dessus ; le responsable du rôle répond ; la question est marquée résolue. Entités `Fil` (commande, auteur, statut) → `Message` (1-n). Compteur de questions ouvertes sur la vue rôle.

**4. Historique des simulations « Et si ? ».** Chaque simulation enregistrée (source, destination, protocole, port, état A ou B, verdict, règle) ; un membre peut en marquer une « pour la démo » ; la page démo les rejoue dans l'ordre choisi.

Le modèle de données doit être dessiné dans `docs/modele-donnees.md` (diagramme de classes Mermaid) avant la première migration.

### Les autres notions du cours, chacune à sa place

Utilise aussi les notions suivantes, aux endroits indiqués :

| Notion | Où et pourquoi |
|---|---|
| **`IFormFile`** (TP3) | Joindre des **captures d'écran de preuve** à une étape (Live View d'OPNsense, sortie 12/12, 19/19…) : entité `Preuve` (1-n avec Etape), fichier enregistré dans `wwwroot/preuves/` sous un nom généré, contrôle du type (PNG, JPEG) et de la taille (5 Mo maximum), ViewModel dédié comme `MovieVM` du TP3 |
| **`TempData`** | Message de confirmation après une modification (« Étape enregistrée »), affiché une fois par le Layout |
| **`ViewBag` / `ViewData`** | Titre des pages (`ViewData["Title"]`) ; liste des erreurs du `ModelState` comme dans le TP3 |
| **`RenderSection`** | Section `Scripts` du Layout : `lecteur.js` n'est chargé que sur les pages d'étape et de simulation |
| **Tag Helpers** | Tous les formulaires (`asp-for`, `asp-action`, `asp-validation-for`) ; un **Tag Helper personnalisé** `<zone nom="DMZ">` qui affiche la pastille de couleur d'une zone |
| **Relation 1-1** | Étape ↔ Quiz (un quiz par étape), configurée par l'API Fluent (`HasOne().WithOne().HasForeignKey<Quiz>()`) |
| **Chargement eager / explicite / lazy** | `Include`/`ThenInclude` pour une étape avec ses commandes et effets ; chargement **explicite** (`Entry().Collection().LoadAsync()`) pour l'historique des révisions ; lazy loading **non activé**, avec la raison écrite dans `docs/choix-techniques.md` (requêtes N+1) |
| **Propriété shadow** | `DateCreation` et `DateModification` en propriétés shadow, remplies par l'intercepteur |
| **Type owned** | `AdresseIp` (adresse + longueur de préfixe) en type owned dans `Interface` et `Reseau` |
| **Middleware personnalisé** | Journalise chaque requête (méthode, chemin, utilisateur, durée) |
| **Filtre d'action personnalisé** | `ValiderModeleAttribute` : renvoie la vue avec les erreurs si le `ModelState` est invalide, pour ne pas répéter ce test dans chaque action |
| **Session** | Mémorise l'état A/B choisi et la vitesse du lecteur pendant la visite |
| **Claims** | Claim `NumeroRole` ajoutée à la connexion (`IUserClaimsPrincipalFactory`), lue par la politique `ModifierSonRole` |
| **Options d'Identity** | Règles de mot de passe et verrouillage après 5 échecs (comme le TP5) ; pages Identity générées par **scaffolding** puis traduites en français et adaptées au style |
| **Minimal API** | Endpoint `/api/sante` (état de l'application et de la base) déclaré dans `Program.cs` |
| **Swagger** | Documentation de la Web API (Swashbuckle), accessible en développement |
| **async/await** | Partout dans les repositories, services et contrôleurs (`ToListAsync`, `SaveChangesAsync`…) |
| **Pagination** | Historique des révisions, questions-réponses et historique des simulations (`Skip`/`Take`) |
| **Annotations de données** | Validation des ViewModels (`[Required]`, `[StringLength]`, `[Range]`, `[Display]`) ; les contraintes de la base restent dans l'API Fluent |

Deux notions du cours ne s'appliquent pas ici ; explique-le dans `docs/choix-techniques.md` : **Database First** (la base est créée à partir du code) et les **microservices** (une seule application, une seule équipe, déployée sur un seul poste : un monolithe en Clean Architecture est le bon choix).

### Identity : utile, pas décoratif
- Rôles Identity `Admin` (rôle 1) et `Membre` (rôles 2 à 6), créés au démarrage par `RoleManager` ; compte admin d'amorçage dont le mot de passe vient des **user-secrets**, jamais du code.
- `ApplicationUser : IdentityUser` avec une propriété `NumeroRole` (1 à 6).
- Consultation publique (`[AllowAnonymous]`) ; **suivi de progression** réservé aux membres connectés (`[Authorize]`) : cocher les commandes comprises, passer les quiz, voir son avancement sur l'accueil ; **modification du contenu** selon la politique `ModifierSonRole` ; pages `Admin` (`[Authorize(Roles = "Admin")]`) : gestion des comptes (création des 5 membres avec leur numéro de rôle par `UserManager`), avancement et préparation de la soutenance de toute l'équipe.

### Le rendu graphique
- Le schéma est un **SVG** produit côté serveur par la vue partielle `_Schema.cshtml` à partir du ViewModel de topologie (nœuds, liens, zones, adresses).
- L'animation est jouée par **un seul module JavaScript** (`wwwroot/js/lecteur.js`, sans framework, Web Animations API) : il appelle l'API `api/etapes/{id}/commandes`, reçoit les effets en JSON et les anime (paquets, verdicts, terminal tapé, mise en surbrillance). **Toute la logique (chemins, NAT, verdicts, décodage) reste en C#** ; le JavaScript ne fait qu'animer.
- Pas de Bootstrap par défaut si cela nuit au style de protocol-explorer : CSS propre dans `wwwroot/css/site.css`, avec les variables de couleur de `sources/guide/style.css`.

### Base, exécution, livraison
- SQL Server **LocalDB** en développement (chaîne `DefaultConnection` dans `appsettings.json`) ; `dotnet ef database update` crée la base, l'amorçage la remplit.
- Lancement : ouvrir `app/P6Explorer.sln` dans Visual Studio 2022 → F5. Écris un `app/README.md` pas à pas pour un débutant (prérequis, migrations, compte admin, lancement).
- Branche `role-1-plateforme-graphique`, commits au format `[rôle 1] …`.
- Ajoute `docs/notions-du-cours.md` : un tableau « notion du cours → fichier et ligne où elle est utilisée → pourquoi ici », pour la soutenance du module .NET.

## 8. Ordre de travail (montre-moi le résultat à chaque jalon avant de continuer)

1. **Étude** : analyse du site de référence + lecture de toutes les sources → `docs/style-reference.md` et `docs/plan-plateforme.md` (liste des écrans, schéma de données, nombre de commandes par rôle). **Arrête-toi et attends ma validation.**
2. **Solution et base** : les 6 projets, le DbContext avec l'API Fluent, les migrations, l'import et l'amorçage des 47 étapes ; la page d'accueil MVC avec le Layout, le thème et le schéma SVG de l'état A.
3. **Moteur réseau** : routage, NAT, pare-feu, verdicts ; les 12 + 19 tests retrouvés par xUnit.
4. **Lecteur de commandes + rôle 1 complet**, puis **édition du contenu** (CRUD, autorisation par rôle, historique par l'intercepteur) (c'est le rôle le mieux documenté : il sert de modèle).
5. **Décodeur de paquets** octet par octet.
6. **Rôles 2 à 6**, un par un, avec l'état B pour le rôle 2.
7. **Identity, progression et quiz d'auto-évaluation** (avec la page de préparation de la soutenance), questions et réponses sur les commandes, mode « Et si ? » avec son historique et la sélection pour la démo, glossaire, accessibilité, finitions, `docs/notions-du-cours.md`.

## 9. Critères d'acceptation

- Les 47 étapes de `sources/etapes-par-role-P6.md` sont toutes présentes, et chaque commande de chaque étape l'est aussi, avec son texte exact, son explication et au moins un effet visible.
- Le moteur reproduit 12/12 (`tests.sh`) et 19/19 (`tests-cloisonnement.sh`) ; Kali ne joint jamais MGMT, ni la box, ni Internet, ni Windows.
- Les adresses, interfaces et noms affichés correspondent à la plateforme réelle (écarts E31–E42).
- Aucun secret dans le code ni dans les données.
- La solution compile sans avertissement dans Visual Studio 2022, `dotnet test` passe entièrement, et la base se crée par migrations depuis zéro.
- Un membre connecté peut modifier les étapes de son rôle et pas celles des autres ; chaque modification apparaît dans l'historique et peut être annulée.
- Chaque étape a un quiz ; un membre peut le passer, son score est enregistré, et l'admin voit qui maîtrise quoi.
- Un membre peut poser une question sur une commande, le responsable du rôle y répond et la marque résolue ; le nombre de questions ouvertes s'affiche sur la vue rôle.
- Chaque simulation « Et si ? » est enregistrée ; celles marquées « pour la démo » se rejouent dans l'ordre choisi.
- Chaque notion du cours listée en section 7, y compris le tableau « Les autres notions du cours », est utilisée au moins une fois, à un endroit où elle sert vraiment, et `docs/notions-du-cours.md` le montre.
- Animations fluides sur un portable ordinaire ; Lighthouse ≥ 90 en accessibilité.
- Un débutant comprend, sans aide, pourquoi `client-ext → postgres:5432` est bloqué en état A (FA5) et ce qui change en état B.

Si une information manque ou se contredit entre le guide, `ECARTS.md` et les scripts, **ne devine pas** : liste la question dans `docs/questions.md` et demande-moi.
