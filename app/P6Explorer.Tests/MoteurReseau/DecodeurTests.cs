using System.Buffers.Binary;
using System.Text;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Moteur;
using P6Explorer.Domain.Valeurs;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.MoteurReseau;

/// <summary>DecodeurPaquet : octets réellement construits, sommes de contrôle vérifiées comme le ferait le récepteur.</summary>
public class DecodeurTests
{
    private static Trame Decoder(string etat, string source, string destination, Protocole p, int? port = null)
    {
        var sim = Labo.Etat(etat);
        var demande = new DemandeSimulation(new NoeudId(source), destination, p, port);
        return DecodeurPaquet.Construire(sim.Topologie, demande, sim.Simuler(demande))!;
    }

    private static int DebutIp(Trame t) => t.Couches.Single(c => c.Code == "ip").Debut;

    [Fact]
    public void Une_trame_du_LAN_porte_l_etiquette_VLAN_10_sur_le_trunk()
    {
        var t = Decoder("A", "client-01", "10.20.0.10", Protocole.Https);
        Assert.Equal(0x8100, BinaryPrimitives.ReadUInt16BigEndian(t.Octets.AsSpan(12)));
        Assert.Equal(10, BinaryPrimitives.ReadUInt16BigEndian(t.Octets.AsSpan(14)) & 0x0fff);
        Assert.Equal(0x0800, BinaryPrimitives.ReadUInt16BigEndian(t.Octets.AsSpan(16)));
        Assert.Equal(18, DebutIp(t));
        Assert.Equal(["eth", "vlan", "vlan", "ip", "tcp", "app"], t.Couches.Select(c => c.Code));
    }

    [Fact]
    public void Une_trame_du_WAN_n_a_pas_d_etiquette_VLAN()
    {
        var t = Decoder("A", "kali", "203.0.113.1", Protocole.Https);
        Assert.Equal(0x0800, BinaryPrimitives.ReadUInt16BigEndian(t.Octets.AsSpan(12)));
        Assert.DoesNotContain(t.Couches, c => c.Code == "vlan");
    }

    [Theory]
    [InlineData("kali", "203.0.113.1", Protocole.Https, null)]
    [InlineData("client-ext", "10.10.0.70", Protocole.PostgreSql, null)]
    [InlineData("client-01", "10.20.0.40", Protocole.Dns, null)]
    [InlineData("opnsense", "10.99.0.10", Protocole.Syslog, null)]
    [InlineData("docker", "10.99.0.10", Protocole.Ntp, null)]
    [InlineData("kali", "203.0.113.1", Protocole.Icmp, null)]
    [InlineData("client-ext", "203.0.113.1", Protocole.Http, null)]
    public void L_en_tete_IP_et_le_segment_ont_des_sommes_de_controle_justes(string source, string destination, Protocole p, int? port)
    {
        var t = Decoder("A", source, destination, p, port);
        var ip = DebutIp(t);
        var entete = t.Octets.AsSpan(ip, 20);
        Assert.Equal(0, DecodeurPaquet.SommeControle(entete));                     // le récepteur retrouve 0
        var longueur = BinaryPrimitives.ReadUInt16BigEndian(entete[2..]);
        Assert.Equal(t.Octets.Length - ip, longueur);
        var segment = t.Octets.AsSpan(ip + 20).ToArray();
        var proto = entete[9];
        var src = new Ipv4(BinaryPrimitives.ReadUInt32BigEndian(entete[12..]));
        var dst = new Ipv4(BinaryPrimitives.ReadUInt32BigEndian(entete[16..]));
        Assert.Equal(destination, dst.ToString());
        var verification = proto == 1 ? DecodeurPaquet.SommeControle(segment)
            : DecodeurPaquet.SommeControle(DecodeurPaquet.PseudoEnTete(src, dst, proto, segment.Length).Concat(segment).ToArray());
        Assert.Equal(0, verification);
    }

    [Fact]
    public void Le_SYN_vers_postgres_vise_le_port_5432_sans_donnees()
    {
        var t = Decoder("A", "client-ext", "10.10.0.70", Protocole.PostgreSql);
        var tcp = t.Couches.Single(c => c.Code == "tcp");
        Assert.Equal(5432, BinaryPrimitives.ReadUInt16BigEndian(t.Octets.AsSpan(tcp.Debut + 2)));
        Assert.Equal(0x02, t.Octets[tcp.Debut + 13]);                              // SYN seul
        Assert.Equal(tcp.Debut + 20, t.Octets.Length);
        Assert.Contains(tcp.Champs, c => c.Nom == "Drapeaux" && c.Valeur == "SYN");
    }

    [Fact]
    public void La_question_DNS_demande_www_p6_lab()
    {
        var t = Decoder("A", "client-ext", "203.0.113.1", Protocole.Dns);
        var app = t.Couches.Single(c => c.Code == "app");
        var dns = t.Octets.AsSpan(app.Debut, app.Longueur).ToArray();
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(dns.AsSpan(4)));       // une question
        Assert.Contains("www", Encoding.ASCII.GetString(dns));
        Assert.Equal(new byte[] { 0, 1, 0, 1 }, dns[^4..]);                         // type A, classe IN
    }

    [Fact]
    public void Le_ClientHello_TLS_a_des_longueurs_coherentes_et_annonce_le_SNI()
    {
        var t = Decoder("A", "kali", "203.0.113.1", Protocole.Https);
        var app = t.Couches.Single(c => c.Code == "app");
        var tls = t.Octets.AsSpan(app.Debut, app.Longueur).ToArray();
        Assert.Equal(0x16, tls[0]);
        Assert.Equal(tls.Length - 5, BinaryPrimitives.ReadUInt16BigEndian(tls.AsSpan(3)));
        Assert.Equal(1, tls[5]);                                                    // ClientHello
        Assert.Equal(tls.Length - 9, (tls[6] << 16) | (tls[7] << 8) | tls[8]);
        Assert.EndsWith("www.p6.lab", Encoding.ASCII.GetString(tls));
    }

    [Fact]
    public void Les_champs_couvrent_les_octets_qu_ils_annoncent()
    {
        var t = Decoder("A", "client-01", "10.20.0.10", Protocole.Https);
        foreach (var c in t.Couches)
            Assert.All(c.Champs, f => Assert.InRange(f.Debut + f.Longueur, 1, t.Octets.Length));
        Assert.Contains(t.Couches.SelectMany(c => c.Champs), f => f.Exemple);       // MAC et ports d'exemple signalés
    }

    [Fact]
    public void Somme_de_controle_RFC_1071()
    {
        // Exemple classique : en-tête IPv4 de Wikipédia (somme attendue 0xb861).
        var entete = Convert.FromHexString("450000730000400040110000c0a80001c0a800c7");
        Assert.Equal(0xb861, DecodeurPaquet.SommeControle(entete));
    }
}
