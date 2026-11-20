using P6Explorer.Domain.Enumerations;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.MoteurReseau;

/// <summary>
/// Test d'acceptation du moteur réseau : il doit donner le même résultat que les scripts réellement lancés
/// sur la plateforme (plateforme/labo/). Une ligne par test, dans l'ordre des scripts.
/// Sens de « bloque » dans ces deux scripts : tout échec de la commande (paquet jeté, RST, pas de route).
/// </summary>
public class TestsLaboratoireTests
{
    /// <summary>plateforme/labo/docker/p6-etat-a/tests.sh : 12 tests depuis les conteneurs (docker exec), état A, 12/12 le 17 nov.</summary>
    [Theory]
    // --- Services de base ---
    [InlineData(1, "api", "10.10.0.70", Protocole.PostgreSql, 5432, true, "FA3")]
    [InlineData(2, "client-ext", "203.0.113.1", Protocole.Https, 443, true, "FA1")]
    [InlineData(3, "client-ext", "203.0.113.1", Protocole.Http, 80, true, "FA1")]
    [InlineData(4, "client-ext", "203.0.113.1", Protocole.Dns, 53, true, "FA2")]
    [InlineData(5, "client-01", "10.20.0.10", Protocole.Https, 443, true, "FA4")]
    [InlineData(6, "client-01", "10.20.0.40", Protocole.Dns, 53, true, "FA4")]
    // --- Faiblesses attendues de l'état A ---
    [InlineData(7, "dmz-pivot", "10.10.0.70", Protocole.Tcp, 5432, true, "FA3")]
    [InlineData(8, "client-03", "10.10.0.70", Protocole.Tcp, 5432, true, null)]      // même L2 : pf ne voit rien
    [InlineData(9, "client-03", "10.10.0.1", Protocole.Tcp, 443, true, "FA4")]
    // --- Doit être bloqué (FA5) ---
    [InlineData(10, "client-ext", "10.10.0.70", Protocole.Tcp, 5432, false, "FA5")]
    [InlineData(11, "dmz-pivot", "10.10.0.10", Protocole.Tcp, 22, false, "FA5")]
    [InlineData(12, "client-ext", "203.0.113.1", Protocole.Tcp, 22, false, "FA5")]
    public void Tests_sh(int numero, string source, string destination, Protocole protocole, int port, bool passe, string? regle)
    {
        var r = Labo.Simuler("A", source, destination, protocole, port);
        Assert.True(r.Aboutit == passe, $"tests.sh n°{numero} : attendu {(passe ? "passe" : "bloque")}, obtenu {Labo.Decrire(r)}");
        Assert.Equal(regle, r.Regle);
    }

    /// <summary>plateforme/labo/kali/tests-cloisonnement.sh : 19 tests depuis Kali, état A.</summary>
    [Theory]
    // --- 1. Confinement : rien ne sort du labo (ENF1) ---
    [InlineData(1, "192.168.1.1", Protocole.Icmp, null, false, "FA5")]
    [InlineData(2, "8.8.8.8", Protocole.Icmp, null, false, "FA5")]
    [InlineData(3, "8.8.8.8", Protocole.Dns, 53, false, "FA5")]
    [InlineData(4, "151.101.2.132", Protocole.Http, 80, false, "FA5")]
    [InlineData(5, "10.99.0.2", Protocole.Icmp, null, false, "P6 - bloquer le labo vers Windows")]
    [InlineData(6, "10.99.0.2", Protocole.Smb, 445, false, "P6 - bloquer le labo vers Windows")]
    [InlineData(7, "10.99.0.2", Protocole.Rdp, 3389, false, "P6 - bloquer le labo vers Windows")]
    // --- 2. MGMT : Kali ne doit administrer personne ---
    [InlineData(8, "10.99.0.1", Protocole.Tcp, 443, false, "FA5")]
    [InlineData(9, "10.99.0.1", Protocole.Tcp, 22, false, "FA5")]
    [InlineData(10, "10.99.0.20", Protocole.Tcp, 22, false, "p6_ssh")]
    [InlineData(11, "10.99.0.10", Protocole.Tcp, 22, false, "p6_ssh")]
    // --- 3. Flux de l'état A vus depuis le WAN ---
    [InlineData(12, "203.0.113.1", Protocole.Https, 443, true, "FA1")]
    [InlineData(13, "203.0.113.1", Protocole.Http, 80, true, "FA1")]
    [InlineData(14, "203.0.113.1", Protocole.Dns, 53, true, "FA2")]
    [InlineData(15, "203.0.113.1", Protocole.Tcp, 22, false, "FA5")]
    [InlineData(16, "203.0.113.1", Protocole.Tcp, 5432, false, "FA5")]
    [InlineData(17, "10.20.0.10", Protocole.Tcp, 80, false, "FA5")]       // sans NAT : pas de règle FA1 de filtrage (E36)
    [InlineData(18, "10.10.0.70", Protocole.Tcp, 5432, false, "FA5")]
    [InlineData(19, "203.0.113.1", Protocole.Tcp, 5201, false, "FA5")]    // FA6 désactivée
    public void Tests_cloisonnement_sh(int numero, string destination, Protocole protocole, int? port, bool passe, string regle)
    {
        var r = Labo.Simuler("A", "kali", destination, protocole, port);
        Assert.True(r.Aboutit == passe, $"tests-cloisonnement.sh n°{numero} : attendu {(passe ? "passe" : "bloque")}, obtenu {Labo.Decrire(r)}");
        Assert.Equal(regle, r.Regle);
    }
}
