# Modèle de données — P6 Explorer

Écrit au jalon 2, **avant la première migration** (`app/P6Explorer.Infrastructure/Migrations/`). Base SQL Server LocalDB créée en Code First par EF Core 8 ; toute la configuration est en API Fluent (`app/P6Explorer.Infrastructure/Persistance/Configurations/`, une classe `IEntityTypeConfiguration<T>` par entité).

## 1. Diagramme de classes

```mermaid
classDiagram
  direction LR

  class Role {
    +int Numero «clé, 1 à 6»
    +string Cle
    +string Titre
    +string CourtNom
    +string Couleur
    +string Mission
    +string? Introduction
    +string? Demonstration
  }
  class Etape {
    +int Id
    +int RoleNumero
    +int Numero «0 à 9»
    +string Code «r1-e5»
    +string Titre
    +string? Quand
    +bool Fait
    +StatutValidation Statut
    +string But
    +string Pourquoi
    +string? Prerequis
    +string? Livrable
    +string? Schema
  }
  class Commande {
    +int Id
    +int EtapeId
    +int? ParentId
    +string Code «r1-e5-c03»
    +int Ordre
    +NoeudId Ou
    +Shell Shell
    +string? Compte
    +string Texte
    +string? TexteGuide
    +string Explication
    +string? Sortie
    +bool EstTest
  }
  class Effet {
    <<abstract>>
    +int Id
    +int CommandeId
    +int Ordre
    +bool Automatique
  }
  class Paquet {
    +NoeudId De
    +NoeudId Vers
    +Protocole Proto
    +int? Port
    +string? AdresseVisee
    +List~NoeudId~? Chemin «JSON»
    +Nat? Nat «owned»
    +Verdict Verdict
    +string? Regle
    +int? Vlan
  }
  class Panne { +int Ordre +string Symptome +string Cause +string Solution }
  class CritereFin { +int Ordre +string Texte }
  class Preuve { +string NomFichier +string NomOriginal +string TypeMime +long Taille +string? Legende +DateTime Date }
  class TermeGlossaire { +string Terme +string Definition +string? Comparaison }

  class EtatReseau { +string Code «A | B» +string Libelle +string? Description }
  class Noeud { +NoeudId Id +string Nom +TypeNoeud Type +Zone Zone +string? Passerelle +NoeudId? Hote +double X, Y, Largeur, Hauteur +List~ServiceReseau~ Services «JSON» }
  class Interface { +string Nom +string? Affectation +int? Vlan +AdresseIp? Adresse «owned» +bool Branchee +string? EtatReseauCode }
  class Reseau { +string Code +string Nom +Zone Zone +int? Vlan +AdresseIp? Prefixe «owned» +string? Passerelle +cadre X, Y, L, H }
  class Lien { +LienId Id +NoeudId De +NoeudId Vers +Zone Zone +bool Branche +List~double[]~ Points «JSON» }
  class Regle { +NoeudId Equipement +string Code +string Interface +int Ordre +ActionFiltre Action +string Proto, Source, Destination, Ports +bool Journal, Active }
  class RegleNat { +string Code +string Proto +string AdressePublique, PortPublic +string Cible, PortCible +string? SourceAutorisee +FiltrageNat Filtrage +bool Active }

  class ApplicationUser { +int? NumeroRole +string? NomAffiche }
  class Revision { +Guid Lot +string Entite +int EntiteId +int? EtapeId +string Champ +string? Avant +string? Apres +DateTime Date }
  class Quiz { +double Seuil = 0,8 }
  class Question { +int Ordre +string Texte +StatutValidation Statut +string? Source }
  class Choix { +int Ordre +string Texte +bool Correct +string? Explication }
  class Tentative { +DateTime Date +double Score }
  class Reponse { +int TentativeId «clé» +int ChoixId «clé» }
  class Progression { +bool Comprise +DateTime Date }
  class Fil { +string Titre +StatutFil Statut +DateTime Date }
  class Message { +string Texte +DateTime Date }
  class Simulation { +string Source, Destination +Protocole Protocole +int? Port +Verdict Verdict +string? Regle +bool PourDemo +int? OrdreDemo }

  Role "1" --> "0..*" Etape : Etapes
  Etape "1" --> "0..*" Commande : Commandes
  Commande "0..1" --> "0..*" Commande : SousCommandes
  Commande "1" --> "0..*" Effet : Effets
  Effet <|-- EtatNoeud
  Effet <|-- EtatLien
  Effet <|-- AttribuerIp
  Effet <|-- Paquet
  Effet <|-- ChangementRegle
  Effet <|-- Service
  Effet <|-- Fichier
  Effet <|-- Instantane
  Effet <|-- Journal
  Effet <|-- AlerteIds
  Effet <|-- ResultatTest
  Effet <|-- Surbrillance
  Effet <|-- ChangementEtat
  Effet <|-- ConteneurReseau
  Etape "1" --> "0..*" Panne
  Etape "1" --> "0..*" CritereFin
  Etape "1" --> "0..*" Preuve
  Etape "0..*" -- "0..*" TermeGlossaire : EtapeTerme
  Etape "1" -- "0..1" Quiz
  Quiz "1" --> "0..*" Question
  Question "1" --> "0..*" Choix
  Quiz "1" --> "0..*" Tentative
  Tentative "1" --> "0..*" Reponse
  Choix "1" --> "0..*" Reponse
  ApplicationUser "1" --> "0..*" Tentative
  ApplicationUser "1" --> "0..*" Progression
  Commande "1" --> "0..*" Progression
  Commande "1" --> "0..*" Fil
  Fil "1" --> "0..*" Message
  ApplicationUser "1" --> "0..*" Fil
  ApplicationUser "0..1" --> "0..*" Simulation
  ApplicationUser "0..1" --> "0..*" Revision
  EtatReseau "1" --> "0..*" Reseau
  EtatReseau "1" --> "0..*" Regle
  EtatReseau "1" --> "0..*" RegleNat
  EtatReseau "1" --> "0..*" Simulation
  Noeud "1" --> "0..*" Interface
  Reseau "0..1" --> "0..*" Interface
```

