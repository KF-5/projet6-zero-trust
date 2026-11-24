namespace P6Explorer.Domain.Politique;

/// <summary>
/// La politique comme donnée : les fichiers de donnees/politique/ (flows, segments, hosts, tests-A, tests-B),
/// d'où c2.py déduit les règles et les tests. Lus tels quels, sans rien recalculer.
/// </summary>
public class PolitiqueLaboratoire
{
    public List<FluxPolitique> Flux { get; init; } = [];
    public List<SegmentPolitique> Segments { get; init; } = [];
    public List<HotePolitique> Hotes { get; init; } = [];
    public List<TestPolitique> TestsA { get; init; } = [];
    public List<TestPolitique> TestsB { get; init; } = [];

    public IReadOnlyList<TestPolitique> Tests(string etat) => etat == "B" ? TestsB : TestsA;
    public IEnumerable<FluxPolitique> FluxDe(string etat) => Flux.Where(f => f.Etats.Contains(etat, StringComparison.Ordinal));
    public IEnumerable<SegmentPolitique> SegmentsDe(string etat) => Segments.Where(s => s.Etats.Contains(etat, StringComparison.Ordinal));
}

/// <summary>Une ligne de flows.csv. Sources et destinations : « WAN », « LAN-USERS », un hôte, ou « any » ; « ; » sépare plusieurs valeurs.</summary>
public record FluxPolitique(string Id, IReadOnlyList<string> Sources, IReadOnlyList<string> Destinations,
    string Proto, string Ports, string Justification, string Etats)
{
    /// <summary>La ligne « any → any » : le refus par défaut (FA5 en A, F99 en B), pas un flux autorisé.</summary>
    public bool EstRefusParDefaut => Sources.SequenceEqual(["any"]) && Destinations.SequenceEqual(["any"]);
}

/// <summary>Une ligne de segments.csv ; <see cref="Etats"/> vaut « A », « B » ou « AB » (MGMT).</summary>
public record SegmentPolitique(string Nom, string Etats, string Reseau, int? Vlan, string Passerelle);

/// <summary>Une ligne de hosts.csv : l'hôte, son adresse et son segment dans chaque état.</summary>
public record HotePolitique(string Hote, string Ip, string SegmentA, string SegmentB, string? Conteneur);

/// <summary>Une ligne de tests-A.csv ou tests-B.csv produite par c2.py ; <see cref="FluxRef"/> renvoie à flows.csv.</summary>
public record TestPolitique(string Id, string SourceHote, string IpDestination, string Proto, string Port, string Attendu, string FluxRef);
