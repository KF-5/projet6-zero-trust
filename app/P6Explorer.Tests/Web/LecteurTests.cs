using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using P6Explorer.Application.Dto;

namespace P6Explorer.Tests.Web;

/// <summary>Pages MVC et Web API du lecteur, sur l'application complète (SQLite en mémoire).</summary>
public class LecteurTests(FabriqueP6 fabrique) : IClassFixture<FabriqueP6>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task La_page_d_une_etape_affiche_le_lecteur_et_la_liste_des_etapes()
    {
        var html = await fabrique.CreateClient().GetStringAsync("/Roles/1/Etapes/5");
        Assert.Contains("id=\"lecteur\"", html);
        Assert.Contains("data-api=\"/api/etapes/", html);
        Assert.Contains("class=\"liste-etapes\"", html);
        Assert.Contains("id=\"terminal-ecran\"", html);
        Assert.Contains("id=\"decodeur\"", html);
        Assert.Contains("/js/lecteur.js", html);                      // RenderSection Scripts
        Assert.DoesNotContain("Modifier l'étape", html);              // visiteur : pas d'édition
    }

    [Fact]
    public async Task Une_etape_inconnue_renvoie_404()
        => Assert.Equal(HttpStatusCode.NotFound, (await fabrique.CreateClient().GetAsync("/Roles/1/Etapes/19")).StatusCode);

    [Fact]
    public async Task Le_role_redirige_vers_sa_premiere_etape()
    {
        var client = fabrique.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var r = await client.GetAsync("/Roles/3");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.EndsWith("/Roles/3/Etapes/0", r.Headers.Location!.ToString());   // le rôle 3 commence à l'étape 0
    }

    [Fact]
    public async Task L_api_rend_les_commandes_avec_le_verdict_du_moteur()
    {
        var client = fabrique.CreateClient();
        var html = await client.GetStringAsync("/Roles/1/Etapes/5");
        var api = System.Text.RegularExpressions.Regex.Match(html, "data-api=\"([^\"]+)\"").Groups[1].Value.Replace("&amp;", "&");
        var commandes = await client.GetFromJsonAsync<List<JsonElement>>(api, Json);
        Assert.NotNull(commandes);
        Assert.NotEmpty(commandes!);
        Assert.All(commandes!, c => Assert.True(c.GetProperty("effets").GetArrayLength() >= 1));   // règle 1 : un effet visible
        var tunnel = commandes!.Single(c => c.GetProperty("code").GetString() == "r1-e5-c09");
        var paquet = tunnel.GetProperty("effets").EnumerateArray().First(e => e.GetProperty("donnees").GetProperty("type").GetString() == "paquet");
        Assert.Equal("Passe", paquet.GetProperty("paquet").GetProperty("verdict").GetString());
        Assert.Equal("Admin du pare-feu", paquet.GetProperty("paquet").GetProperty("regle").GetString());
        Assert.Equal("PS C:\\Labo-P6>", tunnel.GetProperty("invite").GetString());
    }

    [Fact]
    public async Task L_api_d_une_etape_absente_renvoie_404()
        => Assert.Equal(HttpStatusCode.NotFound, (await fabrique.CreateClient().GetAsync("/api/etapes/99999/commandes")).StatusCode);

    [Theory]
    [InlineData("A", "Bloque", "FA5")]
    [InlineData("B", "Bloque", "F99")]
    public async Task Et_si_client_ext_vers_postgres(string etat, string verdict, string regle)
    {
        var r = await fabrique.CreateClient().PostAsJsonAsync("/api/simulation",
            new DemandeSimulationDto { Source = "client-ext", Destination = "10.10.0.70", Protocole = "PostgreSql", Port = 5432, Etat = etat });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var dto = (await r.Content.ReadFromJsonAsync<ResultatPaquetDto>(Json))!;
        Assert.Equal((verdict, regle), (dto.Verdict, dto.Regle));
        Assert.True(dto.Filterlog);
        Assert.NotNull(dto.Trame);
        Assert.Contains(dto.Trame!.Couches, c => c.Code == "tcp");
    }

    [Fact]
    public async Task Et_si_sans_destination_renvoie_400()
    {
        var r = await fabrique.CreateClient().PostAsJsonAsync("/api/simulation", new DemandeSimulationDto { Source = "kali", Destination = "" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Theory]
    [InlineData("/Simulation")]
    [InlineData("/Simulation/Historique")]
    [InlineData("/Simulation/Demonstration")]
    [InlineData("/Glossaire")]
    [InlineData("/Discussions")]
    [InlineData("/Compte/Connexion")]
    public async Task Les_pages_publiques_repondent(string url)
        => Assert.Equal(HttpStatusCode.OK, (await fabrique.CreateClient().GetAsync(url)).StatusCode);

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Etapes/Modifier/1")]
    [InlineData("/Etapes/Historique/1")]
    [InlineData("/Discussions/Nouvelle?commandeId=1")]
    public async Task Les_pages_reservees_demandent_une_connexion(string url)
    {
        var client = fabrique.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var r = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Compte/Connexion", r.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Swagger_documente_l_api()
    {
        var r = await fabrique.CreateClient().GetAsync("/swagger/v1/swagger.json");
        if (r.StatusCode == HttpStatusCode.NotFound) return;               // Swagger n'est servi qu'en développement
        Assert.Contains("/api/etapes/{id}/commandes", await r.Content.ReadAsStringAsync());
    }
}
