using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Topologie;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Domain.Moteur;

/// <summary>Un paquet tel que le voit un équipement : adresses et port après les éventuelles traductions.</summary>
public sealed record PaquetIp(Ipv4 Source, Ipv4 Destination, string Transport, int? Port);

/// <summary>Où envoyer un paquet : par quelle carte, et à quelle adresse sur ce segment (la destination ou la passerelle).</summary>
public sealed record Route(Carte Sortie, Ipv4 ProchainSaut, bool Directe);

/// <summary>
/// Moteur de routage (module 4) : réseau connecté le plus précis, sinon passerelle par défaut, sinon pas de route.
/// Deux machines sur le même segment se parlent directement : aucun routeur, donc aucun pare-feu, ne voit le paquet.
/// </summary>
public sealed class MoteurRoutage(TopologieSimulee topologie)
{
    public Route? Router(NoeudId depuis, Ipv4 destination)
    {
        var cartes = topologie.CartesDe(depuis).ToList();
        var connectee = cartes.Where(c => c.Prefixe.Contient(destination)).MaxBy(c => c.Prefixe.Longueur);
        if (connectee is not null) return new Route(connectee, destination, true);

        foreach (var passerelle in Passerelles(topologie.Noeud(depuis), cartes))
        {
            var versPasserelle = cartes.Where(c => c.Prefixe.Contient(passerelle)).MaxBy(c => c.Prefixe.Longueur);
            if (versPasserelle is not null) return new Route(versPasserelle, passerelle, false);
        }
        return null;
    }

    /// <summary>
    /// La passerelle déclarée de la machine ; pour un conteneur, à défaut, celle de son réseau Docker
    /// (Docker la fixe depuis l'ipam : en état B, bind9 passe de 10.20.0.1 à 10.20.0.33).
    /// </summary>
    private static IEnumerable<Ipv4> Passerelles(Noeud noeud, IEnumerable<Carte> cartes)
    {
        if (Ipv4.TryLire(noeud.Passerelle, out var declaree)) yield return declaree;
        if (noeud.Type != TypeNoeud.Conteneur) yield break;
        foreach (var c in cartes)
            if (Ipv4.TryLire(c.Reseau.Passerelle, out var p) && p != c.Adresse) yield return p;
    }

    /// <summary>La carte qui répond à l'ARP pour cette adresse sur le même segment que la carte de sortie.</summary>
    public Carte? Voisin(Carte sortie, Ipv4 adresse)
        => topologie.Cartes.FirstOrDefault(c => c.Reseau.Code == sortie.Reseau.Code && c.Adresse == adresse && c.Noeud.Id != sortie.Noeud.Id);
}

/// <summary>Redirection retenue par le moteur NAT : la règle, et l'adresse et le port après traduction.</summary>
public sealed record Redirection(RegleNat Regle, Ipv4 Cible, int? Port);

/// <summary>Moteur NAT (module 4) : Destination NAT d'OPNsense, appliquée sur l'interface d'entrée, avant le filtrage.</summary>
public sealed class MoteurNat(TopologieSimulee topologie)
{
    public Redirection? Rediriger(string affectation, PaquetIp p, OptionsSimulation options)
    {
        foreach (var r in topologie.ReglesNat)
        {
            if (!(r.Active || options.ReglesActivees.Contains(r.Code))) continue;
            if (!string.Equals(r.Interface, affectation, StringComparison.OrdinalIgnoreCase)) continue;
            if (!Champs.ProtoCorrespond(r.Proto, p.Transport) || !Champs.PortCorrespond(r.PortPublic, p.Port)) continue;
            if (!Ipv4.TryLire(r.AdressePublique, out var publique) || publique != p.Destination) continue;
            if (r.SourceAutorisee is not null && !Champs.AdresseCorrespond(r.SourceAutorisee, p.Source, _ => false)) continue;
            var port = int.TryParse(r.PortCible, out var pc) ? pc : p.Port;
            return new Redirection(r, Ipv4.Lire(r.Cible), port);
        }
        return null;
    }

    /// <summary>Règle de filtrage créée par OPNsense pour une redirection « Register rule » : any → cible:port.</summary>
    public IEnumerable<Regle> ReglesAssociees(string affectation, OptionsSimulation options)
        => topologie.ReglesNat
            .Where(r => r.Filtrage == FiltrageNat.Associee && (r.Active || options.ReglesActivees.Contains(r.Code))
                        && string.Equals(r.Interface, affectation, StringComparison.OrdinalIgnoreCase))
            .Select(r => new Regle
            {
                EtatReseauCode = r.EtatReseauCode, Equipement = NoeudId.Opnsense, Code = r.Code, Interface = r.Interface,
                Ordre = 0, Action = ActionFiltre.Pass, Proto = r.Proto, Source = r.SourceAutorisee ?? "any",
                Destination = r.Cible, Ports = r.PortCible, Description = $"Règle associée à la redirection {r.Code} (Register rule)"
            });
}

/// <summary>Décision d'un filtre : la règle qui a décidé (null : comportement par défaut), et si une ligne est journalisée.</summary>
public sealed record DecisionFiltre(Regle? Regle, bool Passe, bool Journal, string Explication);

/// <summary>
/// Moteur de pare-feu (module 5) : règles de l'interface d'entrée, dans l'ordre, première correspondance.
/// OPNsense refuse ce qu'aucune règle n'autorise ; sur une VM ou sur Windows, ce qu'aucune règle ne vise passe.
/// </summary>
public sealed class MoteurParefeu(TopologieSimulee topologie)
{
    public DecisionFiltre Evaluer(NoeudId equipement, string? affectation, PaquetIp p, OptionsSimulation options,
        IEnumerable<Regle>? reglesEnTete = null)
    {
        var estPareFeu = topologie.EstPareFeu(topologie.Noeud(equipement));
        var regles = (reglesEnTete ?? []).Concat(topologie.Regles.Where(r => r.Equipement == equipement
                && (r.Active || options.ReglesActivees.Contains(r.Code))
                && (!estPareFeu || string.Equals(r.Interface, affectation, StringComparison.OrdinalIgnoreCase))));
        bool Locale(Ipv4 ip) => topologie.EstLocale(equipement, ip);

        foreach (var r in regles)
        {
            if (!Champs.ProtoCorrespond(r.Proto, p.Transport)) continue;
            if (!Champs.AdresseCorrespond(r.Source, p.Source, Locale)) continue;
            if (!Champs.AdresseCorrespond(r.Destination, p.Destination, Locale)) continue;
            if (!Champs.PortCorrespond(r.Ports, p.Port)) continue;
            var passe = r.Action == ActionFiltre.Pass;
            return new DecisionFiltre(r, passe, r.Journal,
                $"{(passe ? "autorisé" : "jeté")} par la règle {r.Code}{(estPareFeu ? $" (interface {affectation})" : "")} : {r.Description}");
        }
        return estPareFeu
            ? new DecisionFiltre(null, false, true, $"aucune règle n'autorise ce paquet sur l'interface {affectation} : refus implicite de pf")
            : new DecisionFiltre(null, true, false, "aucune règle locale ne vise ce paquet");
    }
}
