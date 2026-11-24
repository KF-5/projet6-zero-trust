namespace P6Explorer.Domain.Video;

/// <summary>Une ligne de resultats/synthese/synthese.csv (format : resultats/synthese/FORMAT.md).</summary>
public record IndicateurSynthese(string Indicateur, string Libelle, string Etat, double Valeur, string Unite);

/// <summary>
/// Les indicateurs A contre B affichés en P-7. <see cref="Illustration"/> vrai : le vrai fichier est absent ou invalide,
/// les valeurs viennent de donnees/video/resultats-exemple.csv et doivent être marquées « ILLUSTRATION ».
/// </summary>
public class Synthese
{
    public List<IndicateurSynthese> Lignes { get; init; } = [];
    public bool Illustration { get; init; }
    /// <summary>Chemin affiché sous le graphique (relatif à la racine du dépôt).</summary>
    public string Source { get; init; } = "";
    /// <summary>Pourquoi le vrai fichier n'a pas été retenu (absent, en-tête, valeur illisible…).</summary>
    public List<string> Remarques { get; init; } = [];
}
