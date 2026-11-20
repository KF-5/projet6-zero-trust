using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using P6Explorer.Application.Services;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Moteur;
using P6Explorer.Domain.Valeurs;
using P6Explorer.Infrastructure.Identite;
using P6Explorer.Infrastructure.Persistance;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.Infrastructure;

/// <summary>Le service d'amorçage remplit une base vide depuis donnees/, une seule fois.</summary>
public class AmorcageTests
{
    [Fact]
    public async Task La_base_contient_tout_le_guide_apres_l_amorcage()
    {
        using var b = new BaseDeTest();
        await Amorcer(b);

        using var scope = b.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<P6DbContext>();
        var attendu = Depot.LireRoles().SelectMany(f => f.Role.Etapes).ToList();

        Assert.Equal(6, await db.RolesProjet.CountAsync());
        Assert.Equal(47, await db.Etapes.CountAsync());
        // Commandes du guide + sous-commandes des scripts dépliés (règle 4), toutes rattachées à leur étape.
        Assert.Equal(attendu.Sum(e => e.Commandes.Count + e.Commandes.Sum(c => c.SousCommandes.Count)), await db.Commandes.CountAsync());
        Assert.Equal(attendu.Sum(e => e.Commandes.Sum(c => c.Effets.Count + c.SousCommandes.Sum(x => x.Effets.Count))), await db.Effets.CountAsync());
        Assert.Equal(0, await db.Commandes.CountAsync(c => c.ParentId != null && c.EtapeId != c.Parent!.EtapeId));
        Assert.Equal(attendu.Sum(e => e.Pannes.Count), await db.Pannes.CountAsync());

        // Héritage TPH relu avec son type exact, NAT et chemin compris.
        var paquet = await db.Effets.OfType<Paquet>().FirstAsync(p => p.Port == 22);
        Assert.NotEqual(paquet.De, paquet.Vers);

        // Propriétés shadow remplies par l'intercepteur.
        var etape = await db.Etapes.FirstAsync();
        Assert.NotEqual(default, db.Entry(etape).Property<DateTime>(P6DbContext.DateCreation).CurrentValue);
    }

    [Fact]
    public async Task L_amorcage_ne_double_pas_les_donnees()
    {
        using var b = new BaseDeTest();
        await Amorcer(b);
        await Amorcer(b);
        using var scope = b.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<P6DbContext>();
        Assert.Equal(47, await db.Etapes.CountAsync());
        Assert.Equal(2, await db.EtatsReseau.CountAsync());
        Assert.Equal(Depot.LireTopologie("A").Noeuds.Count, await db.Noeuds.CountAsync());
    }

    [Fact]
    public async Task Le_moteur_reseau_fonctionne_sur_la_topologie_enregistree_en_base()
    {
        using var b = new BaseDeTest();
        await Amorcer(b);
        using var scope = b.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<P6DbContext>();
        var noeuds = await db.Noeuds.Include(n => n.Interfaces).AsNoTracking().ToListAsync();
        var reseaux = await db.Reseaux.AsNoTracking().ToListAsync();
        var regles = await db.Regles.AsNoTracking().ToListAsync();
        var nat = await db.ReglesNat.AsNoTracking().ToListAsync();
        // Cartes B ajoutées aux machines de l'état A, services (colonne JSON), affectations et mode de NAT relus.
        Assert.Equal(2, noeuds.Single(n => n.Id == NoeudId.Nginx).Interfaces.Count);
        Assert.Contains(noeuds.Single(n => n.Id == NoeudId.Bind9).Services, s => s.Proto == "udp" && s.Port == 53);

        var a = new SimulateurPaquet(TopologieSimulee.Construire("A", noeuds, reseaux, regles, nat));
        var bb = new SimulateurPaquet(TopologieSimulee.Construire("B", noeuds, reseaux, regles, nat));
        var s05A = a.Simuler(new DemandeSimulation(NoeudId.Client03, "10.10.0.70", Protocole.PostgreSql));
        var s05B = bb.Simuler(new DemandeSimulation(NoeudId.Client03, "10.10.0.70", Protocole.PostgreSql));
        var web = a.Simuler(new DemandeSimulation(NoeudId.Kali, "203.0.113.1", Protocole.Https));
        Assert.Equal((Verdict.Passe, false), (s05A.Verdict, s05A.Filterlog));
        Assert.Equal((Verdict.Bloque, "F99"), (s05B.Verdict, s05B.Regle));
        Assert.Equal((Verdict.Passe, "FA1"), (web.Verdict, web.Regle));
    }

    [Fact]
    public async Task Les_roles_Identity_et_le_compte_admin_sont_crees()
    {
        using var b = new BaseDeTest(motDePasseAdmin: "Essai-P6-amorcage-1");
        await Amorcer(b);
        using var scope = b.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var comptes = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await roles.RoleExistsAsync(RolesIdentite.Admin));
        Assert.True(await roles.RoleExistsAsync(RolesIdentite.Membre));
        var admin = await comptes.FindByEmailAsync("admin@p6.lab");
        Assert.NotNull(admin);
        Assert.Equal(1, admin.NumeroRole);
        Assert.True(await comptes.IsInRoleAsync(admin, RolesIdentite.Admin));
    }

    [Fact]
    public async Task Sans_mot_de_passe_le_compte_admin_n_est_pas_cree()
    {
        using var b = new BaseDeTest(motDePasseAdmin: null);
        await Amorcer(b);
        using var scope = b.Services.CreateScope();
        var comptes = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await comptes.FindByEmailAsync("admin@p6.lab"));
    }

    [Fact]
    public async Task Les_services_donnent_les_cartes_de_role_et_le_schema()
    {
        using var b = new BaseDeTest();
        await Amorcer(b);
        using var scope = b.Services.CreateScope();
        var cartes = await scope.ServiceProvider.GetRequiredService<IRoleService>().ListerCartesAsync();
        Assert.Equal([1, 2, 3, 4, 5, 6], cartes.Select(c => c.Numero));
        Assert.Equal(5, cartes[0].NbFaites);                    // rôle 1 : étapes 1 à 5 faites
        Assert.Equal(Depot.LireRole(1).Role.Etapes.Sum(e => e.Commandes.Count), cartes[0].NbCommandes);

        var schema = await scope.ServiceProvider.GetRequiredService<ITopologieService>().ChargerSchemaAsync("A", membreConnecte: false);
        Assert.Contains(schema.Noeuds, n => n.Id == "opnsense" && n.Adresses.Contains("em0 203.0.113.1"));
        Assert.Contains(schema.Zones, z => z.Zone == "dmz");
        Assert.DoesNotContain(schema.Noeuds.SelectMany(n => n.Adresses), a => a.Contains("100.125"));   // Q11
    }

    [Fact]
    public void La_configuration_AutoMapper_est_valide()
    {
        using var b = new BaseDeTest();
        b.Services.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
    }

    private static async Task Amorcer(BaseDeTest b)
    {
        using var scope = b.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IServiceAmorcage>().AmorcerAsync(Depot.Racine);
    }
}
