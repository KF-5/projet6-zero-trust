using Microsoft.Extensions.Configuration;
using P6Explorer.Domain.Video;
using P6Explorer.Infrastructure.Fichiers;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.Video;

/// <summary>donnees/video/scenes.json : les 8 scènes de la vidéo se chargent et passent la validation.</summary>
public class ScenesTests
{
    private static DossiersDepot Dossiers(string donnees)
        => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Amorcage:DossierDonnees"] = donnees }).Build());

    private static SourceScenes Source(string donnees)
    {
        var dossiers = Dossiers(donnees);
        return new SourceScenes(dossiers, new SourcePolitique(dossiers));
    }

    private static FichierScenes Charger()
    {
        var lecture = Source(Depot.Chemin("donnees")).Charger();
        Assert.True(lecture.Valide, string.Join(Environment.NewLine, lecture.Erreurs));
        return lecture.Valeur!;
    }

    [Fact]
    public void Le_fichier_contient_les_scenes_P1_a_P8()
    {
        var f = Charger();
        Assert.Equal(["P-1", "P-2", "P-3", "P-4", "P-5", "P-6", "P-7", "P-8"], f.Scenes.Select(s => s.Id).Order());
        Assert.All(f.Scenes, s => Assert.All(s.Etapes, e => Assert.NotNull(e.TypeConnu)));
    }

    [Fact]
    public void Chaque_noeud_cite_existe_dans_la_topologie()
    {
        var noeuds = new[] { "A", "B" }.SelectMany(e => Depot.LireTopologie(e).Noeuds.Select(n => n.Id.Valeur)).ToHashSet();
        var cites = Charger().Scenes.SelectMany(s => s.Etapes)
            .SelectMany(e => (e.Noeuds ?? []).Concat(e.Source is null ? [] : [e.Source])).Distinct().ToList();
        Assert.NotEmpty(cites);
        Assert.All(cites, n => Assert.Contains(n, noeuds));
    }

    [Fact]
    public void Les_durees_des_etapes_tiennent_dans_celle_de_chaque_scene()
    {
        Assert.All(Charger().Scenes, s =>
            Assert.InRange(SourceScenes.DureeTotale(s), s.DureeS - SourceScenes.ToleranceDureeS, s.DureeS + SourceScenes.ToleranceDureeS));
    }

    [Fact]
    public void Un_fichier_incoherent_donne_des_erreurs_lisibles()
    {
        var f = new FichierScenes
        {
            Scenes =
            [
                new SceneVideo { Id = "P-1", Titre = "x", DureeS = 10, Etat = "A", Etapes =
                [
                    new EtapeScene { Type = "inconnu", DureeS = 2 },
                    new EtapeScene { Type = "allumer", Noeuds = ["machine-fantome"], DureeS = 2 },
                ] },
                new SceneVideo { Id = "P-1", Titre = "doublon", DureeS = 5, Etat = "C", Etapes = [new EtapeScene { Type = "pause", DureeS = 5 }] },
            ],
        };
        var reference = new SourceScenes.ReferenceTopologie(["opnsense"], new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DMZ" });
        var erreurs = SourceScenes.Valider(f, reference);
        Assert.Contains(erreurs, e => e.Contains("8 scènes attendues"));
        Assert.Contains(erreurs, e => e.Contains("« P-1 » utilisé 2 fois"));
        Assert.Contains(erreurs, e => e.Contains("type « inconnu » inconnu"));
        Assert.Contains(erreurs, e => e.Contains("« machine-fantome » absent de la topologie"));
        Assert.Contains(erreurs, e => e.Contains("état « C »"));
        Assert.Contains(erreurs, e => e.Contains("les étapes durent 4 s pour une durée annoncée de 10 s"));
    }

    [Fact]
    public void Un_json_illisible_ne_leve_pas_d_exception()
    {
        var dossier = Directory.CreateTempSubdirectory("p6-scenes-");
        try
        {
            Directory.CreateDirectory(Path.Combine(dossier.FullName, "video"));
            File.WriteAllText(Path.Combine(dossier.FullName, "video", "scenes.json"), "{ \"scenes\": [ ");
            var lecture = Source(dossier.FullName).Charger();
            Assert.False(lecture.Valide);
            Assert.Contains(lecture.Erreurs, e => e.StartsWith("scenes.json illisible"));
        }
        finally { dossier.Delete(recursive: true); }
    }
}
