using System.ComponentModel.DataAnnotations;

namespace P6Explorer.Application.ViewModels;

// ---------------------------------------------------------------- « Et si ? »

public sealed record ChoixMachineVM(string Valeur, string Libelle);

public class SimulationFormVM
{
    public string Etat { get; set; } = "A";
    public List<ChoixMachineVM> Sources { get; set; } = [];
    public List<ChoixMachineVM> Destinations { get; set; } = [];
    public SchemaVM Schema { get; set; } = new();

    [Required(ErrorMessage = "Choisis la machine source.")]
    [Display(Name = "Depuis")]
    public string Source { get; set; } = "client-ext";

    [Required(ErrorMessage = "Indique l'adresse visée.")]
    [RegularExpression(@"^\d{1,3}(\.\d{1,3}){3}$", ErrorMessage = "Écris une adresse IPv4, par exemple 10.10.0.70.")]
    [Display(Name = "Vers (adresse)")]
    public string Destination { get; set; } = "10.10.0.70";

    [Display(Name = "Protocole")]
    public string Protocole { get; set; } = "PostgreSql";

    [Range(1, 65535, ErrorMessage = "Un port va de 1 à 65535.")]
    [Display(Name = "Port")]
    public int? Port { get; set; } = 5432;

    [StringLength(120)]
    [Display(Name = "Titre (pour l'historique)")]
    public string? Titre { get; set; }

    [Display(Name = "Mesures iperf3 en cours (FA6 / F09)")]
    public bool Mesures { get; set; }

    [Display(Name = "SSH d'OPNsense réactivé (Q13)")]
    public bool SshOpnsense { get; set; }

    [Display(Name = "Filtre syslog du collecteur (Q16)")]
    public bool DurcissementQ16 { get; set; }
}

public class SimulationLigneVM
{
    public int Id { get; set; }
    public string? Titre { get; set; }
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Protocole { get; set; } = "";
    public int? Port { get; set; }
    public string Etat { get; set; } = "A";
    public string Verdict { get; set; } = "";
    public string VerdictTexte { get; set; } = "";
    public string? Regle { get; set; }
    public string? Chemin { get; set; }
    public bool PourDemo { get; set; }
    public int? OrdreDemo { get; set; }
    public DateTime Date { get; set; }
}

// ---------------------------------------------------------------- quiz

public class QuizVM
{
    public int QuizId { get; set; }
    public int EtapeId { get; set; }
    public int RoleNumero { get; set; }
    public int EtapeNumero { get; set; }
    public string EtapeTitre { get; set; } = "";
    public int Seuil { get; set; } = 80;
    public bool PeutModifier { get; set; }
    public List<QuestionVM> Questions { get; set; } = [];
    public List<TentativeVM> Tentatives { get; set; } = [];
}

public class QuestionVM
{
    public int Id { get; set; }
    public string Texte { get; set; } = "";
    public string Statut { get; set; } = "";
    public string? Source { get; set; }
    public bool PlusieursReponses { get; set; }
    public List<ChoixVM> Choix { get; set; } = [];
}

public class ChoixVM
{
    public int Id { get; set; }
    public string Texte { get; set; } = "";
}

/// <summary>Réponses envoyées par le formulaire : pour chaque question, les choix cochés.</summary>
public class ReponsesQuizVM
{
    public int QuizId { get; set; }
    public Dictionary<int, List<int>> Reponses { get; set; } = [];
}

public class ResultatQuizVM
{
    public int RoleNumero { get; set; }
    public int EtapeNumero { get; set; }
    public string EtapeTitre { get; set; } = "";
    public int Score { get; set; }
    public int Seuil { get; set; }
    public bool Reussi => Score >= Seuil;
    public List<CorrectionQuestionVM> Corrections { get; set; } = [];
}

public class CorrectionQuestionVM
{
    public string Question { get; set; } = "";
    public bool Juste { get; set; }
    public List<string> BonnesReponses { get; set; } = [];
    public List<string> TesReponses { get; set; } = [];
    public string? Explication { get; set; }
}

public class TentativeVM
{
    public DateTime Date { get; set; }
    public int Score { get; set; }
}

public class QuestionFormVM
{
    public int Id { get; set; }
    public int RoleNumero { get; set; }
    public int EtapeNumero { get; set; }

    [Required(ErrorMessage = "La question est obligatoire.")]
    [StringLength(1000)]
    [Display(Name = "Question")]
    public string Texte { get; set; } = "";

    [Display(Name = "Statut")]
    public Domain.Enumerations.StatutValidation Statut { get; set; }

    public List<ChoixFormVM> Choix { get; set; } = [];
}

public class ChoixFormVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Un choix ne peut pas être vide.")]
    [StringLength(500)]
    public string Texte { get; set; } = "";

    public bool Correct { get; set; }
}

