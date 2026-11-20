using AutoMapper;
using P6Explorer.Application.Dto;
using P6Explorer.Application.ViewModels;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Interfaces;
using P6Explorer.Domain.Moteur;
using P6Explorer.Domain.Suivi;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Application.Services;

public interface IEtapeService
{
    /// <summary>La page d'une étape (vue rôle) ; null si l'étape n'existe pas.</summary>
    Task<EtapeVM?> ChargerAsync(int role, int numero, string etat, string? utilisateurId);

    /// <summary>Commandes à rejouer, sous-commandes dépliées, avec le calcul du moteur pour chaque paquet.</summary>
    Task<List<CommandeDto>> ListerCommandesAsync(int etapeId, string etat, string? utilisateurId);
}

public interface ISimulationService
{
    Task<ResultatPaquetDto> SimulerAsync(DemandeSimulationDto demande);
    /// <summary>Machines qu'on peut choisir comme source, et adresses connues comme destination.</summary>
    Task<SimulationFormVM> PreparerFormulaireAsync(string etat);
    Task<int> EnregistrerAsync(DemandeSimulationDto demande, string? titre, string? utilisateurId);
    Task<PageVM<SimulationLigneVM>> HistoriqueAsync(int page);
    Task MarquerDemoAsync(int id, bool pourDemo);
    Task DeplacerDemoAsync(int id, int sens);
    Task<List<SimulationLigneVM>> DemoAsync();
}

/// <summary>Traduit un résultat du moteur en DTO pour l'animation, avec la trame décodée.</summary>
public static class ConvertisseurSimulation
{
    public static ResultatPaquetDto EnDto(ResultatSimulation r, DemandeSimulation demande, TopologieSimulee topologie, bool avecTrame = true)
    {
        var dto = new ResultatPaquetDto
        {
            Source = demande.Source.Valeur,
            Destination = demande.Destination,
            Arrivee = r.Destination?.Valeur,
            Protocole = demande.Protocole.ToString(),
            Transport = demande.Protocole.Transport().ToString().ToLowerInvariant(),
            Port = demande.Port ?? SimulateurPaquet.PortHabituel(demande.Protocole),
            Verdict = r.Verdict.ToString(),
            VerdictTexte = TexteVerdict(r.Verdict),
            Regle = r.Regle,
            Equipement = r.Equipement?.Valeur,
            Filterlog = r.Filterlog,
            Explication = r.Explication,
            Nat = r.Nat is null ? null : new NatDto
            {
                Regle = r.Nat.Regle,
                Avant = $"{r.Nat.AdresseAvant}{(r.Nat.PortAvant is null ? "" : $":{r.Nat.PortAvant}")}",
                Apres = $"{r.Nat.AdresseApres}{(r.Nat.PortApres is null ? "" : $":{r.Nat.PortApres}")}"
            },
            Chemin = r.Chemin.Select(s => new SautDto { Noeud = s.Noeud.Valeur, Entree = s.Entree, Sortie = s.Sortie, Vlan = s.Vlan, Texte = s.Texte }).ToList(),
        };
        dto.Resume = r.Verdict switch
        {
            Verdict.Passe => r.Regle is null ? "Passe : aucun pare-feu sur le chemin." : $"Passe grâce à {r.Regle}.",
            Verdict.Refuse => "Le pare-feu laisse passer, mais aucun service n'écoute : refusé par la machine.",
            Verdict.Bloque => $"Bloqué par {r.Regle ?? "le refus par défaut"}{(r.Filterlog ? " (ligne filterlog écrite)" : "")}.",
            Verdict.Injoignable => "Injoignable : aucune route.",
            _ => ""
        };
        if (avecTrame && r.Chemin.Count > 0)
            dto.Trame = TrameDepuis(DecodeurPaquet.Construire(topologie, demande, r));
        return dto;
    }

    public static ResultatPaquetDto HorsLaboratoire(string source, string destination, Protocole p, int? port, string raison) => new()
    {
        Source = source, Destination = destination, Protocole = p.ToString(), Port = port,
        Transport = p.Transport().ToString().ToLowerInvariant(), Verdict = Verdict.ACalculer.ToString(),
        VerdictTexte = "hors du laboratoire simulé", Explication = raison, Calcule = false, Resume = raison
    };

