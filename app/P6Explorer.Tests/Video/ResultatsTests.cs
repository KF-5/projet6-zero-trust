using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using P6Explorer.Infrastructure.Fichiers;
using P6Explorer.Tests.Outils;
using P6Explorer.Tests.Web;

namespace P6Explorer.Tests.Video;

/// <summary>Résultats A contre B (P-7) : illustration tant que synthese.csv manque, vraies valeurs dès qu'il est valide.</summary>
public sealed class ResultatsTests(FabriqueP6 fabrique) : IClassFixture<FabriqueP6>, IDisposable
{
    private readonly DirectoryInfo _resultats = Directory.CreateTempSubdirectory("p6-resultats-");

    public void Dispose() => _resultats.Delete(recursive: true);

    private const string Valide = """
        indicateur,libelle,etat,valeur,unite
        taux_detection,Taux de détection,A,37.5,%
        taux_detection,Taux de détection,B,96.25,%
        latence,Latence ajoutée,A,1.2,ms
        latence,Latence ajoutée,B,3.4,ms
        """;

    private void Ecrire(string contenu)
    {
        Directory.CreateDirectory(Path.Combine(_resultats.FullName, "synthese"));
        File.WriteAllText(Path.Combine(_resultats.FullName, "synthese", "synthese.csv"), contenu);
    }

    private SourceSynthese Source() => new(new DossiersDepot(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Amorcage:DossierDonnees"] = Depot.Chemin("donnees"),
        ["Amorcage:DossierResultats"] = _resultats.FullName,
    }).Build()));

    [Fact]
    public void Sans_fichier_les_valeurs_d_exemple_sont_marquees_illustration()
    {
        var s = Source().Charger();
        Assert.True(s.Illustration);
        Assert.Equal("donnees/video/resultats-exemple.csv", s.Source);
        Assert.NotEmpty(s.Lignes);
        Assert.Contains(s.Remarques, r => r.Contains("absent"));
    }

    [Fact]
    public void Un_fichier_valide_est_lu_tel_quel_quel_que_soit_le_nombre_d_indicateurs()
    {
        Ecrire(Valide);
        var s = Source().Charger();
        Assert.False(s.Illustration);
        Assert.Equal(4, s.Lignes.Count);
        Assert.Equal(96.25, s.Lignes.Single(l => l.Indicateur == "taux_detection" && l.Etat == "B").Valeur);
    }

    [Theory]
    [InlineData("indicateur,libelle,etat,valeur\nx,X,A,1")]                                  // en-tête incomplet
    [InlineData("indicateur,libelle,etat,valeur,unite\nx,X,A,1,%")]                         // ligne B manquante
    [InlineData("indicateur,libelle,etat,valeur,unite\nx,X,A,beaucoup,%\nx,X,B,2,%")]       // valeur illisible
    public void Un_fichier_invalide_repasse_en_illustration(string contenu)
    {
        Ecrire(contenu);
        var s = Source().Charger();
        Assert.True(s.Illustration);
        Assert.Contains(s.Remarques, r => r.StartsWith("synthese.csv"));
    }

    [Fact]
    public async Task La_page_P7_porte_le_bandeau_sans_fichier_et_les_vraies_valeurs_avec()
    {
        var sansFichier = fabrique.WithWebHostBuilder(b => b.UseSetting("Amorcage:DossierResultats", _resultats.FullName));
        var html = await sansFichier.CreateClient().GetStringAsync("/Video/P-7");
        Assert.Contains("ILLUSTRATION — données fictives", html);

        Ecrire(Valide);
        var avecFichier = fabrique.WithWebHostBuilder(b => b.UseSetting("Amorcage:DossierResultats", _resultats.FullName));
        html = System.Net.WebUtility.HtmlDecode(await avecFichier.CreateClient().GetStringAsync("/Video/P-7"));
        Assert.DoesNotContain("ILLUSTRATION", html);
        Assert.Contains("96,25 %", html);
        Assert.Contains("Latence ajoutée", html);
        Assert.Contains("Source : resultats/synthese/synthese.csv", html);
    }
}
