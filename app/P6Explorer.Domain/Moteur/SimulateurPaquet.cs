using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Topologie;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Domain.Moteur;

/// <summary>Ce que l'on veut envoyer : depuis quelle machine, vers quelle adresse, avec quel protocole.</summary>
/// <param name="Port">Port de destination ; si absent, le port habituel du protocole (443 pour Https…).</param>
public sealed record DemandeSimulation(NoeudId Source, string Destination, Protocole Protocole, int? Port = null);

/// <summary>Réglages de la simulation qui ne sont pas dans la topologie de référence.</summary>
public sealed class OptionsSimulation
{
    public static readonly OptionsSimulation Defaut = new();

    /// <summary>Q13 : le SSH d'OPNsense est désactivé ; il sera réactivé sur MGMT pour l'orchestrateur du rôle 5.</summary>
    public bool SshOpnsenseActif { get; init; }

    /// <summary>Codes de règles désactivées à considérer comme actives (FA6 / F09 pendant les mesures iperf3).</summary>
    public IReadOnlySet<string> ReglesActivees { get; init; } = new HashSet<string>();
}

/// <summary>Un passage du paquet par une machine.</summary>
public sealed record Saut(NoeudId Noeud, string? Entree, string? Sortie, int? Vlan, string Texte);

/// <summary>Résultat d'une simulation : chemin saut par saut, traduction, verdict et règle responsable.</summary>
public sealed record ResultatSimulation(
    Verdict Verdict,
    IReadOnlyList<Saut> Chemin,
    Nat? Nat,
    string? Regle,
    NoeudId? Equipement,
    bool Filterlog,
    NoeudId? Destination,
    string Explication)
{
    /// <summary>La connexion aboutit (sens de tests.sh et tests-cloisonnement.sh : tout le reste compte « bloqué »).</summary>
    public bool Aboutit => Verdict == Verdict.Passe;

    /// <summary>Le paquet a franchi les filtres (sens de test_flux.sh : un RST du service prouve que le pare-feu a laissé passer).</summary>
    public bool FranchitLesFiltres => Verdict is Verdict.Passe or Verdict.Refuse;
}

/// <summary>
/// Simule un paquet : routage (MoteurRoutage), puis à chaque passage par OPNsense la Destination NAT (MoteurNat)
/// et le filtrage de l'interface d'entrée (MoteurParefeu), enfin le filtre local et le service de la machine d'arrivée.
/// Les réponses sont supposées autorisées (pf et nftables gardent l'état des connexions), mais la machine d'arrivée
/// doit avoir une route de retour vers la source.
/// </summary>
public sealed class SimulateurPaquet
{
    private const int SautsMax = 8;
    private readonly TopologieSimulee _t;
    private readonly MoteurRoutage _routage;
    private readonly MoteurNat _nat;
    private readonly MoteurParefeu _parefeu;

    public SimulateurPaquet(TopologieSimulee topologie)
    {
        _t = topologie;
        _routage = new MoteurRoutage(topologie);
        _nat = new MoteurNat(topologie);
        _parefeu = new MoteurParefeu(topologie);
    }

    public TopologieSimulee Topologie => _t;