/// <summary>Admin › Préparation de la soutenance.</summary>
public class PreparationVM
{
    public List<string> Membres { get; set; } = [];
    public List<LignePreparationVM> Lignes { get; set; } = [];
    public List<string> PersonneNeMaitrise { get; set; } = [];
    public int EtapesMaitrisees { get; set; }
}

public class LignePreparationVM
{
    public int RoleNumero { get; set; }
    public string Etape { get; set; } = "";
    public List<ScoreMembreVM?> Scores { get; set; } = [];
}

public class ScoreMembreVM
{
    public int Score { get; set; }
    public DateTime Date { get; set; }
    public int Tentatives { get; set; }
    public bool Reussi { get; set; }
}

// ---------------------------------------------------------------- questions et réponses

public class FilLigneVM
{
    public int Id { get; set; }
    public string Titre { get; set; } = "";
    public string Auteur { get; set; } = "";
    public string Statut { get; set; } = "";
    public DateTime Date { get; set; }
    public int NbMessages { get; set; }
    public string CommandeCode { get; set; } = "";
    public int RoleNumero { get; set; }
    public int EtapeNumero { get; set; }
    public int CommandeIndex { get; set; }
}

public class FilVM
{
    public int Id { get; set; }
    public string Titre { get; set; } = "";
    public string Statut { get; set; } = "";
    public string Auteur { get; set; } = "";
    public string CommandeCode { get; set; } = "";
    public string CommandeTexte { get; set; } = "";
    public int RoleNumero { get; set; }
    public int EtapeNumero { get; set; }
    public bool PeutResoudre { get; set; }
    public List<MessageVM> Messages { get; set; } = [];
    public NouveauMessageVM Reponse { get; set; } = new();
}

public class MessageVM
{
    public string Auteur { get; set; } = "";
    public DateTime Date { get; set; }
    public string TexteHtml { get; set; } = "";
}

public class NouveauFilVM
{
    public int CommandeId { get; set; }
    public string? CommandeCode { get; set; }
    public string? CommandeTexte { get; set; }

    [Required(ErrorMessage = "Donne un titre à ta question.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Entre 5 et 200 caractères.")]
    [Display(Name = "Titre")]
    public string Titre { get; set; } = "";

    [Required(ErrorMessage = "Écris ta question.")]
    [StringLength(4000)]
    [Display(Name = "Ta question")]
    public string Texte { get; set; } = "";
}

public class NouveauMessageVM
{
    public int FilId { get; set; }

    [Required(ErrorMessage = "Le message est vide.")]
    [StringLength(4000)]
    [Display(Name = "Ta réponse")]
    public string Texte { get; set; } = "";
}

public class DiscussionsVM
{
    public string Filtre { get; set; } = "Ouverts";
    public PageVM<FilLigneVM> Fils { get; set; } = new();
}

// ---------------------------------------------------------------- comptes

public class CompteLigneVM
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Nom { get; set; }
    public int? NumeroRole { get; set; }
    public bool Admin { get; set; }
    public bool Verrouille { get; set; }
}

public class NouveauCompteVM
{
    [Required(ErrorMessage = "L'adresse est obligatoire.")]
    [EmailAddress(ErrorMessage = "Adresse invalide.")]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Le nom affiché est obligatoire.")]
    [StringLength(60)]
    [Display(Name = "Nom affiché")]
    public string Nom { get; set; } = "";

    [Range(1, 6, ErrorMessage = "Le numéro de rôle va de 1 à 6.")]
    [Display(Name = "Numéro de rôle")]
    public int NumeroRole { get; set; } = 2;

    [Required(ErrorMessage = "Choisis un mot de passe provisoire.")]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "10 caractères au moins.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe provisoire")]
    public string MotDePasse { get; set; } = "";
}

public class ConnexionVM
{
    [Required(ErrorMessage = "L'adresse est obligatoire.")]
    [EmailAddress(ErrorMessage = "Adresse invalide.")]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string MotDePasse { get; set; } = "";

    [Display(Name = "Rester connecté")]
    public bool Memoriser { get; set; }

    public string? Retour { get; set; }
}

public class ChangerMotDePasseVM
{
    [Required(ErrorMessage = "Obligatoire.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe actuel")]
    public string Actuel { get; set; } = "";

    [Required(ErrorMessage = "Obligatoire.")]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "10 caractères au moins.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nouveau mot de passe")]
    public string Nouveau { get; set; } = "";

    [Compare(nameof(Nouveau), ErrorMessage = "Les deux mots de passe ne correspondent pas.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmation")]
    public string Confirmation { get; set; } = "";
}

public class AvancementVM
{
    public List<AvancementMembreVM> Membres { get; set; } = [];
}

public class AvancementMembreVM
{
    public string Nom { get; set; } = "";
    public int? NumeroRole { get; set; }
    public int CommandesComprises { get; set; }
    public int EtapesMaitrisees { get; set; }
}
