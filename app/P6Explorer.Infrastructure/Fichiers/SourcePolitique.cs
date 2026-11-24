using P6Explorer.Domain.Interfaces;
using P6Explorer.Domain.Politique;

namespace P6Explorer.Infrastructure.Fichiers;

/// <summary>donnees/politique/*.csv, lus une seule fois (singleton) : ce sont des fichiers produits par le rôle 2 et c2.py.</summary>
public sealed class SourcePolitique(DossiersDepot dossiers) : ISourcePolitique
{
    private readonly Lazy<PolitiqueLaboratoire> _politique = new(() => Lire(dossiers));

    public PolitiqueLaboratoire Charger() => _politique.Value;

    private static PolitiqueLaboratoire Lire(DossiersDepot dossiers)
    {
        var dossier = dossiers.CheminDonnees("politique")
                      ?? throw new DirectoryNotFoundException("Dossier donnees/ introuvable (réglage Amorcage:DossierDonnees).");
        static string[] Liste(string v) => v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new PolitiqueLaboratoire
        {
            Flux = Lignes(dossier, "flows.csv", ["id", "src", "dst", "proto", "port", "justification", "etat"])
                .Select(l => new FluxPolitique(l["id"], Liste(l["src"]), Liste(l["dst"]), l["proto"], l["port"], l["justification"], l["etat"])).ToList(),
            Segments = Lignes(dossier, "segments.csv", ["nom", "etat", "reseau", "vlan", "passerelle"])
                .Select(l => new SegmentPolitique(l["nom"], l["etat"], l["reseau"], int.TryParse(l["vlan"], out var v) ? v : null, l["passerelle"])).ToList(),
            Hotes = Lignes(dossier, "hosts.csv", ["hote", "ip", "segment_A", "segment_B", "conteneur"])
                .Select(l => new HotePolitique(l["hote"], l["ip"], l["segment_A"], l["segment_B"], l["conteneur"] is "-" or "" ? null : l["conteneur"])).ToList(),
            TestsA = Tests(dossier, "tests-A.csv"),
            TestsB = Tests(dossier, "tests-B.csv"),
        };
    }

    private static List<TestPolitique> Tests(string dossier, string fichier)
        => Lignes(dossier, fichier, ["id_test", "src_host", "dst_ip", "proto", "port", "attendu", "flux_ref"])
            .Select(l => new TestPolitique(l["id_test"], l["src_host"], l["dst_ip"], l["proto"], l["port"], l["attendu"], l["flux_ref"])).ToList();

    /// <summary>Lignes d'un fichier sous forme colonne → valeur ; une colonne attendue manquante est une erreur claire.</summary>
    private static List<Dictionary<string, string>> Lignes(string dossier, string fichier, string[] colonnes)
    {
        var chemin = Path.Combine(dossier, fichier);
        var (entete, lignes) = Csv.Lire(chemin);
        var manquantes = colonnes.Where(c => !entete.Contains(c)).ToList();
        if (manquantes.Count > 0)
            throw new InvalidDataException($"politique/{fichier} : colonne(s) absente(s) : {string.Join(", ", manquantes)}.");
        return lignes.Select(l => entete.Select((c, i) => (c, v: i < l.Length ? l[i] : ""))
            .GroupBy(x => x.c).ToDictionary(g => g.Key, g => g.First().v)).ToList();
    }
}
