using System.Net;
using System.Text.RegularExpressions;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.Web;

/// <summary>Pages filmées ou projetées : publiques, et sans donnée personnelle ni adresse réservée, même connecté.</summary>
public partial class VideoPagesTests(FabriqueP6 fabrique) : IClassFixture<FabriqueP6>
{
    public static TheoryData<string> Pages() =>
    [
        "/Video", "/Video/P-1", "/Video/P-2", "/Video/P-3", "/Video/P-4", "/Video/P-5", "/Video/P-6", "/Video/P-7", "/Video/P-8",
        "/Politique", "/Politique?etat=B",
    ];

    [GeneratedRegex(@"\b100\.\d{1,3}\.\d{1,3}\.\d{1,3}\b")]
    private static partial Regex AdresseTailscale();

    private static void SansDonneeSensible(string page, string html)
    {
        Assert.False(AdresseTailscale().IsMatch(html), $"{page} : adresse 100.x (Tailscale) à l'image.");
        Assert.DoesNotContain("@", html);                                   // ni adresse e-mail (admin@p6.lab…), ni rien qui y ressemble
        Assert.DoesNotContain(FabriqueConnectee.NomCompte, html);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Visiteur_la_page_repond_sans_donnee_sensible(string page)
    {
        var reponse = await fabrique.CreateClient().GetAsync(page);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        SansDonneeSensible(page, await reponse.Content.ReadAsStringAsync());
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Connecte_la_page_n_affiche_toujours_ni_compte_ni_adresse_reservee(string page)
    {
        var client = fabrique.Connecter().CreateClient();
        var reponse = await client.GetAsync(page);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var html = await reponse.Content.ReadAsStringAsync();
        SansDonneeSensible(page, html);
        Assert.DoesNotContain("Déconnexion", html);
    }

    [Fact]
    public async Task Connecte_les_autres_pages_montrent_bien_le_compte()
    {
        // Garde-fou du test précédent : l'authentification de test fonctionne (le nom apparaît ailleurs).
        var html = await fabrique.Connecter().CreateClient().GetStringAsync("/");
        Assert.Contains(FabriqueConnectee.NomCompte, html);
    }

    [Fact]
    public async Task Le_mode_tournage_n_a_ni_en_tete_ni_connexion()
    {
        var html = await fabrique.CreateClient().GetStringAsync("/Video/P-5");
        Assert.DoesNotContain("class=\"entete\"", html);
        Assert.DoesNotContain("/Compte/Connexion", html);
        Assert.DoesNotContain("class=\"pied\"", html);
        Assert.Contains("data-mode=\"video\"", html);
        Assert.Contains("class=\"coin-scene\"", html);
        Assert.DoesNotContain("class=\"coin-scene\"", await fabrique.CreateClient().GetStringAsync("/Video/P-5?propre=1"));
    }

    [Fact]
    public async Task La_scene_P6_annonce_la_cible_et_une_scene_inconnue_rend_404()
    {
        Assert.Contains("Ce qui suit est la cible", await fabrique.CreateClient().GetStringAsync("/Video/P-6"));
        Assert.Equal(HttpStatusCode.NotFound, (await fabrique.CreateClient().GetAsync("/Video/P-9")).StatusCode);
    }

    [Fact]
    public async Task La_liste_donne_le_statut_de_chaque_scene()
    {
        var html = WebUtility.HtmlDecode(await fabrique.CreateClient().GetStringAsync("/Video"));
        Assert.DoesNotContain("erreurs-scenes", html);                      // scenes.json valide
        Assert.Equal(8, Regex.Matches(html, "Ouvrir en mode tournage").Count);
        Assert.Contains("statut-cible\">cible", html);                      // P-6 tant que l'état B n'est pas construit
        Assert.Contains("statut-prete\">prête", html);
    }
}
