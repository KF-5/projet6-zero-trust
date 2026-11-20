using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Moteur;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Tests.Outils;

/// <summary>Le laboratoire simulé, construit depuis donnees/topologie/etat-A.json et etat-B.json.</summary>
public static class Labo
{
    private static readonly Lazy<SimulateurPaquet> SimulateurA = new(() => Creer("A"));
    private static readonly Lazy<SimulateurPaquet> SimulateurB = new(() => Creer("B"));

    public static SimulateurPaquet Etat(string etat) => etat switch
    {
        "A" => SimulateurA.Value,
        "B" => SimulateurB.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(etat))
    };

    public static ResultatSimulation Simuler(string etat, string source, string destination, Protocole protocole,
        int? port = null, OptionsSimulation? options = null)
        => Etat(etat).Simuler(new DemandeSimulation(new NoeudId(source), destination, protocole, port), options);

    /// <summary>Message d'échec lisible : le verdict, l'explication et le chemin.</summary>
    public static string Decrire(ResultatSimulation r)
        => $"{r.Verdict} (règle {r.Regle ?? "-"}) : {r.Explication}{Environment.NewLine}  "
           + string.Join(Environment.NewLine + "  ", r.Chemin.Select(s => $"{s.Noeud} [{s.Entree} → {s.Sortie}] {s.Texte}"));

    // Fichiers relus à chaque construction : la fusion A + B modifie les nœuds du premier fichier.
    private static SimulateurPaquet Creer(string etat)
        => new(TopologieSimulee.Depuis(etat, [Depot.LireTopologie("A"), Depot.LireTopologie("B")]));
}
