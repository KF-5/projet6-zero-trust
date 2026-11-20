using P6Explorer.Domain.Amorcage;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Topologie;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Domain.Moteur;

/// <summary>Une carte réseau utilisable dans un état : branchée, adressée, sur un réseau de cet état.</summary>
public sealed record Carte(Noeud Noeud, Interface Interface, Reseau Reseau, Ipv4 Adresse, Prefixe4 Prefixe)
{
    public string Nom => Interface.Nom;
    public int? Vlan => Interface.Vlan ?? Reseau.Vlan;
    /// <summary>Nom de l'interface dans OPNsense (WAN, LAN, DMZ-WEB…), ou le nom de la carte ailleurs.</summary>
    public string Affectation => Interface.Affectation ?? Interface.Nom;
}

/// <summary>
/// Vue de la topologie pour un état (A ou B), prête pour les moteurs : cartes actives, règles et redirections de cet état.
/// Construite à partir des entités (base) ou des fichiers donnees/topologie/etat-X.json (tests).
/// </summary>
public sealed class TopologieSimulee
{
    public string Etat { get; }
    public IReadOnlyDictionary<NoeudId, Noeud> Noeuds { get; }
    public IReadOnlyList<Carte> Cartes { get; }
    public IReadOnlyList<Regle> Regles { get; }
    public IReadOnlyList<RegleNat> ReglesNat { get; }

    private TopologieSimulee(string etat, IReadOnlyDictionary<NoeudId, Noeud> noeuds, IReadOnlyList<Carte> cartes,
        IReadOnlyList<Regle> regles, IReadOnlyList<RegleNat> reglesNat)
        => (Etat, Noeuds, Cartes, Regles, ReglesNat) = (etat, noeuds, cartes, regles, reglesNat);

    public static TopologieSimulee Construire(string etat, IEnumerable<Noeud> noeuds, IEnumerable<Reseau> reseaux,
        IEnumerable<Regle> regles, IEnumerable<RegleNat> reglesNat)
    {
        bool DeLEtat(string? code) => code is null || code == etat;
        var reseauxEtat = reseaux.Where(r => DeLEtat(r.EtatReseauCode)).ToDictionary(r => r.Code);
        var dico = noeuds.ToDictionary(n => n.Id);
        var cartes = (
            from n in dico.Values
            from i in n.Interfaces
            where i.Branchee && DeLEtat(i.EtatReseauCode) && i.Adresse is not null && i.ReseauCode is not null
            where reseauxEtat.ContainsKey(i.ReseauCode!)
            let ip = Ipv4.Lire(i.Adresse!.Adresse)
            select new Carte(n, i, reseauxEtat[i.ReseauCode!], ip, Prefixe4.Creer(ip, i.Adresse!.Prefixe))
        ).ToList();
        return new TopologieSimulee(etat, dico, cartes,
            regles.Where(r => r.EtatReseauCode == etat).OrderBy(r => r.Ordre).ToList(),
            reglesNat.Where(r => r.EtatReseauCode == etat).ToList());
    }

    /// <summary>Construit l'état demandé à partir des fichiers d'amorçage (les nœuds communs sont fusionnés).</summary>
    public static TopologieSimulee Depuis(string etat, IReadOnlyCollection<FichierTopologie> fichiers)
        => Construire(etat, FichierTopologie.FusionnerNoeuds(fichiers), fichiers.SelectMany(f => f.Reseaux),
            fichiers.SelectMany(f => f.Regles), fichiers.SelectMany(f => f.ReglesNat));

    public Noeud Noeud(NoeudId id)
        => Noeuds.TryGetValue(id, out var n) ? n : throw new ArgumentException($"Machine inconnue : {id}.", nameof(id));

    public IEnumerable<Carte> CartesDe(NoeudId id) => Cartes.Where(c => c.Noeud.Id == id);

    /// <summary>L'adresse appartient-elle à la machine (une de ses cartes, ou 127.0.0.1) ?</summary>
    public bool EstLocale(NoeudId id, Ipv4 ip) => ip.EstBoucle || CartesDe(id).Any(c => c.Adresse == ip);

    /// <summary>La machine qui porte cette adresse dans l'état (null si personne).</summary>
    public Noeud? Proprietaire(Ipv4 ip) => Cartes.FirstOrDefault(c => c.Adresse == ip)?.Noeud;

    public bool EstPareFeu(Noeud n) => n.Type == TypeNoeud.PareFeu;
}