    public static string TexteVerdict(Verdict v) => v switch
    {
        Verdict.Passe => "passe",
        Verdict.Refuse => "refusé par le service",
        Verdict.Bloque => "bloqué",
        Verdict.Injoignable => "injoignable",
        _ => "à calculer"
    };

    private static TrameDto? TrameDepuis(Trame? t) => t is null ? null : new TrameDto
    {
        Titre = t.Titre,
        Hexa = Convert.ToHexString(t.Octets).ToLowerInvariant(),
        Longueur = t.Octets.Length,
        Couches = t.Couches.Select(c => new CoucheDto
        {
            Nom = c.Nom, Code = c.Code, Debut = c.Debut, Longueur = c.Longueur,
            Champs = c.Champs.Select(x => new ChampDto { Nom = x.Nom, Debut = x.Debut, Longueur = x.Longueur, Valeur = x.Valeur, Explication = x.Explication, Exemple = x.Exemple }).ToList()
        }).ToList()
    };
}

public class EtapeService(
    IEtapeRepository etapes,
    IRoleRepository roles,
    ITopologieRepository topologie,
    ITopologieService schemas,
    IDiscussionRepository discussions,
    IQuizRepository quiz,
    IProgressionRepository progression,
    IGlossaireRepository glossaire,
    IMapper mapper) : IEtapeService
{
    public async Task<EtapeVM?> ChargerAsync(int role, int numero, string etat, string? utilisateurId)
    {
        var etape = await etapes.ChargerPageAsync(role, numero);
        if (etape?.Role is null) return null;
        var termes = await glossaire.ListerPourEtapeAsync(etape.Id);
        var liste = (await roles.ListerAvecEtapesAsync()).Single(r => r.Numero == role).Etapes;
        var dates = await etapes.DatesModificationAsync(role);
        var ouverts = await discussions.OuvertsParEtapeAsync(role);
        var maitrisees = utilisateurId is null ? new HashSet<int>() : await quiz.EtapesMaitriseesAsync(utilisateurId);

        var detail = mapper.Map<EtapeDetailVM>(etape);
        detail.ButHtml = TexteRiche.EnHtml(etape.But, termes);
        detail.PourquoiHtml = TexteRiche.EnHtml(etape.Pourquoi, termes);
        detail.PrerequisHtml = TexteRiche.EnHtml(etape.Prerequis, termes);
        detail.LivrableHtml = TexteRiche.EnHtml(etape.Livrable, termes);
        detail.MiseAJourHtml = TexteRiche.EnHtml(etape.MiseAJour, termes);
        detail.DateModification = dates.TryGetValue(etape.Id, out var d) ? d : null;

        return new EtapeVM
        {
            Role = new RoleEnteteVM
            {
                Numero = etape.Role.Numero, Titre = etape.Role.Titre, CourtNom = etape.Role.CourtNom, Couleur = etape.Role.Couleur,
                Mission = etape.Role.Mission, IntroductionHtml = TexteRiche.EnHtml(etape.Role.Introduction),
                DemonstrationHtml = TexteRiche.EnHtml(etape.Role.Demonstration)
            },
            Etapes = liste.Select(e => new EtapeResumeVM
            {
                Id = e.Id, Numero = e.Numero, Titre = e.Titre, Quand = e.Quand, Fait = e.Fait, EtatTexte = e.EtatTexte,
                Statut = e.Statut, DateModification = dates.TryGetValue(e.Id, out var x) ? x : null,
                QuestionsOuvertes = ouverts.GetValueOrDefault(e.Id), Maitrisee = maitrisees.Contains(e.Id), Active = e.Id == etape.Id
            }).ToList(),
            Etape = detail,
            Schema = await schemas.ChargerSchemaAsync(etat, utilisateurId is not null),
            Etat = etat,
            Connecte = utilisateurId is not null,
            QuestionsOuvertes = ouverts.Values.Sum(),
            QuizDisponible = await quiz.ChargerParEtapeAsync(etape.Id) is { Questions.Count: > 0 },
            Maitrisee = maitrisees.Contains(etape.Id),
            Termes = termes.Select(t => new TermeVM { Id = t.Id, Terme = t.Terme, Definition = t.Definition, Comparaison = t.Comparaison }).ToList(),
        };
    }

    public async Task<List<CommandeDto>> ListerCommandesAsync(int etapeId, string etat, string? utilisateurId)
    {
        var commandes = await etapes.ListerCommandesAsync(etapeId);
        var termes = await glossaire.ListerPourEtapeAsync(etapeId);
        var simulateur = new SimulateurPaquet(await topologie.ChargerPourMoteurAsync(etat));
        var ouvertes = await discussions.OuvertsParCommandeAsync(etapeId);
        var comprises = utilisateurId is null ? new HashSet<int>() : await progression.CommandesComprisesAsync(utilisateurId, etapeId);

        var liste = new List<CommandeDto>();
        void Ajouter(Commande c, int niveau, string? parent)
        {
            var dto = mapper.Map<CommandeDto>(c);
            dto.Index = liste.Count + 1;
            dto.Niveau = niveau;
            dto.ParentCode = parent;
            dto.OuNom = simulateur.Topologie.Noeuds.TryGetValue(c.Ou, out var n) ? n.Nom : c.Ou.Valeur;
            dto.Invite = Invite(c, n);
            dto.Graphique = c.Shell is Shell.OpnsenseGui or Shell.VmwareGui or Shell.WindowsGui or Shell.Navigateur;
            dto.ExplicationHtml = TexteRiche.EnHtml(c.Explication, termes);
            dto.QuestionsOuvertes = ouvertes.GetValueOrDefault(c.Id);
            dto.Comprise = comprises.Contains(c.Id);
            dto.Effets = c.Effets.OrderBy(e => e.Ordre).Select(e => Effet(e, simulateur)).ToList();
            liste.Add(dto);
            foreach (var s in c.SousCommandes.OrderBy(s => s.Ordre)) Ajouter(s, niveau + 1, c.Code);
        }
        foreach (var c in commandes) Ajouter(c, 0, null);
        return liste;
    }

    private static EffetDto Effet(Effet e, SimulateurPaquet simulateur) => new()
    {
        Donnees = e,
        Resume = e.Resumer(),
        Noeud = e switch
        {
            EtatNoeud x => x.Noeud.Valeur, AttribuerIp x => x.Noeud.Valeur, Paquet x => x.De.Valeur, Service x => x.Noeud.Valeur,
            Fichier x => x.Noeud.Valeur, Instantane x => x.Vm.Valeur, Journal x => x.De.Valeur, Surbrillance x => x.Noeud.Valeur,
            ConteneurReseau x => x.Conteneur.Valeur, AlerteIds => NoeudId.Opnsense.Valeur, ChangementRegle => NoeudId.Opnsense.Valeur,
            _ => null
        },
        Paquet = e is Paquet p ? SimulerPaquet(p, simulateur) : null
    };

    /// <summary>Un effet Paquet : le moteur calcule chemin, NAT, verdict et règle selon l'état choisi.</summary>
    public static ResultatPaquetDto SimulerPaquet(Paquet p, SimulateurPaquet simulateur)
    {
        var t = simulateur.Topologie;
        var destination = Ipv4.TryLire(p.AdresseVisee, out _) ? p.AdresseVisee!
            : t.CartesDe(p.Vers).Select(c => c.Adresse.ToString()).FirstOrDefault();
        if (destination is null || !t.Noeuds.ContainsKey(p.De) || !t.CartesDe(p.De).Any())
            return ConvertisseurSimulation.HorsLaboratoire(p.De.Valeur, destination ?? p.Vers.Valeur, p.Proto, p.Port,
                "Hors du laboratoire simulé (PC d'un membre, Internet, Tailscale) : le paquet est seulement montré, sans calcul de chemin.");
        var demande = new DemandeSimulation(p.De, destination, p.Proto, p.Port ?? SimulateurPaquet.PortHabituel(p.Proto) ?? (p.Proto.Transport() == Protocole.Icmp ? null : 0));
        var dto = ConvertisseurSimulation.EnDto(simulateur.Simuler(demande), demande, t);
        if (p.Verdict != Verdict.ACalculer && p.Verdict.ToString() != dto.Verdict)
        {
            dto.Explication += $" (Verdict fixé à la main dans la commande : {ConvertisseurSimulation.TexteVerdict(p.Verdict)}.)";
            dto.Verdict = p.Verdict.ToString();
            dto.VerdictTexte = ConvertisseurSimulation.TexteVerdict(p.Verdict);
        }
        return dto;
    }

    private static readonly Dictionary<string, string> NomsMachines = new()
    {
        ["opnsense"] = "fw-p6", ["docker"] = "p6-docker", ["collecteur"] = "p6-collecteur", ["kali"] = "p6-kali",
    };

    /// <summary>Invite affichée devant la commande, comme dans le vrai terminal.</summary>
    public static string Invite(Commande c, Domain.Topologie.Noeud? n) => c.Shell switch
    {
        Shell.PowerShell => "PS C:\\Labo-P6>",
        Shell.OpnsenseShell => "root@fw-p6:~ #",
        Shell.OpnsenseGui => "OPNsense ›",
        Shell.VmwareGui => "VMware ›",
        Shell.WindowsGui => "Windows ›",
        Shell.Navigateur => "Navigateur ›",
        Shell.Editeur => $"fichier ({NomsMachines.GetValueOrDefault(c.Ou.Valeur, c.Ou.Valeur)}) ›",
        Shell.Document => "document ›",
        _ when n?.Type == TypeNoeud.Conteneur => $"root@{c.Ou.Valeur}:/#",
        _ when c.Ou == NoeudId.PcMembre => "membre@pc:~$",
        _ => $"{c.Compte ?? "karim"}@{NomsMachines.GetValueOrDefault(c.Ou.Valeur, c.Ou.Valeur)}:~$"
    };
}

