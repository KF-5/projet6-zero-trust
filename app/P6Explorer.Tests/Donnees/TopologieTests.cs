using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Valeurs;
using P6Explorer.Tests.Outils;

namespace P6Explorer.Tests.Donnees;

/// <summary>La topologie de l'état A correspond à la plateforme réelle (docs/interfaces.md, ECARTS.md E31–E42).</summary>
public class TopologieTests
{
    private readonly Domain.Amorcage.FichierTopologie _a = Depot.LireTopologie("A");

    [Theory]
    [InlineData("opnsense", "em0", "203.0.113.1")]
    [InlineData("opnsense", "vlan01", "10.10.0.1")]
    [InlineData("opnsense", "vlan02", "10.20.0.1")]
    [InlineData("opnsense", "em2", "10.99.0.1")]
    [InlineData("docker", "ens32", "10.99.0.20")]
    [InlineData("collecteur", "ens32", "10.99.0.10")]
    [InlineData("kali", "eth0", "203.0.113.66")]
    [InlineData("kali", "eth1", "10.99.0.66")]
    [InlineData("hote", "VMnet9", "10.99.0.2")]
    [InlineData("nginx", "eth0", "10.20.0.10")]
    [InlineData("postgres", "eth0", "10.10.0.70")]
    [InlineData("client-ext", "eth0", "203.0.113.10")]
    public void Adresses_reelles(string noeud, string carte, string ip)
    {
        var n = _a.Noeuds.Single(x => x.Id == new NoeudId(noeud));
        Assert.Equal(ip, n.Interfaces.Single(i => i.Nom == carte).Adresse?.Adresse);
    }

    [Fact]
    public void Kali_a_une_seule_route_par_defaut_vers_opnsense()
        => Assert.Equal("203.0.113.1", _a.Noeuds.Single(n => n.Id == NoeudId.Kali).Passerelle);

    [Fact]
    public void Les_cartes_nat_sont_debranchees()
    {
        var nat = _a.Noeuds.SelectMany(n => n.Interfaces.Select(i => (n.Id, i))).Where(x => x.i.ReseauCode == "vmnet8").ToList();
        Assert.Equal(4, nat.Count);                     // opnsense em3, docker ens35, collecteur ens33, kali eth2
        Assert.All(nat, x => Assert.False(x.i.Branchee));
        Assert.All(_a.Liens.Where(l => l.Id.Valeur.EndsWith("-nat")), l => Assert.False(l.Branche));
    }

    [Fact]
    public void Les_references_sont_coherentes()
    {
        var noeuds = _a.Noeuds.Select(n => n.Id).ToHashSet();
        var reseaux = _a.Reseaux.Select(r => r.Code).ToHashSet();
        Assert.All(_a.Liens, l => { Assert.Contains(l.De, noeuds); Assert.Contains(l.Vers, noeuds); Assert.True(l.Points.Count >= 2); });
        Assert.All(_a.Noeuds.SelectMany(n => n.Interfaces).Where(i => i.ReseauCode is not null), i => Assert.Contains(i.ReseauCode!, reseaux));
        var adresses = _a.Noeuds.SelectMany(n => n.Interfaces).Where(i => i.Adresse is not null && i.Nom != "Tailscale").Select(i => i.Adresse!.Adresse).ToList();
        Assert.Equal(adresses.Count, adresses.Distinct().Count());
    }

    [Fact]
    public void Regles_de_l_etat_A()
    {
        var opn = _a.Regles.Where(r => r.Equipement == NoeudId.Opnsense).ToList();
        Assert.Equal(["FA1", "FA2", "FA6", "FA5"], opn.Where(r => r.Interface == "WAN").OrderBy(r => r.Ordre).Select(r => r.Code));
        Assert.False(opn.Single(r => r.Code == "FA6").Active);
        Assert.All(opn.Where(r => r.Code == "FA5"), r => Assert.Equal(ActionFiltre.Block, r.Action));
        Assert.Contains(opn, r => r.Code == "Admin du pare-feu" && r.Source == "10.99.0.2;10.99.0.10" && r.Ports == "22;443");
        Assert.Equal(3, _a.ReglesNat.Count(r => r.Active));
    }
}