    public ResultatSimulation Simuler(DemandeSimulation demande, OptionsSimulation? options = null)
    {
        options ??= OptionsSimulation.Defaut;
        var source = _t.Noeud(demande.Source);
        var transport = demande.Protocole.Transport().ToString().ToLowerInvariant();
        int? port = transport == "icmp" ? null : demande.Port ?? PortHabituel(demande.Protocole)
            ?? throw new ArgumentException($"Port de destination manquant pour {demande.Protocole}.", nameof(demande));
        var destination = Ipv4.Lire(demande.Destination);
        var chemin = new List<Saut>();
        var etat = new Parcours(chemin);

        if (_t.EstLocale(source.Id, destination))
        {
            chemin.Add(new Saut(source.Id, null, null, null, $"{source.Nom} s'adresse à lui-même"));
            return Arriver(source, new PaquetIp(destination, destination, transport, port), etat, options, local: true);
        }

        var route = _routage.Router(source.Id, destination);
        if (route is null)
            return Fin(Verdict.Injoignable, etat, $"{source.Nom} n'a aucune route vers {destination} (pas de réseau connecté, pas de passerelle).");

        var paquet = new PaquetIp(route.Sortie.Adresse, destination, transport, port);
        chemin.Add(new Saut(source.Id, null, route.Sortie.Nom, route.Sortie.Vlan,
            $"{source.Nom} émet depuis {route.Sortie.Adresse} ({route.Sortie.Nom}) " +
            (route.Directe ? $"directement sur {route.Sortie.Reseau.Nom}" : $"vers sa passerelle {route.ProchainSaut}")));

        for (var i = 0; i < SautsMax; i++)
        {
            var voisin = _routage.Voisin(route.Sortie, route.ProchainSaut);
            if (voisin is null)
                return Fin(Verdict.Injoignable, etat,
                    $"Personne ne répond à l'ARP pour {route.ProchainSaut} sur {route.Sortie.Reseau.Nom} : le paquet n'est jamais livré.");
            var noeud = voisin.Noeud;

            if (!_t.EstPareFeu(noeud))
            {
                if (!_t.EstLocale(noeud.Id, paquet.Destination))
                    return Fin(Verdict.Injoignable, etat, $"{noeud.Nom} n'est pas un routeur : il ne relaie pas le paquet vers {paquet.Destination}.");
                chemin.Add(new Saut(noeud.Id, voisin.Nom, null, voisin.Vlan, $"{noeud.Nom} reçoit le paquet sur {voisin.Nom}"));
                return Arriver(noeud, paquet, etat, options);
            }

            // OPNsense : Destination NAT sur l'interface d'entrée, puis filtrage de cette interface.
            var affectation = voisin.Affectation;
            var redirection = _nat.Rediriger(affectation, paquet, options);
            if (redirection is not null)
            {
                etat.Nat = new Nat
                {
                    Regle = redirection.Regle.Code, AdresseAvant = paquet.Destination.ToString(), PortAvant = paquet.Port,
                    AdresseApres = redirection.Cible.ToString(), PortApres = redirection.Port
                };
                paquet = paquet with { Destination = redirection.Cible, Port = redirection.Port };
            }

            DecisionFiltre decision = redirection?.Regle.Filtrage == FiltrageNat.Pass
                ? new DecisionFiltre(null, true, false, $"redirection {redirection.Regle.Code} avec « Firewall rule = Pass » : les règles de filtrage ne sont pas évaluées (E36)")
                : _parefeu.Evaluer(noeud.Id, affectation, paquet, options, _nat.ReglesAssociees(affectation, options));
            etat.Filterlog |= decision.Journal;
            if (decision.Regle is not null || !decision.Passe) (etat.Regle, etat.Equipement) = (decision.Regle?.Code ?? "refus implicite", noeud.Id);
            else if (redirection is not null) (etat.Regle, etat.Equipement) = (redirection.Regle.Code, noeud.Id);

            if (!decision.Passe)
            {
                chemin.Add(new Saut(noeud.Id, voisin.Nom, null, voisin.Vlan, $"OPNsense ({affectation}) : {decision.Explication}"));
                return Fin(Verdict.Bloque, etat, $"Le paquet est {decision.Explication}.");
            }

            if (_t.EstLocale(noeud.Id, paquet.Destination))
            {
                chemin.Add(new Saut(noeud.Id, voisin.Nom, null, voisin.Vlan, $"OPNsense ({affectation}) : {decision.Explication} ; destination : le pare-feu lui-même"));
                return Arriver(noeud, paquet, etat, options, decision.Explication);
            }

            var suite = _routage.Router(noeud.Id, paquet.Destination);
            if (suite is null)
            {
                chemin.Add(new Saut(noeud.Id, voisin.Nom, null, voisin.Vlan, $"OPNsense ({affectation}) : {decision.Explication}"));
                return Fin(Verdict.Injoignable, etat,
                    $"OPNsense n'a aucune route vers {paquet.Destination} (pas de passerelle amont : la carte NAT est débranchée).");
            }
            chemin.Add(new Saut(noeud.Id, voisin.Nom, suite.Sortie.Nom, suite.Sortie.Vlan,
                $"OPNsense ({affectation}) : {decision.Explication} ; routé vers {suite.Sortie.Affectation}"));
            route = suite;
        }
        return Fin(Verdict.Injoignable, etat, "Boucle de routage : le paquet a dépassé le nombre de sauts autorisé.");
    }

