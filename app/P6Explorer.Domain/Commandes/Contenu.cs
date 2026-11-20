using System.Text.Json.Serialization;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Suivi;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Domain.Commandes;

/// <summary>
/// Marque les entités éditables : l'intercepteur EF Core remplit leurs propriétés shadow
/// DateCreation et DateModification (elles n'apparaissent pas dans le modèle C#).
/// </summary>
public interface IHorodate;

/// <summary>Un des six rôles du projet.</summary>
public class Role
{
    public int Numero { get; set; }                  // 1 à 6, clé
    public string Cle { get; set; } = "";            // « chef », « parefeu »… (identifiant du guide)
    public string Titre { get; set; } = "";
    public string CourtNom { get; set; } = "";
    public string Couleur { get; set; } = "";        // variable CSS : « --r1 »
    public string Mission { get; set; } = "";
    public string? Introduction { get; set; }
    public string? Demonstration { get; set; }       // « Démonstration finale (2 minutes) »
    public List<Etape> Etapes { get; set; } = [];
}

/// <summary>Une étape d'un rôle (47 au total).</summary>
public class Etape : IHorodate
{
    [JsonIgnore] public int Id { get; set; }
    [JsonIgnore] public int RoleNumero { get; set; }
    [JsonIgnore] public Role? Role { get; set; }

    public int Numero { get; set; }                  // 0 à 9 (le rôle 3 a une étape 0)
    public string Code { get; set; } = "";           // « r1-e5 »
    public string Titre { get; set; } = "";
    public string? Quand { get; set; }
    public bool Fait { get; set; }
    public string? EtatTexte { get; set; }           // « déjà fait », « en partie fait »
    public StatutValidation Statut { get; set; } = StatutValidation.Validee;
    public string? MiseAJour { get; set; }
    public string But { get; set; } = "";
    public string Pourquoi { get; set; } = "";
    public string? Prerequis { get; set; }
    public string? Livrable { get; set; }
    public string? Schema { get; set; }              // schéma du guide associé (« nat-steps »…)

    public List<Commande> Commandes { get; set; } = [];
    public List<Panne> Pannes { get; set; } = [];
    public List<CritereFin> Criteres { get; set; } = [];
    [JsonIgnore] public List<Preuve> Preuves { get; set; } = [];
    [JsonIgnore] public List<TermeGlossaire> Termes { get; set; } = [];
    [JsonIgnore] public Quiz? Quiz { get; set; }
}

/// <summary>Une commande tapée (ou une action cliquée) et les effets qu'elle produit sur le schéma.</summary>
public class Commande : IHorodate
{
    [JsonIgnore] public int Id { get; set; }
    [JsonIgnore] public int EtapeId { get; set; }
    [JsonIgnore] public Etape? Etape { get; set; }
    [JsonIgnore] public int? ParentId { get; set; }
    [JsonIgnore] public Commande? Parent { get; set; }
    public List<Commande> SousCommandes { get; set; } = [];

    public string Code { get; set; } = "";           // « r1-e5-c03 »
    public int Ordre { get; set; }
    public NoeudId Ou { get; set; }                  // machine où l'on tape
    public Shell Shell { get; set; }
    public string? Compte { get; set; }              // « karim », « root », « p6labo »
    public string? Bloc { get; set; }                // titre du bloc du guide (« Contrôles (PowerShell) »)
    public string Texte { get; set; } = "";          // commande affichée (réellement utilisée si l'étape est faite)
    public string? TexteGuide { get; set; }          // texte d'origine du guide, quand il diffère
    public string Explication { get; set; } = "";
    public string? Sortie { get; set; }              // sortie attendue (tirée du guide)
    public bool EstTest { get; set; }                // commande de la rubrique « Tester »
    public List<Effet> Effets { get; set; } = [];
}

public class Panne : IHorodate
{
    [JsonIgnore] public int Id { get; set; }
    [JsonIgnore] public int EtapeId { get; set; }
    public int Ordre { get; set; }
    public string Symptome { get; set; } = "";
    public string Cause { get; set; } = "";
    public string Solution { get; set; } = "";
}

public class CritereFin : IHorodate
{
    [JsonIgnore] public int Id { get; set; }
    [JsonIgnore] public int EtapeId { get; set; }
    public int Ordre { get; set; }
    public string Texte { get; set; } = "";
}

/// <summary>Capture d'écran jointe à une étape comme preuve (Live View, 12/12…).</summary>
public class Preuve : IHorodate
{
    public int Id { get; set; }
    public int EtapeId { get; set; }
    public Etape? Etape { get; set; }
    public string NomFichier { get; set; } = "";     // nom généré dans wwwroot/preuves/
    public string NomOriginal { get; set; } = "";
    public string TypeMime { get; set; } = "";
    public long Taille { get; set; }
    public string? Legende { get; set; }
    public string? AuteurId { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>Terme du glossaire affiché au survol.</summary>
public class TermeGlossaire : IHorodate
{
    public int Id { get; set; }
    public string Terme { get; set; } = "";
    public string Definition { get; set; } = "";
    public string? Comparaison { get; set; }
    public List<Etape> Etapes { get; set; } = [];
}