public class SimulationService(ITopologieRepository topologie, ISimulationRepository simulations, TimeProvider horloge) : ISimulationService
{
    public const int TaillePage = 15;

    public async Task<ResultatPaquetDto> SimulerAsync(DemandeSimulationDto d)
    {
        var etat = d.Etat == "B" ? "B" : "A";
        var t = await topologie.ChargerPourMoteurAsync(etat);
        if (!Enum.TryParse<Protocole>(d.Protocole, true, out var proto)) proto = Protocole.Tcp;
        if (!Ipv4.TryLire(d.Destination, out _))
            return ConvertisseurSimulation.HorsLaboratoire(d.Source, d.Destination, proto, d.Port, "Adresse de destination invalide : écris une adresse IPv4 (par exemple 10.10.0.70).");
        if (!t.Noeuds.ContainsKey(new NoeudId(d.Source)))
            return ConvertisseurSimulation.HorsLaboratoire(d.Source, d.Destination, proto, d.Port, "Machine source inconnue.");
        var port = proto.Transport() == Protocole.Icmp ? null : d.Port ?? SimulateurPaquet.PortHabituel(proto);
        if (proto.Transport() != Protocole.Icmp && port is null)
            return ConvertisseurSimulation.HorsLaboratoire(d.Source, d.Destination, proto, d.Port, "Indique un port de destination.");
        var demande = new DemandeSimulation(new NoeudId(d.Source), d.Destination, proto, port);
        return ConvertisseurSimulation.EnDto(new SimulateurPaquet(t).Simuler(demande, Options(d)), demande, t);
    }

