using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Topologie;

namespace P6Explorer.Domain.Amorcage;

/// <summary>
/// Contenu d'un fichier donnees/commandes/role-N.json, écrit par l'importeur et lu une seule fois
/// par le service d'amorçage. Ensuite, la base est la source de vérité.
/// </summary>
public class FichierRole
{
    public int Version { get; set; } = 1;
    public string Source { get; set; } = "";
    public Role Role { get; set; } = new();
    /// <summary>« Questions probables du jury » : amorceront les quiz (jalon 7).</summary>
    public List<QuestionJury> QuestionsJury { get; set; } = [];
}

public class QuestionJury
{
    public string Question { get; set; } = "";
    public string Reponse { get; set; } = "";
}

/// <summary>Contenu de donnees/topologie/etat-X.json : machines, réseaux, câbles et règles d'un état.</summary>
public class FichierTopologie
{
    public string Etat { get; set; } = "A";
    public string Libelle { get; set; } = "";
    public string? Description { get; set; }
    public List<Noeud> Noeuds { get; set; } = [];
    public List<Reseau> Reseaux { get; set; } = [];
    public List<Lien> Liens { get; set; } = [];
    public List<Regle> Regles { get; set; } = [];
    public List<RegleNat> ReglesNat { get; set; } = [];

    /// <summary>
    /// Les machines sont décrites une fois (fichier de l'état A) ; un fichier suivant (état B) ne redonne que
    /// leurs cartes propres à cet état (id + interfaces). On ajoute ces cartes et ces services à la première description.
    /// </summary>
    public static List<Noeud> FusionnerNoeuds(IEnumerable<FichierTopologie> fichiers)
    {
        var resultat = new Dictionary<string, Noeud>();
        foreach (var n in fichiers.SelectMany(f => f.Noeuds))
        {
            if (resultat.TryGetValue(n.Id.Valeur, out var existant))
            {
                existant.Interfaces.AddRange(n.Interfaces);
                existant.Services.AddRange(n.Services);
            }
            else resultat[n.Id.Valeur] = n;
        }
        return [.. resultat.Values];
    }
}
