using Microsoft.Extensions.DependencyInjection;
using P6Explorer.Application.Services;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Moteur;
using P6Explorer.Domain.Valeurs;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.Donnees;

/// <summary>Jalon 6 : scripts dépliés, bascule A → B, état B dessiné, aucune commande sans effet précis.</summary>
public class RolesDeplieTests
{
    private static Commande Commande(string code)
        => Depot.LireRoles().SelectMany(f => f.Role.Etapes).SelectMany(e => e.Commandes).Single(c => c.Code == code);

    [Theory]
    [InlineData("r1-e4-c09", 12)]        // tests.sh
    [InlineData("r1-e5-c10", 19)]        // tests-cloisonnement.sh
    [InlineData("r2-e7-c05", 8)]         // test_flux.sh, état A
    [InlineData("r2-e9-c03", 42)]        // test_flux.sh, état B
    public void Les_suites_de_tests_sont_depliees_une_sous_commande_par_test(string code, int nombre)
    {
        var c = Commande(code);
        Assert.Equal(nombre, c.SousCommandes.Count);
        Assert.All(c.SousCommandes, s => Assert.Contains(s.Effets, e => e is Paquet));
        Assert.All(c.SousCommandes, s => Assert.StartsWith(code + "-s", s.Code));
    }

    [Fact]
    public void Les_sous_commandes_de_tests_sh_retrouvent_12_sur_12_avec_le_moteur()
    {
        var sim = Labo.Etat("A");
        var attendus = new[] { true, true, true, true, true, true, true, true, true, false, false, false };
        var c = Commande("r1-e4-c09");
        for (var i = 0; i < 12; i++)
        {
            var p = c.SousCommandes[i].Effets.OfType<Paquet>().First();
            var r = sim.Simuler(new DemandeSimulation(p.De, p.AdresseVisee!, p.Proto, p.Port));
            Assert.True(r.Aboutit == attendus[i], $"{c.SousCommandes[i].Code} : {Labo.Decrire(r)}");
        }
    }

    [Fact]
    public void Un_script_lance_par_ssh_se_deplie_sur_la_machine_visee()
        => Assert.All(Commande("r1-e4-c08").SousCommandes, s => Assert.Equal(NoeudId.Kali, s.Ou));   // setup-kali.sh

    [Theory]
    [InlineData(2, 8)]
    [InlineData(4, 8)]
    public void Les_etapes_de_bascule_font_passer_le_schema_a_l_etat_B(int role, int etape)
    {
        var e = Depot.LireRole(role).Role.Etapes.Single(x => x.Numero == etape);
        Assert.Contains(e.Commandes.SelectMany(c => c.Effets), f => f is ChangementEtat { Etat: "B" });
    }

    [Fact]
    public void Plus_aucune_commande_n_a_un_effet_generique()
    {
        var md = File.ReadAllText(Depot.Chemin("docs", "a-completer.md"));
        Assert.Contains("## 1. Effet générique (0 commandes)", md);
        Assert.Contains("Scripts restant à déplier (0)", md);
    }

    [Fact]
    public async Task Le_schema_B_a_six_segments_et_un_cable_par_conteneur()
    {
        using var b = new BaseDeTest();
        using var scope = b.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IServiceAmorcage>().AmorcerAsync(Depot.Racine);
        var schema = await scope.ServiceProvider.GetRequiredService<ITopologieService>().ChargerSchemaAsync("B", false);
        Assert.Equal(["dmz-app", "dmz-dns", "dmz-web", "lan-admin", "lan-db", "lan-users"],
            schema.Zones.Where(z => z.Zone is "dmz" or "lan").Select(z => z.Code).Order());
        Assert.Equal(8, schema.Liens.Count(l => l.Id.Contains("-vlan") && !l.Id.EndsWith("vlan10") && !l.Id.EndsWith("vlan20")));
        Assert.DoesNotContain(schema.Liens, l => l.Id.EndsWith("-vlan10") || l.Id.EndsWith("-vlan20"));
        Assert.Contains(schema.Noeuds, n => n.Id == "bind9" && n.Adresses.Contains("eth0 10.20.0.40"));
        Assert.Contains(schema.Noeuds, n => n.Id == "opnsense" && n.Adresses.Contains("vlan0.22 10.20.0.33"));
    }

    [Fact]
    public void Les_schemas_du_guide_sont_rendus_pour_les_etapes_qui_en_citent_un()
    {
        var chemin = Depot.Chemin("donnees", "schemas", "schemas.json");
        Assert.True(File.Exists(chemin), "Lancer : node outils/schemas-vers-svg.mjs");
        var json = File.ReadAllText(chemin);
        foreach (var nom in new[] { "netmap-position", "nat-steps", "state-table", "macvlan", "mgmt-bypass" })
            Assert.Contains($"\"{nom}\"", json);
        Assert.Contains("<svg", json);
        Assert.DoesNotContain("<script", json);
    }
}