    /// <summary>Le paquet arrive sur la machine qui porte l'adresse : filtre local, route de retour, service.</summary>
    private ResultatSimulation Arriver(Noeud noeud, PaquetIp p, Parcours etat, OptionsSimulation options,
        string? decisionPf = null, bool local = false)
    {
        if (!_t.EstPareFeu(noeud) && !local)                // le trafic d'une machine vers elle-même n'est pas filtré
        {
            var filtre = _parefeu.Evaluer(noeud.Id, null, p, options);
            if (!filtre.Passe)
            {
                (etat.Regle, etat.Equipement) = (filtre.Regle?.Code, noeud.Id);
                return Fin(Verdict.Bloque, etat, $"Arrivé sur {noeud.Nom}, le paquet est {filtre.Explication}.", noeud.Id);
            }
        }

        if (!local && _routage.Router(noeud.Id, p.Source) is null)
            return Fin(Verdict.Injoignable, etat,
                $"{noeud.Nom} reçoit le paquet mais n'a aucune route de retour vers {p.Source} : la réponse ne repart jamais.", noeud.Id);

        var avant = decisionPf is null ? "" : $"OPNsense : {decisionPf}. ";
        if (p.Transport == "icmp")
            return Fin(Verdict.Passe, etat, $"{avant}{noeud.Nom} ({p.Destination}) répond à l'écho ICMP.", noeud.Id);

        var service = noeud.Services.FirstOrDefault(s =>
            string.Equals(s.Proto, p.Transport, StringComparison.OrdinalIgnoreCase) && s.Port == p.Port
            && (s.Adresse is null || s.Adresse == p.Destination.ToString())
            && (s.Actif || (noeud.Id == NoeudId.Opnsense && s.Port == 22 && options.SshOpnsenseActif)));
        if (service is not null)
            return Fin(Verdict.Passe, etat, $"{avant}{noeud.Nom} accepte la connexion : {service.Nom} écoute sur {p.Transport.ToUpperInvariant()} {p.Port}.", noeud.Id);

        var refus = p.Transport == "tcp" ? "il répond par un RST (connexion refusée)" : "il répond « port injoignable » (ICMP)";
        var arret = noeud.Services.FirstOrDefault(s => s.Port == p.Port && !s.Actif);
        var pourquoi = arret is null ? $"aucun service n'écoute sur {p.Transport.ToUpperInvariant()} {p.Port}" : $"{arret.Nom} est arrêté ({arret.Description})";
        return Fin(Verdict.Refuse, etat, $"{avant}Le paquet arrive sur {noeud.Nom}, mais {pourquoi} : {refus}.", noeud.Id);
    }

    private static ResultatSimulation Fin(Verdict verdict, Parcours etat, string explication, NoeudId? destination = null)
        => new(verdict, etat.Chemin, etat.Nat, etat.Regle, etat.Equipement, etat.Filterlog, destination, explication);

    public static int? PortHabituel(Protocole p) => p switch
    {
        Protocole.Http => 80, Protocole.Https => 443, Protocole.Ssh => 22, Protocole.Dns => 53, Protocole.Ntp => 123,
        Protocole.Syslog => 514, Protocole.PostgreSql => 5432, Protocole.Iperf3 => 5201, Protocole.Smb => 445,
        Protocole.Rdp => 3389, Protocole.Vnc => 5900, _ => null
    };

    private sealed class Parcours(List<Saut> chemin)
    {
        public List<Saut> Chemin { get; } = chemin;
        public Nat? Nat { get; set; }
        public string? Regle { get; set; }
        public NoeudId? Equipement { get; set; }
        public bool Filterlog { get; set; }
    }
}
