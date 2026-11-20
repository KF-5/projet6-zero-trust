using P6Explorer.Domain.Enumerations;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.MoteurReseau;

/// <summary>
/// Rejoue tests.csv (produit par le c2.py du guide, rôle 2 étape 6) comme le ferait test_flux.sh (rôle 2 étape 7).
/// Sens de test_flux : en TCP, un RST du service compte « pass » (le pare-feu a laissé passer) ;
/// en UDP 53, dig doit obtenir une réponse.
/// </summary>
public class TestFluxTests
{
    private static readonly string Dossier = Depot.Chemin("app", "P6Explorer.Tests", "MoteurReseau", "Politique");

    public sealed record LigneTest(string Id, string Source, string Destination, string Proto, int Port, string Attendu, string Flux)
    {
        public override string ToString() => $"{Id} {Source} → {Destination} {Proto}/{Port} {Attendu} ({Flux})";
    }

    public static TheoryData<string, string, string, string, string, int, string, string> Lignes()
    {
        var donnees = new TheoryData<string, string, string, string, string, int, string, string>();
        foreach (var etat in new[] { "A", "B" })
            foreach (var l in Lire(etat))
                donnees.Add(etat, l.Id, l.Source, l.Destination, l.Proto, l.Port, l.Attendu, l.Flux);
        return donnees;
    }

    [Theory]
    [MemberData(nameof(Lignes))]
    public void Conforme_a_test_flux(string etat, string id, string source, string destination, string proto, int port, string attendu, string flux)
    {
        var t = new LigneTest(id, source, destination, proto, port, attendu, flux);
        var protocole = t.Proto == "udp" ? (t.Port == 53 ? Protocole.Dns : Protocole.Udp) : Protocole.Tcp;
        var r = Labo.Simuler(etat, Conteneur(t.Source), t.Destination, protocole, t.Port);
        var obtenu = (t.Proto == "udp" && t.Port == 53 ? r.Aboutit : r.FranchitLesFiltres) ? "pass" : "block";
        Assert.True(obtenu == t.Attendu, $"État {etat}, {t} : obtenu {obtenu}. {Labo.Decrire(r)}");
    }

    [Theory]
    [InlineData("A", 8)]
    [InlineData("B", 42)]
    public void Nombre_de_tests_annonce_par_le_guide(string etat, int nombre) => Assert.Equal(nombre, Lire(etat).Count);

    /// <summary>Un test « block » sur F99 / FA5 doit être jeté par une règle d'OPNsense, et laisser une ligne filterlog.</summary>
    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    public void Les_refus_par_defaut_sont_journalises(string etat)
    {
        foreach (var t in Lire(etat).Where(t => t.Flux is "F99" or "FA5"))
        {
            var r = Labo.Simuler(etat, Conteneur(t.Source), t.Destination, t.Proto == "udp" ? Protocole.Dns : Protocole.Tcp, t.Port);
            Assert.True(r.Verdict == Verdict.Bloque && r.Filterlog, $"État {etat}, {t} : {Labo.Decrire(r)}");
        }
    }

    private static List<LigneTest> Lire(string etat)
        => File.ReadAllLines(Path.Combine(Dossier, $"tests-{etat}.csv")).Skip(1).Where(l => l.Length > 0)
            .Select(l => l.Split(','))
            .Select(c => new LigneTest(c[0], c[1], c[2], c[3], int.Parse(c[4]), c[5], c[6]))
            .ToList();

    /// <summary>hosts.csv : nom d'hôte de la politique → nom du conteneur (= identifiant du nœud ; postgresql → postgres).</summary>
    private static string Conteneur(string hote)
        => Hotes.Value.TryGetValue(hote, out var c) && c != "-" ? c : hote;

    private static readonly Lazy<Dictionary<string, string>> Hotes = new(() =>
        File.ReadAllLines(Path.Combine(Dossier, "hosts.csv")).Skip(1).Where(l => l.Length > 0)
            .Select(l => l.Split(',')).ToDictionary(c => c[0], c => c[4]));
}
