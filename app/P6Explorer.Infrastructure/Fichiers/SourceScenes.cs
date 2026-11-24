using System.Globalization;
using System.Text.Json;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Interfaces;
using P6Explorer.Domain.Video;

namespace P6Explorer.Infrastructure.Fichiers;

/// <summary>
/// Lit donnees/video/scenes.json à chaque appel (l'équipe le modifie sans recompiler ni redémarrer) et le vérifie :
/// 8 scènes P-1 à P-8, identifiants uniques, types d'étapes connus, nœuds et zones présents dans la topologie,
/// somme des durées d'étapes à ±2 s de la durée de la scène. Les erreurs sont rendues, jamais levées.
/// </summary>
public sealed class SourceScenes(DossiersDepot dossiers, ISourcePolitique politique) : ISourceScenes
{
    public const double ToleranceDureeS = 2;
    public static readonly string[] IdentifiantsAttendus = ["P-1", "P-2", "P-3", "P-4", "P-5", "P-6", "P-7", "P-8"];

    public ResultatValidation<FichierScenes> Charger()
    {
        var chemin = dossiers.CheminDonnees("video", "scenes.json");
        if (chemin is null || !File.Exists(chemin))
            return new() { Erreurs = ["donnees/video/scenes.json introuvable."] };
        FichierScenes? fichier;
        try { fichier = JsonSerializer.Deserialize<FichierScenes>(File.ReadAllText(chemin)); }
        catch (JsonException e) { return new() { Erreurs = [$"scenes.json illisible (ligne {e.LineNumber + 1}) : {e.Message}"] }; }
        if (fichier is null) return new() { Erreurs = ["scenes.json est vide."] };
        return new() { Valeur = fichier, Erreurs = Valider(fichier, Topologie()) };
    }

    /// <summary>Noms de la topologie, pour vérifier les identifiants écrits dans scenes.json.</summary>
    public sealed record ReferenceTopologie(HashSet<string> Noeuds, HashSet<string> Zones);