## 2. Tables et choix de modélisation

| Table | Entité | Clé | Points notables (API Fluent) |
|---|---|---|---|
| `Roles6` | `Role` | `Numero` (non générée) | « Roles » est déjà utilisé par Identity (`AspNetRoles`) ; index unique sur `Cle` |
| `Etapes` | `Etape` | `Id` | index uniques `Code` et (`RoleNumero`, `Numero`) ; `Statut` stocké en texte |
| `Commandes` | `Commande` | `Id` | index unique `Code` ; `Ou` converti `NoeudId` ↔ texte ; auto-référence `ParentId` sans cascade (SQL Server refuse les cascades sur une auto-référence) |
| `Effets` | `Effet` et 14 types dérivés | `Id` | **héritage TPH** : une table, discriminateur `Type` (mêmes valeurs que le JSON polymorphe) ; colonnes partagées entre types frères (`Noeud`, `De`, `Vers`, `Nom`, `Port`, `Ip`, `CheminFichier`) ; `Chemin` en JSON ; `Nat` en **type owned** (colonnes `Nat_…`) |
| `Pannes`, `Criteres` | `Panne`, `CritereFin` | `Id` | 1-n avec `Etape`, cascade |
| `Preuves` | `Preuve` | `Id` | `NomFichier` unique (nom généré) ; auteur → `AspNetUsers`, mis à null si le compte est supprimé |
| `Glossaire`, `EtapeTerme` | `TermeGlossaire` | `Id` | **n-n** `Etape` ↔ `TermeGlossaire` par une table de jointure implicite |
| `Quiz` | `Quiz` | `Id` | **1-1** avec `Etape` : `HasOne().WithOne().HasForeignKey<Quiz>()`, `EtapeId` unique |
| `Questions`, `Choix` | | `Id` | 1-n en cascade depuis `Quiz` |
| `Tentatives` | `Tentative` | `Id` | utilisateur → cascade ; index (`UtilisateurId`, `QuizId`) |
| `Reponses` | `Reponse` | (`TentativeId`, `ChoixId`) | **n-n** Tentative ↔ Choix par une entité de jointure explicite ; pas de cascade depuis `Choix` (deux chemins de cascade depuis `Quiz` sinon) |
| `Progressions` | `Progression` | `Id` | index unique (`UtilisateurId`, `CommandeId`) |
| `Fils`, `Messages` | `Fil`, `Message` | `Id` | auteur en `Restrict` (un message reste si son auteur part) |
| `Simulations` | `Simulation` | `Id` | `EtatReseauCode` → `EtatsReseau` ; utilisateur mis à null à la suppression |
| `Revisions` | `Revision` | `Id` | **sans** clé étrangère vers l'étape : l'historique survit à une suppression ; index (`EtapeId`, `Date`) |
| `EtatsReseau` | `EtatReseau` | `Code` (« A », « B ») | réseaux, règles et règles NAT en cascade |
| `Noeuds` | `Noeud` | `Id` (`NoeudId` converti) | position dans le SVG (centre, taille) ; `Services` (ports en écoute lus par le moteur réseau) : **type owned rangé en JSON** dans une colonne (`ToJson`, jalon 3) |
| `Interfaces` | `Interface` | `Id` | `Adresse` en **type owned** (`AdresseIp` : adresse + préfixe) ; réseau en `Restrict` ; `Affectation` : nom de l'interface dans OPNsense (WAN, LAN, DMZ-WEB…), là où s'écrivent les règles (jalon 3) |
| `Reseaux` | `Reseau` | `Id` | `Code` unique ; `Prefixe` en **type owned** ; cadre de zone dans le SVG |
| `Liens` | `Lien` | `Id` (`LienId` converti) | `Points` de la polyligne en JSON |
| `Regles`, `ReglesNat` | `Regle`, `RegleNat` | `Id` | index (`EtatReseauCode`, `Equipement`, `Interface`, `Ordre`) : première correspondance ; `RegleNat.Filtrage` (`Aucune`, `Associee`, `Pass` : champ « Firewall rule » d'OPNsense, E36), en texte (jalon 3) |
| `AspNet…` | Identity | | `IdentityDbContext<ApplicationUser>` ; `NumeroRole` sur `AspNetUsers` |

**Propriétés shadow** `DateCreation` et `DateModification` : ajoutées dans `OnModelCreating` à toute entité racine qui implémente `IHorodate` (contenu éditable, questions, fils), absentes des classes C#, remplies par `IntercepteurHorodatage` (`SaveChangesInterceptor`).

**Lazy loading non activé** : les navigations sont chargées explicitement (`Include` / `ThenInclude`, ou `Entry().Collection().LoadAsync()` pour l'historique), pour éviter les requêtes N+1 (justification dans `docs/choix-techniques.md`).

## 3. Source des données

| Données | Origine | Moment |
|---|---|---|
| Rôles, étapes, commandes, effets, pannes, critères | `donnees/commandes/role-1.json` … `role-6.json`, produits par `P6Explorer.Importeur` depuis le guide | amorçage, une seule fois (base vide) |
| Topologie (nœuds, réseaux, liens, règles, NAT) | `donnees/topologie/etat-A.json` (toutes les machines) et `etat-B.json` (cartes, réseaux, règles et NAT de l'état B seulement : fusionnés dans les machines de A) | amorçage, une seule fois |
| Rôles Identity `Admin`, `Membre` ; compte admin | `RoleManager`, `UserManager` ; mot de passe dans les user-secrets | à chaque démarrage s'ils manquent |
| Révisions, quiz, tentatives, progression, fils, simulations, preuves | l'équipe, par l'application | en continu |

Après l'amorçage, **la base est la source de vérité** : les fichiers JSON ne sont plus relus.

## 4. Migrations

| Migration | Jalon | Contenu |
|---|---|---|
| `Initiale` | 2 | 34 tables |
| `MoteurReseau` | 3 | `Noeuds.Services` (JSON), `Interfaces.Affectation`, `ReglesNat.Filtrage` (défaut `Aucune`) |

L'amorçage de la topologie ne tourne que sur une base sans état réseau : une base créée avant le jalon 3 n'aurait pas l'état B. Aucune ne l'a été (LocalDB n'était pas installé) ; sinon, la supprimer (`dotnet ef database drop`) avant de relancer.