    public static OptionsSimulation Options(DemandeSimulationDto d)
    {
        var regles = new HashSet<string>();
        if (d.Mesures) { regles.Add("FA6"); regles.Add("F09"); }
        if (d.DurcissementQ16) regles.Add("p6_syslog");
        return new OptionsSimulation { SshOpnsenseActif = d.SshOpnsense, ReglesActivees = regles };
    }

    public async Task<SimulationFormVM> PreparerFormulaireAsync(string etat)
    {
        var t = await topologie.ChargerPourMoteurAsync(etat);
        return new SimulationFormVM
        {
            Etat = etat,
            Sources = t.Noeuds.Values.Where(n => t.CartesDe(n.Id).Any()).OrderBy(n => n.Zone).ThenBy(n => n.Nom)
                .Select(n => new ChoixMachineVM(n.Id.Valeur, $"{n.Nom} ({string.Join(", ", t.CartesDe(n.Id).Select(c => c.Adresse))})")).ToList(),
            Destinations = t.Cartes.OrderBy(c => c.Adresse.Valeur)
                .Select(c => new ChoixMachineVM(c.Adresse.ToString(), $"{c.Adresse} · {c.Noeud.Nom} {c.Nom}"))
                .Concat([new("192.168.1.1", "192.168.1.1 · box de la maison"), new("8.8.8.8", "8.8.8.8 · Internet")]).ToList(),
        };
    }

