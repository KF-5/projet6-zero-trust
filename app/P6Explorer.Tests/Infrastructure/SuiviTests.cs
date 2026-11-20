using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using P6Explorer.Application.Dto;
using P6Explorer.Application.Services;
using P6Explorer.Application.ViewModels;
using P6Explorer.Infrastructure.Identite;
using P6Explorer.Infrastructure.Persistance;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.Infrastructure;

/// <summary>Jalon 7 : quiz et préparation de la soutenance, questions-réponses, simulations « Et si ? », glossaire.</summary>
public class SuiviTests
{
    private static async Task<(BaseDeTest Base, string Membre)> Preparer()
    {
        var b = new BaseDeTest();
        using var scope = b.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IServiceAmorcage>().AmorcerAsync(Depot.Racine);
        var comptes = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var membre = new ApplicationUser { UserName = "role2@p6.lab", Email = "role2@p6.lab", NomAffiche = "Membre rôle 2", NumeroRole = 2 };
        Assert.True((await comptes.CreateAsync(membre, "Essai-Membre-2026")).Succeeded);
        return (b, membre.Id);
    }

    [Fact]
    public async Task Chaque_etape_a_un_quiz_a_valider_tire_du_guide()
    {
        var (b, _) = await Preparer();
        using var _b = b;
        using var scope = b.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<P6DbContext>();
        Assert.Equal(47, await db.Quiz.CountAsync());
        Assert.Equal(0, await db.Quiz.CountAsync(q => q.Questions.Count < 2));
        Assert.All(await db.Questions.Include(q => q.Choix).ToListAsync(), q =>
        {
            Assert.Equal(P6Explorer.Domain.Enumerations.StatutValidation.AValider, q.Statut);
            Assert.Contains(q.Choix, c => c.Correct);
            Assert.True(q.Choix.Count >= 2);
        });
        Assert.Equal(30, await db.Questions.CountAsync(q => q.Source == "jury"));   // 5 questions du jury × 6 rôles
        Assert.Equal(0.8, (await db.Quiz.FirstAsync()).Seuil);
    }

    [Fact]
    public async Task Un_quiz_reussi_est_enregistre_et_apparait_dans_la_preparation()
    {
        var (b, membre) = await Preparer();
        using var _b = b;
        int etapeId;
        using (var scope = b.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<P6DbContext>();
            var quiz = await db.Quiz.Include(q => q.Etape).Include(q => q.Questions).ThenInclude(q => q.Choix).FirstAsync(q => q.Etape!.Code == "r2-e5");
            etapeId = quiz.EtapeId;
            var service = scope.ServiceProvider.GetRequiredService<IQuizService>();
            var justes = quiz.Questions.ToDictionary(q => q.Id, q => q.Choix.Where(c => c.Correct).Select(c => c.Id).ToList());
            var r = await service.CorrigerAsync(quiz.Id, justes, membre);
            Assert.Equal(100, r.Score);
            Assert.True(r.Reussi);
            var faux = quiz.Questions.ToDictionary(q => q.Id, q => q.Choix.Where(c => !c.Correct).Take(1).Select(c => c.Id).ToList());
            Assert.Equal(0, (await service.CorrigerAsync(quiz.Id, faux, membre)).Score);
            Assert.Equal(2, await db.Tentatives.CountAsync());
            Assert.True(await db.Reponses.CountAsync() >= quiz.Questions.Count);    // choix cochés (n-n)
        }
        using (var scope = b.Services.CreateScope())
        {
            var prep = await scope.ServiceProvider.GetRequiredService<IQuizService>().PreparationAsync();
            Assert.Contains("Membre rôle 2 (rôle 2)", prep.Membres);
            var ligne = prep.Lignes.Single(l => l.Etape.StartsWith("2.5 "));
            Assert.Equal(100, ligne.Scores.Single()!.Score);                         // meilleur score (Max)
            Assert.Equal(2, ligne.Scores.Single()!.Tentatives);                     // Count
            Assert.DoesNotContain(ligne.Etape, prep.PersonneNeMaitrise);
            Assert.Equal(46, prep.PersonneNeMaitrise.Count);
            var maitrisees = await scope.ServiceProvider.GetRequiredService<IProgressionService>().EtapesMaitriseesAsync(membre);
            Assert.Equal([etapeId], maitrisees);
        }
    }

