using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using P6Explorer.Infrastructure.Persistance;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.Web;

/// <summary>L'application complète (MVC, Razor, amorçage) sur SQLite en mémoire au lieu de LocalDB.</summary>
public sealed class FabriqueP6 : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connexion = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connexion.Open();
        builder.UseEnvironment("Tests");
        builder.UseSetting("Amorcage:AppliquerMigrations", "false");
        builder.UseSetting("Amorcage:DossierDonnees", Depot.Chemin("donnees"));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<P6DbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<P6DbContext>((sp, o) => o.UseSqlite(_connexion).AddInterceptors(sp.GetRequiredService<IntercepteurHorodatage>()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connexion.Dispose();
    }
}

public class AccueilTests(FabriqueP6 fabrique) : IClassFixture<FabriqueP6>
{
    [Fact]
    public async Task L_accueil_affiche_le_schema_et_les_six_roles()
    {
        var reponse = await fabrique.CreateClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var html = await reponse.Content.ReadAsStringAsync();
        Assert.Contains("<svg class=\"schema-svg\"", html);
        Assert.Contains("data-noeud=\"opnsense\"", html);
        Assert.Contains("data-lien=\"opnsense-nat\"", html);
        Assert.Contains("class=\"pastille-zone z-dmz\"", html);     // Tag Helper <zone>
        Assert.Equal(6, html.Split("class=\"carte-role\"").Length - 1);
        Assert.DoesNotContain("100.125.208.28", html);               // Q11 : réservé aux membres connectés
    }

    [Fact]
    public async Task L_api_de_sante_repond()
    {
        var reponse = await fabrique.CreateClient().GetAsync("/api/sante");
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        using var json = JsonDocument.Parse(await reponse.Content.ReadAsStringAsync());
        Assert.Equal("ok", json.RootElement.GetProperty("base").GetString());
        Assert.Equal(47, json.RootElement.GetProperty("etapes").GetInt32());
    }

    [Fact]
    public async Task Le_theme_memorise_est_applique_par_le_serveur()
    {
        var client = fabrique.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "theme=light");
        var html = await client.GetStringAsync("/");
        Assert.Contains("<html lang=\"fr\" data-theme=\"light\">", html);
    }
}