    private ReferenceTopologie Topologie()
    {
        var noeuds = new HashSet<string>(StringComparer.Ordinal);
        var zones = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TRUNK" };
        var dossier = dossiers.CheminDonnees("topologie");
        if (dossier is not null && Directory.Exists(dossier))
            foreach (var f in Directory.GetFiles(dossier, "etat-*.json"))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(f));
                if (doc.RootElement.TryGetProperty("noeuds", out var n))
                    foreach (var x in n.EnumerateArray()) noeuds.Add(x.GetProperty("id").GetString()!);
                if (doc.RootElement.TryGetProperty("reseaux", out var r))
                    foreach (var x in r.EnumerateArray())
                    {
                        zones.Add(x.GetProperty("code").GetString()!);
                        if (x.TryGetProperty("zone", out var z)) zones.Add(z.GetString()!);
                    }
            }
        foreach (var s in politique.Charger().Segments) zones.Add(s.Nom);
        return new ReferenceTopologie(noeuds, zones);
    }

    public static List<string> Valider(FichierScenes fichier, ReferenceTopologie reference)
    {
        var erreurs = new List<string>();
        var ids = fichier.Scenes.Select(s => s.Id).ToList();
        if (fichier.Scenes.Count != IdentifiantsAttendus.Length)
            erreurs.Add($"{IdentifiantsAttendus.Length} scènes attendues, {fichier.Scenes.Count} trouvées.");
        foreach (var doublon in ids.GroupBy(i => i).Where(g => g.Count() > 1))
            erreurs.Add($"identifiant « {doublon.Key} » utilisé {doublon.Count()} fois.");
        foreach (var manquant in IdentifiantsAttendus.Except(ids))
            erreurs.Add($"scène {manquant} absente.");
        foreach (var inconnu in ids.Except(IdentifiantsAttendus).Distinct())
            erreurs.Add($"scène « {inconnu} » inattendue (P-1 à P-8).");

        foreach (var s in fichier.Scenes)
        {
            var nom = s.Id.Length > 0 ? s.Id : "(scène sans id)";
            if (string.IsNullOrWhiteSpace(s.Titre)) erreurs.Add($"{nom} : titre vide.");
            if (s.Etat is not ("A" or "B")) erreurs.Add($"{nom} : état « {s.Etat} » (A ou B attendu).");
            if (s.DureeS <= 0) erreurs.Add($"{nom} : duree_s doit être positive.");
            if (s.Etapes.Count == 0) erreurs.Add($"{nom} : aucune étape.");
            for (var i = 0; i < s.Etapes.Count; i++)
                erreurs.AddRange(ValiderEtape(s, i, reference).Select(e => $"{nom}, étape {i + 1} ({s.Etapes[i].Type}) : {e}"));
            var total = DureeTotale(s);
            if (s.DureeS > 0 && Math.Abs(total - s.DureeS) > ToleranceDureeS)
                erreurs.Add(string.Create(CultureInfo.GetCultureInfo("fr-FR"),
                    $"{nom} : les étapes durent {total:0.#} s pour une durée annoncée de {s.DureeS:0.#} s (écart toléré : ±{ToleranceDureeS} s)."));
        }
        return erreurs;
    }

    /// <summary>Somme des durées d'étapes ; une pause « jusqu_a_s » complète jusqu'à l'instant demandé.</summary>
    public static double DureeTotale(SceneVideo s)
    {
        double t = 0;
        foreach (var e in s.Etapes)
            t = e.TypeConnu == TypeEtapeScene.Pause && e.JusquAS is { } jusqua ? Math.Max(t, jusqua) : t + (e.DureeS ?? 0);
        return t;
    }

    private static IEnumerable<string> ValiderEtape(SceneVideo s, int i, ReferenceTopologie reference)
    {
        var e = s.Etapes[i];
        if (e.TypeConnu is not { } type) { yield return $"type « {e.Type} » inconnu."; yield break; }
        if (e.DureeS < 0) yield return "duree_s négative.";
        foreach (var n in e.Noeuds ?? [])
            if (!reference.Noeuds.Contains(n)) yield return $"nœud « {n} » absent de la topologie.";
        foreach (var z in e.Zones ?? [])
            if (!reference.Zones.Contains(z)) yield return $"zone « {z} » inconnue (topologie ou segments.csv).";
        if (e.Etat is not null and not ("A" or "B")) yield return $"état « {e.Etat} » (A ou B attendu).";
        if (e.Vitesse is { } v && v is < 0.25 or > 4) yield return "vitesse entre 0,25 et 4 attendue.";

        switch (type)
        {
            case TypeEtapeScene.Pause when e.DureeS is null && e.JusquAS is null:
                yield return "duree_s ou jusqu_a_s attendu."; break;
            case TypeEtapeScene.Legende or TypeEtapeScene.Titre when string.IsNullOrWhiteSpace(e.Texte):
                yield return "texte attendu."; break;
            case TypeEtapeScene.Cadrer or TypeEtapeScene.Allumer when e.Noeuds is not { Count: > 0 }:
                yield return "au moins un nœud attendu."; break;
            case TypeEtapeScene.SurlignerZone when e.Zones is not { Count: > 0 }:
                yield return "au moins une zone attendue."; break;
            case TypeEtapeScene.BasculeEtat or TypeEtapeScene.Matrice when e.Etat is null:
                yield return "état attendu."; break;
            case TypeEtapeScene.Simulation:
                if (string.IsNullOrWhiteSpace(e.Source) || !reference.Noeuds.Contains(e.Source)) yield return $"source « {e.Source} » absente de la topologie.";
                if (string.IsNullOrWhiteSpace(e.Destination)) yield return "destination attendue.";
                if (!Enum.TryParse<Protocole>(e.Protocole, ignoreCase: false, out _)) yield return $"protocole « {e.Protocole} » inconnu du moteur.";
                break;
        }
    }
}