    public async Task<int> EnregistrerAsync(DemandeSimulationDto d, string? titre, string? utilisateurId)
    {
        var r = await SimulerAsync(d);
        var s = new Simulation
        {
            UtilisateurId = utilisateurId, Titre = string.IsNullOrWhiteSpace(titre) ? null : titre.Trim(),
            Source = d.Source, Destination = d.Destination,
            Protocole = Enum.TryParse<Protocole>(d.Protocole, true, out var p) ? p : Protocole.Tcp, Port = r.Port,
            EtatReseauCode = d.Etat == "B" ? "B" : "A",
            Verdict = Enum.TryParse<Verdict>(r.Verdict, out var v) ? v : Verdict.ACalculer, Regle = r.Regle,
            Chemin = string.Join(" → ", r.Chemin.Select(c => c.Noeud)), Date = horloge.GetUtcNow().UtcDateTime
        };
        await simulations.AjouterAsync(s);
        await simulations.EnregistrerAsync();
        return s.Id;
    }

    public async Task<PageVM<SimulationLigneVM>> HistoriqueAsync(int page)
    {
        var p = await simulations.ListerAsync(page, TaillePage);
        return new PageVM<SimulationLigneVM> { Elements = p.Elements.Select(Ligne).ToList(), Page = p.Numero, NombrePages = p.NombrePages, Total = p.Total };
    }

    public async Task MarquerDemoAsync(int id, bool pourDemo)
    {
        var s = await simulations.TrouverAsync(id) ?? throw new Domain.Exceptions.EntiteIntrouvableException("Simulation", id);
        s.PourDemo = pourDemo;
        s.OrdreDemo = pourDemo ? (await simulations.ListerDemoAsync()).Select(x => x.OrdreDemo ?? 0).DefaultIfEmpty(0).Max() + 1 : null;
        await simulations.EnregistrerAsync();
    }

    public async Task DeplacerDemoAsync(int id, int sens)
    {
        var demo = (await simulations.ListerDemoAsync()).Select(x => x.Id).ToList();
        var i = demo.IndexOf(id);
        var j = i + Math.Sign(sens);
        if (i < 0 || j < 0 || j >= demo.Count) return;
        (demo[i], demo[j]) = (demo[j], demo[i]);
        for (var k = 0; k < demo.Count; k++)
        {
            var s = await simulations.TrouverAsync(demo[k]);
            if (s is not null) s.OrdreDemo = k + 1;
        }
        await simulations.EnregistrerAsync();
    }

    public async Task<List<SimulationLigneVM>> DemoAsync() => (await simulations.ListerDemoAsync()).Select(Ligne).ToList();

    private static SimulationLigneVM Ligne(Simulation s) => new()
    {
        Id = s.Id, Titre = s.Titre, Source = s.Source, Destination = s.Destination, Protocole = s.Protocole.ToString(), Port = s.Port,
        Etat = s.EtatReseauCode, Verdict = s.Verdict.ToString(), VerdictTexte = ConvertisseurSimulation.TexteVerdict(s.Verdict),
        Regle = s.Regle, Chemin = s.Chemin, PourDemo = s.PourDemo, OrdreDemo = s.OrdreDemo, Date = s.Date
    };
}
