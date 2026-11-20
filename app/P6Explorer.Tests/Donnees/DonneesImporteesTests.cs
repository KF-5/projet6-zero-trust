using System.Text.RegularExpressions;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.Donnees;

/// <summary>
/// Critères d'acceptation sur les données produites par l'importeur (prompt, section 9) :
/// les 47 étapes, chaque commande avec un texte, une explication et au moins un effet visible, aucun secret.
/// </summary>
public partial class DonneesImporteesTests
{
    [Fact]
    public void Les_47_etapes_sont_presentes()
    {
        var etapes = Depot.LireRoles().SelectMany(f => f.Role.Etapes).ToList();
        Assert.Equal(47, etapes.Count);
        Assert.Equal(etapes.Count, etapes.Select(e => e.Code).Distinct().Count());
    }

    [Theory]
    [InlineData(1, 9, 1)]
    [InlineData(2, 9, 1)]
    [InlineData(3, 8, 0)]   // le rôle 3 commence par une étape 0 (préparation)
    [InlineData(4, 8, 1)]
    [InlineData(5, 6, 1)]
    [InlineData(6, 7, 1)]
    public void Chaque_role_a_ses_etapes_dans_l_ordre(int role, int nombre, int premiere)
    {
        var etapes = Depot.LireRole(role).Role.Etapes;
        Assert.Equal(nombre, etapes.Count);
        Assert.Equal(Enumerable.Range(premiere, nombre), etapes.Select(e => e.Numero));
    }

    [Fact]
    public void Chaque_commande_a_un_texte_une_explication_et_au_moins_un_effet()
    {
        var commandes = Toutes().ToList();
        Assert.True(commandes.Count > 300, $"seulement {commandes.Count} commandes importées");
        Assert.All(commandes, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Texte), $"{c.Code} : texte vide");
            Assert.False(string.IsNullOrWhiteSpace(c.Explication), $"{c.Code} : explication vide");
            Assert.NotEmpty(c.Effets);
            Assert.Equal(Enumerable.Range(1, c.Effets.Count), c.Effets.Select(e => e.Ordre));
        });
        Assert.Equal(commandes.Count, commandes.Select(c => c.Code).Distinct().Count());
    }

    [Fact]
    public void Les_paquets_attendent_le_moteur_reseau()
    {
        var paquets = Toutes().SelectMany(c => c.Effets).OfType<Paquet>().ToList();
        Assert.NotEmpty(paquets);
        Assert.All(paquets, p => Assert.Equal(Verdict.ACalculer, p.Verdict));
    }

    [Fact]
    public void Aucun_secret_dans_les_donnees()
    {
        foreach (var n in Enumerable.Range(1, 6))
        {
            var json = File.ReadAllText(Depot.Chemin("donnees", "commandes", $"role-{n}.json"));
            Assert.DoesNotMatch(CleSsh(), json);
            Assert.DoesNotMatch(ClePrivee(), json);
            Assert.DoesNotMatch(MotDePasse(), json);
            Assert.DoesNotMatch(CurlUtilisateur(), json);
        }
    }

    [Fact]
    public void Les_etapes_faites_montrent_les_commandes_reellement_utilisees()
    {
        // Décision du 20 nov. (Q4) : compte karim + sudo, jamais root@ ; noms réels de la plateforme.
        var faites = Depot.LireRoles().SelectMany(f => f.Role.Etapes).Where(e => e.Fait).ToList();
        Assert.NotEmpty(faites);
        var executables = faites.SelectMany(e => e.Commandes).Where(c => c.Shell is not (Shell.Editeur or Shell.Document)).ToList();
        Assert.All(executables, c =>
        {
            Assert.DoesNotContain("root@", c.Texte);
            Assert.DoesNotContain("labo.test", c.Texte);
            Assert.DoesNotContain("/var/log/remote/", c.Texte);
            Assert.DoesNotContain("--network lan ", c.Texte);
        });
        // Le texte d'origine du guide reste consultable.
        Assert.Contains(executables, c => c.TexteGuide is not null && c.TexteGuide.Contains("root@10.99.0.1"));
    }

    [Fact]
    public void Les_etapes_a_venir_gardent_le_texte_du_guide_et_signalent_l_ecart()
    {
        var commande = Toutes().First(c => c.Code.StartsWith("r2-e7") && c.Texte.Contains("ssh root@10.99.0.20"));
        Assert.Null(commande.TexteGuide);
        Assert.Contains("E40", commande.Explication);
    }

    private static IEnumerable<Commande> Toutes() => Depot.LireRoles().SelectMany(f => f.Role.Etapes).SelectMany(e => e.Commandes);

    [GeneratedRegex(@"ssh-(ed25519|rsa)\s+AAAA[A-Za-z0-9+/]{8,}")] private static partial Regex CleSsh();
    [GeneratedRegex(@"PRIVATE KEY-----\s*[A-Za-z0-9+/=]{20}")] private static partial Regex ClePrivee();
    [GeneratedRegex(@"(PASSWORD|_PWD)\s*[=:]\s*(?![$•\s""']|\$\{)[A-Za-z0-9]")] private static partial Regex MotDePasse();
    [GeneratedRegex(@"-u\s+[A-Za-z0-9_.-]+:[A-Za-z0-9_.-]+")] private static partial Regex CurlUtilisateur();
}
