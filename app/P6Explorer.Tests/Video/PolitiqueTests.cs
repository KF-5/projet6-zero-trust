using Microsoft.Extensions.Configuration;
using P6Explorer.Application.Services;
using P6Explorer.Infrastructure.Fichiers;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.Video;

/// <summary>Matrice de flux (P-4, /Politique) : tout vient de donnees/politique/*.csv, rien n'est recalculé.</summary>
public class PolitiqueTests
{
    private static readonly SourcePolitique Source = new(new DossiersDepot(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Amorcage:DossierDonnees"] = Depot.Chemin("donnees") }).Build()));

    private static readonly PolitiqueService Service = new(Source);

    private static int LignesCsv(string fichier) => File.ReadAllLines(Depot.Chemin("donnees", "politique", fichier)).Count(l => l.Trim().Length > 0) - 1;

    [Theory]
    [InlineData("A", "tests-A.csv", 8)]
    [InlineData("B", "tests-B.csv", 42)]
    public void Le_nombre_de_tests_affiches_est_celui_du_fichier(string etat, string fichier, int attendu)
    {
        var m = Service.Matrice(etat);
        Assert.Equal(attendu, LignesCsv(fichier));
        Assert.Equal(attendu, m.NbTests);
        Assert.Equal(m.NbTests, m.NbPass + m.NbBlock);
        Assert.Equal(m.NbTests, m.Flux.Sum(f => f.Tests.Count));       // chaque test est rattaché à un flux
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    public void Chaque_flux_ref_des_tests_existe_dans_flows_csv(string etat)
    {
        var politique = Source.Charger();
        var flux = politique.FluxDe(etat).Select(f => f.Id).ToHashSet();
        Assert.All(politique.Tests(etat), t => Assert.Contains(t.FluxRef, flux));
    }

    [Fact]
    public void Les_lignes_mises_en_avant_dans_la_video_sont_verifiees_par_des_tests()
    {
        var fa3 = Service.Matrice("A").Flux.Single(f => f.Id == "FA3");
        Assert.Equal("Toute la DMZ vers la base", fa3.Justification);
        Assert.Equal(["T005", "T006"], fa3.Tests.Select(t => t.Id));
        var f04 = Service.Matrice("B").Flux.Single(f => f.Id == "F04");
        Assert.Equal("Seul accès autorisé à la base", f04.Justification);
        Assert.NotEmpty(f04.Tests);
    }

    [Fact]
    public void Le_refus_par_defaut_n_est_pas_un_flux_autorise()
    {
        var a = Service.Matrice("A");
        var b = Service.Matrice("B");
        Assert.Equal("FA5", a.IdRefusDefaut);
        Assert.Equal("F99", b.IdRefusDefaut);
        Assert.Equal(5, a.NbFlux);                                      // FA1, FA2, FA3, FA4, FA6
        Assert.Equal(9, b.NbFlux);                                      // F01 à F09
        Assert.DoesNotContain(a.Cases.SelectMany(l => l).SelectMany(c => c), c => c.FluxId == "FA5");
        Assert.Contains("tout", a.Destinations);                        // « any » s'affiche « tout »
    }
}
