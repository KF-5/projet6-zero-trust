namespace P6Explorer.Application.ViewModels;

/// <summary>Page /Video : les 8 scènes à filmer et les erreurs de scenes.json, affichées en clair.</summary>
public class VideoIndexVM
{
    public List<SceneLigneVM> Scenes { get; set; } = [];
    public List<string> Erreurs { get; set; } = [];
    /// <summary>Vrai : resultats/synthese/synthese.csv absent ou invalide, P-7 et P-8 sont des illustrations.</summary>
    public bool ResultatsIllustration { get; set; }
    public List<string> RemarquesResultats { get; set; } = [];
}

public class SceneLigneVM
{
    public string Id { get; set; } = "";
    public string Titre { get; set; } = "";
    public int Sequence { get; set; }
    public double DureeS { get; set; }
    public string Etat { get; set; } = "A";
    /// <summary>« prête », « illustration » ou « cible ».</summary>
    public string Statut { get; set; } = "prête";
    public int NbEtapes { get; set; }
}

/// <summary>Une scène en mode tournage (/Video/P-5) : tout ce que lecteur.js (mode video) joue, sans appel en écriture.</summary>
public class SceneTournageVM
{
    public string Id { get; set; } = "";
    public string Titre { get; set; } = "";
    public int Sequence { get; set; }
    public double DureeS { get; set; }
    public string Etat { get; set; } = "A";
    public bool Cible { get; set; }
    /// <summary>?propre=1 : sans le coin « P-5 », pour la prise finale.</summary>
    public bool Propre { get; set; }
    /// <summary>?lecture=auto : démarre seule après l'image fixe (vérification des durées, pas pour le tournage).</summary>
    public bool LectureAuto { get; set; }
    public string? Precedente { get; set; }
    public string? Suivante { get; set; }
    /// <summary>Schémas des deux états, toujours sans les adresses réservées (MembreConnecte = false), même connecté.</summary>
    public SchemaVM SchemaA { get; set; } = new();
    public SchemaVM SchemaB { get; set; } = new();
    public List<EtapeLectureVM> Etapes { get; set; } = [];
    /// <summary>État → nom de zone en majuscules (WAN, DMZ-WEB…) → cadres et machines à mettre en avant.</summary>
    public Dictionary<string, Dictionary<string, ZoneSceneVM>> Zones { get; set; } = [];
    public Dictionary<string, MatriceVM> Matrices { get; set; } = [];
    public ResultatsVM? Resultats { get; set; }
    public ResumeVM? Resume { get; set; }
    public List<string> Erreurs { get; set; } = [];
}

/// <summary>Une étape telle que lecteur.js la joue (textes déjà résolus côté serveur, par exemple les segments).</summary>
public class EtapeLectureVM
{
    public string Type { get; set; } = "";
    public double? DureeS { get; set; }
    public double? JusquAS { get; set; }
    public string? Texte { get; set; }
    public string? Legende { get; set; }
    public List<string> Noeuds { get; set; } = [];
    public List<string> Zones { get; set; } = [];
    public string? Source { get; set; }
    public string? Destination { get; set; }
    public string? Protocole { get; set; }
    public int? Port { get; set; }
    public string? Etat { get; set; }
    public double? Vitesse { get; set; }
    public string? Ligne { get; set; }
    public string? Gauche { get; set; }
    public string? Droite { get; set; }
}

public class ZoneSceneVM
{
    /// <summary>Codes des cadres de zone du schéma (attribut data-reseau).</summary>
    public List<string> Reseaux { get; set; } = [];
    public List<string> Noeuds { get; set; } = [];
}

/// <summary>Matrice de flux d'un état (P-4 et /Politique), construite depuis donnees/politique/*.csv.</summary>
public class MatriceVM
{
    public string Etat { get; set; } = "A";
    public List<string> Sources { get; set; } = [];
    public List<string> Destinations { get; set; } = [];
    /// <summary>[ligne][colonne] : les flux autorisés de cette case ; vide = refus par défaut.</summary>
    public List<List<List<CaseMatriceVM>>> Cases { get; set; } = [];
    /// <summary>Identifiant du refus par défaut (FA5 en A, F99 en B).</summary>
    public string? IdRefusDefaut { get; set; }
    public int NbFlux { get; set; }
    public int NbTests { get; set; }
    public int NbPass { get; set; }
    public int NbBlock { get; set; }
    public List<FluxLigneVM> Flux { get; set; } = [];
    public string FichierTests => $"tests-{Etat}.csv";
}

public class CaseMatriceVM
{
    public string FluxId { get; set; } = "";
    public string Proto { get; set; } = "";
    public string Ports { get; set; } = "";
}

/// <summary>Une ligne de flows.csv et les tests de tests-&lt;état&gt;.csv qui la vérifient (flux_ref).</summary>
public class FluxLigneVM
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Proto { get; set; } = "";
    public string Ports { get; set; } = "";
    public string Justification { get; set; } = "";
    public bool RefusParDefaut { get; set; }
    /// <summary>La ligne telle qu'elle est écrite dans flows.csv (montrée à l'écran, sans réécriture).</summary>
    public string LigneCsv { get; set; } = "";
    public List<TestLigneVM> Tests { get; set; } = [];
}

public class TestLigneVM
{
    public string Id { get; set; } = "";
    public string SourceHote { get; set; } = "";
    public string IpDestination { get; set; } = "";
    public string Proto { get; set; } = "";
    public string Port { get; set; } = "";
    public string Attendu { get; set; } = "";
}

/// <summary>Résultats A contre B (P-7) : vrais (synthese.csv) ou illustration (bandeau obligatoire).</summary>
public class ResultatsVM
{
    public bool Illustration { get; set; }
    public string Source { get; set; } = "";
    public List<IndicateurVM> Indicateurs { get; set; } = [];
    public List<string> Remarques { get; set; } = [];
}

public class IndicateurVM
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public string Unite { get; set; } = "";
    public double ValeurA { get; set; }
    public double ValeurB { get; set; }
    public double Maximum => Math.Max(Math.Max(ValeurA, ValeurB), 0.0001);
}

/// <summary>Écran de fin (P-8) : A contre B (valeurs calculées), l'équipe, la phrase de fin.</summary>
public class ResumeVM
{
    public List<LigneResumeVM> Lignes { get; set; } = [];
    public List<CarteEquipeVM> Equipe { get; set; } = [];
    public string Phrase { get; set; } = "";
    public string Reproduire { get; set; } = "";
}

public class LigneResumeVM
{
    public string Libelle { get; set; } = "";
    public string A { get; set; } = "";
    public string B { get; set; } = "";
}

public class CarteEquipeVM
{
    public string Titre { get; set; } = "";
    public string? Prenoms { get; set; }
    public int? Numero { get; set; }
}
