using System.ComponentModel.DataAnnotations;
using P6Explorer.Domain.Enumerations;

namespace P6Explorer.Application.ViewModels;

/// <summary>Une page de résultats, pour les listes paginées (historique, questions, simulations).</summary>
public class PageVM<T>
{
    public List<T> Elements { get; set; } = [];
    public int Page { get; set; } = 1;
    public int NombrePages { get; set; } = 1;
    public int Total { get; set; }
}

/// <summary>Vue rôle : liste des étapes à gauche, étape choisie (schéma, terminal, explication).</summary>
public class EtapeVM
{
    public RoleEnteteVM Role { get; set; } = new();
    public List<EtapeResumeVM> Etapes { get; set; } = [];
    public EtapeDetailVM Etape { get; set; } = new();
    public SchemaVM Schema { get; set; } = new();
    public string Etat { get; set; } = "A";
    public int CommandeInitiale { get; set; } = 1;
    public double Vitesse { get; set; } = 1;
    public bool PeutModifier { get; set; }
    public bool Connecte { get; set; }
    public int QuestionsOuvertes { get; set; }
    public bool QuizDisponible { get; set; }
    public bool Maitrisee { get; set; }
    public List<TermeVM> Termes { get; set; } = [];
}

public class RoleEnteteVM
{
    public int Numero { get; set; }
    public string Titre { get; set; } = "";
    public string CourtNom { get; set; } = "";
    public string Couleur { get; set; } = "";
    public string Mission { get; set; } = "";
    public string? IntroductionHtml { get; set; }
    public string? DemonstrationHtml { get; set; }
}

public class EtapeResumeVM
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public string Titre { get; set; } = "";
    public string? Quand { get; set; }
    public bool Fait { get; set; }
    public string? EtatTexte { get; set; }
    public StatutValidation Statut { get; set; }
    public DateTime? DateModification { get; set; }
    public int QuestionsOuvertes { get; set; }
    public bool Maitrisee { get; set; }
    public bool Active { get; set; }
}

public class EtapeDetailVM
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int Numero { get; set; }
    public string Titre { get; set; } = "";
    public string? Quand { get; set; }
    public bool Fait { get; set; }
    public string? EtatTexte { get; set; }
    public StatutValidation Statut { get; set; }
    public DateTime? DateModification { get; set; }
    public string? MiseAJourHtml { get; set; }
    public string ButHtml { get; set; } = "";
    public string PourquoiHtml { get; set; } = "";
    public string? PrerequisHtml { get; set; }
    public string? LivrableHtml { get; set; }
    public int NbCommandes { get; set; }
    public List<PanneVM> Pannes { get; set; } = [];
    public List<string> Criteres { get; set; } = [];
    public List<PreuveVM> Preuves { get; set; } = [];
    public List<CommandeLigneVM> Commandes { get; set; } = [];
}

public class CommandeLigneVM
{
    public int Id { get; set; }
    public int Ordre { get; set; }
    public string Code { get; set; } = "";
    public string Shell { get; set; } = "";
    public string Ou { get; set; } = "";
    public string Apercu { get; set; } = "";
}

public class PanneVM
{
    public string Symptome { get; set; } = "";
    public string Cause { get; set; } = "";
    public string Solution { get; set; } = "";
}

public class PreuveVM
{
    public int Id { get; set; }
    public string Url { get; set; } = "";
    public string NomOriginal { get; set; } = "";
    public string? Legende { get; set; }
    public DateTime Date { get; set; }
}

public class TermeVM
{
    public int Id { get; set; }
    public string Terme { get; set; } = "";
    public string Definition { get; set; } = "";
    public string? Comparaison { get; set; }
    public List<string> Etapes { get; set; } = [];
}

// ---------------------------------------------------------------- édition

public class EtapeFormVM
{
    public int Id { get; set; }
    public int RoleNumero { get; set; }
    public int Numero { get; set; }
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Le titre est obligatoire.")]
    [StringLength(200, ErrorMessage = "200 caractères au plus.")]
    [Display(Name = "Titre")]
    public string Titre { get; set; } = "";

    [StringLength(60)]
    [Display(Name = "Quand")]
    public string? Quand { get; set; }

    [Display(Name = "Étape déjà réalisée sur la plateforme")]
    public bool Fait { get; set; }

    [Display(Name = "Statut")]
    public StatutValidation Statut { get; set; }

    [Required(ErrorMessage = "Le but est obligatoire.")]
    [StringLength(4000)]
    [Display(Name = "But")]
    public string But { get; set; } = "";

    [Required(ErrorMessage = "Le « pourquoi » est obligatoire.")]
    [StringLength(4000)]
    [Display(Name = "Pourquoi")]
    public string Pourquoi { get; set; } = "";

    [StringLength(2000)]
    [Display(Name = "Mise à jour")]
    public string? MiseAJour { get; set; }

    [StringLength(2000)]
    [Display(Name = "Livrable")]
    public string? Livrable { get; set; }
}

public class CommandeFormVM
{
    public int Id { get; set; }
    public int EtapeId { get; set; }
    public int RoleNumero { get; set; }
    public int EtapeNumero { get; set; }
    public string? Code { get; set; }

    [Range(1, 999, ErrorMessage = "L'ordre va de 1 à 999.")]
    [Display(Name = "Ordre")]
    public int Ordre { get; set; } = 1;

    [Required(ErrorMessage = "Indique la machine.")]
    [Display(Name = "Machine où l'on tape")]
    public string Ou { get; set; } = "hote";

    [Display(Name = "Interface")]
    public Shell Shell { get; set; } = Shell.Bash;

    [StringLength(30)]
    [Display(Name = "Compte")]
    public string? Compte { get; set; }

    [Required(ErrorMessage = "Le texte de la commande est obligatoire.")]
    [StringLength(20000)]
    [Display(Name = "Commande")]
    public string Texte { get; set; } = "";

    [Required(ErrorMessage = "Explique la commande en une ou deux phrases.")]
    [StringLength(4000)]
    [Display(Name = "Explication")]
    public string Explication { get; set; } = "";

    [StringLength(20000)]
    [Display(Name = "Sortie attendue")]
    public string? Sortie { get; set; }

    [Display(Name = "Commande de test")]
    public bool EstTest { get; set; }

    public List<EffetLigneVM> Effets { get; set; } = [];
    public List<string> Machines { get; set; } = [];
}

public class EffetLigneVM
{
    public int Id { get; set; }
    public int Ordre { get; set; }
    public string Type { get; set; } = "";
    public string Resume { get; set; } = "";
    public bool Automatique { get; set; }
}

public class EffetFormVM
{
    public int Id { get; set; }
    public int CommandeId { get; set; }
    public string? CommandeCode { get; set; }

    [Required(ErrorMessage = "Décris l'effet en JSON.")]
    [StringLength(8000)]
    [Display(Name = "Effet (JSON)")]
    public string Json { get; set; } = "";
}

public class HistoriqueVM
{
    public int EtapeId { get; set; }
    public int RoleNumero { get; set; }
    public int EtapeNumero { get; set; }
    public string Titre { get; set; } = "";
    public bool PeutModifier { get; set; }
    public PageVM<LotRevisionVM> Lots { get; set; } = new();
}

public class LotRevisionVM
{
    public Guid Lot { get; set; }
    public DateTime Date { get; set; }
    public string Auteur { get; set; } = "";
    public string Element { get; set; } = "";
    public List<ChampRevisionVM> Champs { get; set; } = [];
}

public class ChampRevisionVM
{
    public string Champ { get; set; } = "";
    public string? Avant { get; set; }
    public string? Apres { get; set; }
}
