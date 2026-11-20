using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using P6Explorer.Application.Services;
using P6Explorer.Application.ViewModels;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Exceptions;
using P6Explorer.Infrastructure.Persistance;
using P6Explorer.Tests.Outils;
using P6Explorer.Web.Securite;

namespace P6Explorer.Tests.Infrastructure;

/// <summary>Édition du contenu : historique écrit par l'intercepteur, retour arrière, politique ModifierSonRole.</summary>
public class EditionTests
{
    private static async Task<BaseDeTest> Amorcee()
    {
        var b = new BaseDeTest();
        using var scope = b.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IServiceAmorcage>().AmorcerAsync(Depot.Racine);
        return b;
    }

    private static async Task<int> IdEtape(BaseDeTest b, string code)
    {
        using var scope = b.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<P6DbContext>().Etapes.Where(e => e.Code == code).Select(e => e.Id).SingleAsync();
    }

    [Fact]
    public async Task L_amorcage_n_ecrit_aucune_revision()
    {
        using var b = await Amorcee();
        using var scope = b.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<P6DbContext>().Revisions.CountAsync());
    }

    [Fact]
    public async Task Modifier_une_etape_ecrit_l_historique_et_on_peut_revenir_en_arriere()
    {
        using var b = await Amorcee();
        var id = await IdEtape(b, "r2-e8");
        string ancien;
        using (var scope = b.Services.CreateScope())
        {
            var edition = scope.ServiceProvider.GetRequiredService<IEditionService>();
            var f = (await edition.EtapeAsync(id))!;
            ancien = f.Titre;
            f.Titre = "Nouveau titre de test";
            f.Statut = StatutValidation.AValider;
            await edition.ModifierEtapeAsync(f);
        }
        HistoriqueVM h;
        using (var scope = b.Services.CreateScope())
        {
            var edition = scope.ServiceProvider.GetRequiredService<IEditionService>();
            h = (await edition.HistoriqueAsync(id, 1))!;
            var lot = Assert.Single(h.Lots.Elements);
            Assert.Contains(lot.Champs, c => c.Champ == "Titre" && c.Avant == ancien && c.Apres == "Nouveau titre de test");
            Assert.Contains(lot.Champs, c => c.Champ == "Statut");
            Assert.Contains("Modification annulée", await edition.AnnulerAsync(lot.Lot));
        }
        using (var scope = b.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<P6DbContext>();
            var e = await db.Etapes.SingleAsync(x => x.Id == id);
            Assert.Equal(ancien, e.Titre);
            Assert.Equal(StatutValidation.Validee, e.Statut);
            Assert.Equal(2, await db.Revisions.Select(r => r.Lot).Distinct().CountAsync());   // l'annulation est elle-même tracée
        }
    }

    [Fact]
    public async Task Supprimer_une_commande_puis_annuler_la_recree_avec_ses_effets()
    {
        using var b = await Amorcee();
        int commandeId, nbEffets, etapeId;
        string texte;
        using (var scope = b.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<P6DbContext>();
            var c = await db.Commandes.Include(x => x.Effets).Where(x => x.Code == "r1-e5-c10").SingleAsync();
            (commandeId, nbEffets, texte, etapeId) = (c.Id, c.Effets.Count, c.Texte, c.EtapeId);
            await scope.ServiceProvider.GetRequiredService<IEditionService>().SupprimerCommandeAsync(commandeId);
        }
        using (var scope = b.Services.CreateScope())
        {
            var edition = scope.ServiceProvider.GetRequiredService<IEditionService>();
            var lot = (await edition.HistoriqueAsync(etapeId, 1))!.Lots.Elements.Single();
            Assert.Contains(lot.Champs, c => c.Champ == IntercepteurHorodatage.Suppression);
            await edition.AnnulerAsync(lot.Lot);
        }
        using (var scope = b.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<P6DbContext>();
            var c = await db.Commandes.Include(x => x.Effets).SingleAsync(x => x.Code == "r1-e5-c10");
            Assert.NotEqual(commandeId, c.Id);                  // recréée avec un nouvel identifiant
            Assert.Equal(texte, c.Texte);
            Assert.Equal(nbEffets, c.Effets.Count);
        }
    }

    [Fact]
    public async Task Un_effet_se_modifie_en_JSON_et_un_JSON_invalide_est_refuse()
    {
        using var b = await Amorcee();
        using var scope = b.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<P6DbContext>();
        var edition = scope.ServiceProvider.GetRequiredService<IEditionService>();
        var c = await db.Commandes.AsNoTracking().FirstAsync(x => x.Code == "r1-e5-c10");
        await edition.EnregistrerEffetAsync(new EffetFormVM
        {
            CommandeId = c.Id, Json = """{ "type": "paquet", "de": "kali", "vers": "postgres", "proto": "PostgreSql", "port": 5432, "adresseVisee": "10.10.0.70" }"""
        });
        db.ChangeTracker.Clear();
        var paquet = (await db.Effets.Where(e => e.CommandeId == c.Id).ToListAsync()).OfType<Paquet>().Single(p => p.AdresseVisee == "10.10.0.70");
        Assert.False(paquet.Automatique);
        await Assert.ThrowsAsync<ExceptionMetier>(() => edition.EnregistrerEffetAsync(new EffetFormVM { CommandeId = c.Id, Json = "{ \"de\": 3 }" }));
    }

    [Theory]
    [InlineData("2", null, 2, true)]
    [InlineData("2", null, 1, false)]
    [InlineData("5", null, 4, false)]
    [InlineData(null, "Admin", 4, true)]
    [InlineData(null, null, 1, false)]
    public async Task Politique_ModifierSonRole(string? numeroRole, string? roleIdentity, int roleEtape, bool attendu)
    {
        var services = new ServiceCollection().AddLogging()
            .AddAuthorization(o => o.AddPolicy(Politiques.ModifierSonRole, p => p.RequireAuthenticatedUser().AddRequirements(new ExigenceModifierSonRole())))
            .AddSingleton<IAuthorizationHandler, GestionnaireModifierSonRole>()
            .BuildServiceProvider();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "membre") };
        if (numeroRole is not null) claims.Add(new Claim(Politiques.TypeNumeroRole, numeroRole));
        if (roleIdentity is not null) claims.Add(new Claim(ClaimTypes.Role, roleIdentity));
        var utilisateur = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var r = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(utilisateur, roleEtape, Politiques.ModifierSonRole);
        Assert.Equal(attendu, r.Succeeded);
    }
}
