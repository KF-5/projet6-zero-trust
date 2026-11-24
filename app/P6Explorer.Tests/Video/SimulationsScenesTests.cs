using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using P6Explorer.Domain.Video;
using P6Explorer.Tests.Outils;
using P6Explorer.Tests.Web;

namespace P6Explorer.Tests.Video;

/// <summary>Chaque étape « simulation » des scènes est rejouée par le moteur (POST /api/simulation), comme à l'image.</summary>
public class SimulationsScenesTests(FabriqueP6 fabrique) : IClassFixture<FabriqueP6>
{
    public static TheoryData<string, string, string, string, int?, string> Simulations()
    {
        var f = JsonSerializer.Deserialize<FichierScenes>(File.ReadAllText(Depot.Chemin("donnees", "video", "scenes.json")))!;
        var donnees = new TheoryData<string, string, string, string, int?, string>();
        foreach (var s in f.Scenes)
            foreach (var e in s.Etapes.Where(e => e.TypeConnu == TypeEtapeScene.Simulation))
                donnees.Add(s.Id, e.Source!, e.Destination!, e.Protocole!, e.Port, e.Etat ?? s.Etat);
        return donnees;
    }

    private async Task<JsonElement> SimulerAsync(string source, string destination, string protocole, int? port, string etat)
    {
        var reponse = await fabrique.CreateClient().PostAsJsonAsync("/api/simulation", new { source, destination, protocole, port, etat });
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    [Theory]
    [MemberData(nameof(Simulations))]
    public async Task Le_moteur_rend_un_verdict(string scene, string source, string destination, string protocole, int? port, string etat)
    {
        var r = await SimulerAsync(source, destination, protocole, port, etat);
        Assert.True(r.GetProperty("calcule").GetBoolean(), $"{scene} : {source} → {destination} hors du laboratoire simulé.");
        Assert.False(string.IsNullOrEmpty(r.GetProperty("verdict").GetString()), scene);
    }

    [Fact]
    public async Task P5_le_poste_compromis_atteint_la_base_en_etat_A()
    {
        var r = await SimulerAsync("client-03", "10.10.0.70", "PostgreSql", 5432, "A");
        Assert.Equal("Passe", r.GetProperty("verdict").GetString());
        Assert.False(r.GetProperty("filterlog").GetBoolean());          // même réseau : rien dans les journaux du pare-feu (N = 0)
    }

    [Fact]
    public async Task P6_le_meme_paquet_est_bloque_et_journalise_en_etat_B()
    {
        var r = await SimulerAsync("client-03", "10.10.0.70", "PostgreSql", 5432, "B");
        Assert.Equal("Bloque", r.GetProperty("verdict").GetString());
        Assert.Equal("F99", r.GetProperty("regle").GetString());
        Assert.True(r.GetProperty("filterlog").GetBoolean());
    }
}
