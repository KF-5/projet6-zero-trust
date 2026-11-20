using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Enumerations;

namespace P6Explorer.Domain.Suivi;

/// <summary>Une modification d'un champ, écrite par l'intercepteur EF Core (historique et retour arrière).</summary>
public class Revision
{
    public int Id { get; set; }
    public Guid Lot { get; set; }                     // regroupe les champs d'un même enregistrement
    public string Entite { get; set; } = "";          // « Etape », « Commande », « Effet »…
    public int EntiteId { get; set; }
    public int? EtapeId { get; set; }
    public string Champ { get; set; } = "";
    public string? Avant { get; set; }
    public string? Apres { get; set; }
    public string? AuteurId { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>Auto-évaluation : un quiz par étape (relation 1-1).</summary>
public class Quiz
{
    public int Id { get; set; }
    public int EtapeId { get; set; }
    public Etape? Etape { get; set; }
    public double Seuil { get; set; } = 0.8;
    public List<Question> Questions { get; set; } = [];
    public List<Tentative> Tentatives { get; set; } = [];
}

public class Question : IHorodate
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public Quiz? Quiz { get; set; }
    public int Ordre { get; set; }
    public string Texte { get; set; } = "";
    public StatutValidation Statut { get; set; } = StatutValidation.AValider;
    public string? Source { get; set; }               // « jury », « panne », « but »…
    public List<Choix> Choix { get; set; } = [];
}

public class Choix : IHorodate
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public Question? Question { get; set; }
    public int Ordre { get; set; }
    public string Texte { get; set; } = "";
    public bool Correct { get; set; }
    public string? Explication { get; set; }
}

public class Tentative
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public Quiz? Quiz { get; set; }
    public string UtilisateurId { get; set; } = "";
    public DateTime Date { get; set; }
    public double Score { get; set; }
    public List<Reponse> Reponses { get; set; } = [];
}

/// <summary>Table de jointure n-n entre Tentative et Choix (les choix cochés).</summary>
public class Reponse
{
    public int TentativeId { get; set; }
    public Tentative? Tentative { get; set; }
    public int ChoixId { get; set; }
    public Choix? Choix { get; set; }
}

/// <summary>Un membre a coché « j'ai compris » sur une commande.</summary>
public class Progression
{
    public int Id { get; set; }
    public string UtilisateurId { get; set; } = "";
    public int CommandeId { get; set; }
    public Commande? Commande { get; set; }
    public bool Comprise { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>Fil de questions et réponses sur une commande.</summary>
public class Fil : IHorodate
{
    public int Id { get; set; }
    public int CommandeId { get; set; }
    public Commande? Commande { get; set; }
    public string AuteurId { get; set; } = "";
    public string Titre { get; set; } = "";
    public StatutFil Statut { get; set; } = StatutFil.Ouvert;
    public DateTime Date { get; set; }
    public List<Message> Messages { get; set; } = [];
}

public class Message : IHorodate
{
    public int Id { get; set; }
    public int FilId { get; set; }
    public Fil? Fil { get; set; }
    public string AuteurId { get; set; } = "";
    public string Texte { get; set; } = "";
    public DateTime Date { get; set; }
}

/// <summary>Une simulation « Et si ? » enregistrée.</summary>
public class Simulation
{
    public int Id { get; set; }
    public string? UtilisateurId { get; set; }
    public string? Titre { get; set; }
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public Protocole Protocole { get; set; }
    public int? Port { get; set; }
    public string EtatReseauCode { get; set; } = "A";
    public Verdict Verdict { get; set; }
    public string? Regle { get; set; }
    public string? Chemin { get; set; }
    public bool PourDemo { get; set; }
    public int? OrdreDemo { get; set; }
    public DateTime Date { get; set; }
}