    [Fact]
    public async Task Question_sur_une_commande_reponse_et_resolution()
    {
        var (b, membre) = await Preparer();
        using var _b = b;
        using var scope = b.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<P6DbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IDiscussionService>();
        var commande = await db.Commandes.Include(c => c.Etape).FirstAsync(c => c.Code == "r2-e5-c01");
        var id = await service.CreerAsync(new NouveauFilVM { CommandeId = commande.Id, Titre = "Pourquoi Pass et pas Register rule ?", Texte = "Je ne vois pas la différence." }, membre);
        Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<P6Explorer.Domain.Interfaces.IDiscussionRepository>().OuvertsParEtapeAsync(2)).GetValueOrDefault(commande.EtapeId));
        await service.RepondreAsync(new NouveauMessageVM { FilId = id, Texte = "Register rule crée « any → 10.20.0.10 » : voir E36." }, membre);
        await service.ChangerStatutAsync(id, resolu: true);
        var fil = (await service.ChargerAsync(id))!;
        Assert.Equal("Resolu", fil.Statut);
        Assert.Equal(2, fil.Messages.Count);
        Assert.Equal("Membre rôle 2", fil.Auteur);
        Assert.Empty((await service.ListerAsync("Ouverts", 1)).Fils.Elements);
        Assert.Single((await service.ListerAsync("Resolus", 1)).Fils.Elements);
    }

    [Fact]
    public async Task Les_simulations_sont_enregistrees_et_rejouees_dans_l_ordre_choisi()
    {
        var (b, membre) = await Preparer();
        using var _b = b;
        using var scope = b.Services.CreateScope();
        var s = scope.ServiceProvider.GetRequiredService<ISimulationService>();
        var a = await s.EnregistrerAsync(new DemandeSimulationDto { Source = "client-ext", Destination = "10.10.0.70", Protocole = "PostgreSql", Etat = "A" }, "client-ext vers la base (A)", membre);
        var bb = await s.EnregistrerAsync(new DemandeSimulationDto { Source = "client-03", Destination = "10.10.0.70", Protocole = "PostgreSql", Etat = "B" }, "S-05 en B", membre);
        var c = await s.EnregistrerAsync(new DemandeSimulationDto { Source = "kali", Destination = "203.0.113.1", Protocole = "Https", Etat = "A" }, null, membre);
        var h = await s.HistoriqueAsync(1);
        Assert.Equal(3, h.Total);
        Assert.Contains(h.Elements, x => x.Id == a && x.Verdict == "Bloque" && x.Regle == "FA5");
        Assert.Contains(h.Elements, x => x.Id == bb && x.Verdict == "Bloque" && x.Regle == "F99");
        await s.MarquerDemoAsync(a, true);
        await s.MarquerDemoAsync(bb, true);
        await s.MarquerDemoAsync(c, true);
        await s.DeplacerDemoAsync(c, -1);
        Assert.Equal([a, c, bb], (await s.DemoAsync()).Select(x => x.Id));
        await s.MarquerDemoAsync(a, false);
        Assert.Equal([c, bb], (await s.DemoAsync()).Select(x => x.Id));
    }

    [Fact]
    public async Task Le_glossaire_est_relie_aux_etapes_et_annote_les_explications()
    {
        var (b, _) = await Preparer();
        using var _b = b;
        using var scope = b.Services.CreateScope();
        var termes = await scope.ServiceProvider.GetRequiredService<IGlossaireService>().ListerAsync();
        Assert.True(termes.Count >= 40);
        foreach (var t in new[] { "VLAN", "trunk", "NAT", "macvlan", "nftables", "Tailscale", "tunnel", "ProxyJump", "VNC", "IDS", "IPS", "netmap" })
            Assert.Contains(termes, x => x.Terme == t);
        Assert.Contains("r2-e3", termes.Single(t => t.Terme == "VLAN").Etapes);
        var html = TexteRiche.EnHtml("Le **trunk** transporte le VLAN 10 et `ens33.10`.",
            [new P6Explorer.Domain.Commandes.TermeGlossaire { Id = 1, Terme = "VLAN", Definition = "Réseau virtuel." }]);
        Assert.Contains("<strong>trunk</strong>", html);
        Assert.Contains("<code>ens33.10</code>", html);
        Assert.Contains("<dfn class=\"terme\" tabindex=\"0\" data-definition=\"Réseau virtuel.\">VLAN</dfn>", html);
        Assert.DoesNotContain("<script", TexteRiche.EnHtml("<script>alert(1)</script>"));
    }
}
