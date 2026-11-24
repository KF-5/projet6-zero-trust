using System.Globalization;
using P6Explorer.Domain.Interfaces;
using P6Explorer.Domain.Video;

namespace P6Explorer.Infrastructure.Fichiers;

/// <summary>
/// Résultats A contre B (P-7, P-8). Le rôle 6 dépose resultats/synthese/synthese.csv : lu à chaque appel, la page
/// change sans redémarrer. Absent ou invalide : l'exemple donnees/video/resultats-exemple.csv, marqué « illustration ».
/// </summary>
public sealed class SourceSynthese(DossiersDepot dossiers) : ISourceSynthese
{
    public static readonly string[] Entete = ["indicateur", "libelle", "etat", "valeur", "unite"];

    public Synthese Charger()
    {
        var remarques = new List<string>();
        var reel = dossiers.Resultats is null ? null : Path.Combine(dossiers.Resultats, "synthese", "synthese.csv");
        if (reel is null || !File.Exists(reel)) remarques.Add("resultats/synthese/synthese.csv absent.");
        else
        {
            var (lignes, erreurs) = Lire(reel);
            if (erreurs.Count == 0) return new Synthese { Lignes = lignes, Source = "resultats/synthese/synthese.csv" };
            remarques.AddRange(erreurs.Select(e => "synthese.csv : " + e));
        }

        var exemple = dossiers.CheminDonnees("video", "resultats-exemple.csv");
        if (exemple is not null && File.Exists(exemple))
        {
            var (lignes, erreurs) = Lire(exemple);
            remarques.AddRange(erreurs.Select(e => "resultats-exemple.csv : " + e));
            return new Synthese { Lignes = erreurs.Count == 0 ? lignes : [], Illustration = true, Source = "donnees/video/resultats-exemple.csv", Remarques = remarques };
        }
        remarques.Add("donnees/video/resultats-exemple.csv absent.");
        return new Synthese { Illustration = true, Source = "donnees/video/resultats-exemple.csv", Remarques = remarques };
    }

    /// <summary>Vérifie le format décrit dans resultats/synthese/FORMAT.md ; rend les lignes et la liste des erreurs.</summary>
    public static (List<IndicateurSynthese> Lignes, List<string> Erreurs) Lire(string chemin)
    {
        var erreurs = new List<string>();
        var (entete, brutes) = Csv.Lire(chemin);
        if (!entete.SequenceEqual(Entete))
            return ([], [$"en-tête attendu « {string.Join(",", Entete)} », trouvé « {string.Join(",", entete)} »."]);
        var lignes = new List<IndicateurSynthese>();
        for (var i = 0; i < brutes.Count; i++)
        {
            var l = brutes[i];
            var n = i + 2;                                   // numéro de ligne dans le fichier (l'en-tête est la 1)
            if (l.Length != Entete.Length) { erreurs.Add($"ligne {n} : {Entete.Length} colonnes attendues, {l.Length} trouvées."); continue; }
            if (l[0].Length == 0 || l[1].Length == 0) erreurs.Add($"ligne {n} : indicateur et libellé obligatoires.");
            if (l[2] is not ("A" or "B")) erreurs.Add($"ligne {n} : état « {l[2]} » (A ou B attendu).");
            if (!double.TryParse(l[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var valeur) || valeur < 0)
                erreurs.Add($"ligne {n} : valeur « {l[3]} » illisible (nombre positif, point décimal).");
            else lignes.Add(new IndicateurSynthese(l[0], l[1], l[2], valeur, l[4]));
        }
        if (brutes.Count == 0) erreurs.Add("aucune ligne de données.");
        foreach (var g in lignes.GroupBy(x => x.Indicateur))
        {
            if (g.Count(x => x.Etat == "A") != 1 || g.Count(x => x.Etat == "B") != 1)
                erreurs.Add($"indicateur « {g.Key} » : une ligne A et une ligne B attendues.");
            if (g.Select(x => x.Unite).Distinct().Count() > 1)
                erreurs.Add($"indicateur « {g.Key} » : la même unité est attendue en A et en B.");
        }
        return (lignes, erreurs);
    }
}
