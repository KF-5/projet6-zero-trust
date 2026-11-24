using System.Globalization;
using P6Explorer.Application.Dto;
using P6Explorer.Application.ViewModels;
using P6Explorer.Domain.Interfaces;
using P6Explorer.Domain.Politique;
using P6Explorer.Domain.Video;

namespace P6Explorer.Application.Services;

/// <summary>La politique comme donnée : matrice de flux d'un état et tests qui vérifient chaque flux.</summary>
public interface IPolitiqueService
{
    MatriceVM Matrice(string etat);
}

/// <summary>Section « Vidéo » : la liste des scènes P-1 à P-8 et une scène prête à filmer.</summary>
public interface IVideoService
{
    Task<VideoIndexVM> ListerAsync();
    /// <summary>Null si la scène n'existe pas dans scenes.json.</summary>
    Task<SceneTournageVM?> ChargerSceneAsync(string id);
}

public class PolitiqueService(ISourcePolitique source) : IPolitiqueService
{
    public MatriceVM Matrice(string etat)
    {
        etat = etat == "B" ? "B" : "A";
        var politique = source.Charger();
        var lignes = politique.FluxDe(etat).ToList();
        var autorises = lignes.Where(f => !f.EstRefusParDefaut).ToList();
        var tests = politique.Tests(etat);

        var sources = autorises.SelectMany(f => f.Sources).Distinct().ToList();
        var destinations = autorises.SelectMany(f => f.Destinations).Distinct().ToList();
        var cases = sources.Select(s => destinations.Select(d => autorises
            .Where(f => f.Sources.Contains(s) && f.Destinations.Contains(d))
            .Select(f => new CaseMatriceVM { FluxId = f.Id, Proto = Afficher(f.Proto), Ports = Ports(f.Ports) }).ToList()).ToList()).ToList();

        return new MatriceVM
        {
            Etat = etat,
            Sources = sources.Select(Afficher).ToList(),
            Destinations = destinations.Select(Afficher).ToList(),
            Cases = cases,
            IdRefusDefaut = lignes.FirstOrDefault(f => f.EstRefusParDefaut)?.Id,
            NbFlux = autorises.Count,
            NbTests = tests.Count,
            NbPass = tests.Count(t => t.Attendu == "pass"),
            NbBlock = tests.Count(t => t.Attendu == "block"),
            Flux = lignes.Select(f => new FluxLigneVM
            {
                Id = f.Id,
                Source = string.Join(", ", f.Sources.Select(Afficher)),
                Destination = string.Join(", ", f.Destinations.Select(Afficher)),
                Proto = Afficher(f.Proto),
                Ports = Ports(f.Ports),
                Justification = f.Justification,
                RefusParDefaut = f.EstRefusParDefaut,
                LigneCsv = string.Join(",", f.Id, string.Join(";", f.Sources), string.Join(";", f.Destinations), f.Proto, f.Ports, f.Justification, f.Etats),
                Tests = tests.Where(t => t.FluxRef == f.Id).Select(t => new TestLigneVM
                {
                    Id = t.Id, SourceHote = t.SourceHote, IpDestination = t.IpDestination, Proto = t.Proto, Port = t.Port, Attendu = t.Attendu,
                }).ToList(),
            }).ToList(),
        };
    }

    /// <summary>« any » s'affiche « tout » (sources, destinations, protocole, ports).</summary>
    public static string Afficher(string v) => v == "any" ? "tout" : v;
    private static string Ports(string v) => v == "any" ? "tout" : v.Replace(";", ", ");
}

public class VideoService(
    ISourceScenes scenes,
    ISourceSynthese synthese,
    ISourcePolitique politique,
    IPolitiqueService politiqueService,
    ITopologieService topologie,
    ISimulationService simulations,
    IRoleService roles) : IVideoService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public Task<VideoIndexVM> ListerAsync()
    {
        var lecture = scenes.Charger();
        var resultats = synthese.Charger();
        var vm = new VideoIndexVM { Erreurs = lecture.Erreurs, ResultatsIllustration = resultats.Illustration, RemarquesResultats = resultats.Remarques };
        if (lecture.Valeur is null) return Task.FromResult(vm);
        vm.Scenes = Ordonner(lecture.Valeur.Scenes).Select(s => new SceneLigneVM
        {
            Id = s.Id, Titre = s.Titre, Sequence = s.Sequence, DureeS = s.DureeS, Etat = s.Etat, NbEtapes = s.Etapes.Count,
            Statut = Statut(s, resultats),
        }).ToList();
        return Task.FromResult(vm);
    }

    public async Task<SceneTournageVM?> ChargerSceneAsync(string id)
    {
        var lecture = scenes.Charger();
        if (lecture.Valeur is null) return new SceneTournageVM { Id = id, Titre = "scenes.json illisible", Erreurs = lecture.Erreurs };
        var ordre = Ordonner(lecture.Valeur.Scenes);
        var i = ordre.FindIndex(s => s.Id == id);
        if (i < 0) return null;
        var s = ordre[i];

        var vm = new SceneTournageVM
        {
            Id = s.Id, Titre = s.Titre, Sequence = s.Sequence, DureeS = s.DureeS, Etat = s.Etat, Cible = s.Cible,
            Precedente = i > 0 ? ordre[i - 1].Id : null,
            Suivante = i + 1 < ordre.Count ? ordre[i + 1].Id : null,
            Erreurs = lecture.Erreurs,
            // Pages filmées : jamais les adresses réservées (Tailscale), même pour un membre connecté.
            SchemaA = await topologie.ChargerSchemaAsync("A", membreConnecte: false),
            SchemaB = await topologie.ChargerSchemaAsync("B", membreConnecte: false),
        };
        var pol = politique.Charger();
        vm.Zones["A"] = Zones(vm.SchemaA, pol, "A");
        vm.Zones["B"] = Zones(vm.SchemaB, pol, "B");
        vm.Etapes = s.Etapes.Select(e => Lecture(e, s, pol)).ToList();

        foreach (var e in s.Etapes.Where(e => e.TypeConnu == TypeEtapeScene.Matrice && e.Etat is not null))
            vm.Matrices[e.Etat!] = politiqueService.Matrice(e.Etat!);
        var resultats = synthese.Charger();
        if (s.Etapes.Any(e => e.TypeConnu == TypeEtapeScene.Resultats)) vm.Resultats = Resultats(resultats);
        if (s.Etapes.Any(e => e.TypeConnu == TypeEtapeScene.Resume)) vm.Resume = await ResumeAsync(lecture.Valeur, pol, resultats);
        return vm;
    }

    /// <summary>Dans l'ordre P-1 … P-8 (pas l'ordre du fichier).</summary>
    private static List<SceneVideo> Ordonner(IEnumerable<SceneVideo> liste)
        => liste.OrderBy(s => int.TryParse(s.Id.Replace("P-", ""), out var n) ? n : int.MaxValue).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();

    private static string Statut(SceneVideo s, Synthese resultats)
    {
        if (s.Cible) return "cible";
        var utiliseResultats = s.Etapes.Any(e => e.TypeConnu is TypeEtapeScene.Resultats or TypeEtapeScene.Resume);
        return utiliseResultats && resultats.Illustration ? "illustration" : "prête";
    }

    private static EtapeLectureVM Lecture(EtapeScene e, SceneVideo s, PolitiqueLaboratoire pol)
    {
        var etat = e.Etat ?? (e.TypeConnu is TypeEtapeScene.Simulation or TypeEtapeScene.SurlignerZone ? s.Etat : null);
        var legende = e.Legende;
        // Légende d'un segment : lue dans segments.csv (nom, VLAN, réseau), jamais écrite à la main.
        if (e.TypeConnu == TypeEtapeScene.SurlignerZone && e.Segment)
            legende = string.Join(" — ", (e.Zones ?? []).Select(z => pol.SegmentsDe(etat ?? s.Etat).FirstOrDefault(x => x.Nom == z) is { } seg
                ? $"{seg.Nom} · {(seg.Vlan is { } v ? $"VLAN {v} · " : "")}{seg.Reseau}"
                : z));
        return new EtapeLectureVM
        {
            Type = e.Type, DureeS = e.DureeS, JusquAS = e.JusquAS, Texte = e.Texte, Legende = legende,
            Noeuds = e.Noeuds ?? [], Zones = (e.Zones ?? []).Select(z => z.ToUpperInvariant()).ToList(),
            Source = e.Source, Destination = e.Destination, Protocole = e.Protocole, Port = e.Port, Etat = etat, Vitesse = e.Vitesse,
            Ligne = e.LigneMiseEnAvant, Gauche = e.Gauche, Droite = e.Droite,
        };
    }

    /// <summary>
    /// Ce qu'une zone met en avant : les cadres du schéma (par zone et par code de réseau) et les machines,
    /// d'après la topologie et, pour les segments (DMZ-WEB…), d'après politique/hosts.csv.
    /// </summary>
    private static Dictionary<string, ZoneSceneVM> Zones(SchemaVM schema, PolitiqueLaboratoire pol, string etat)
    {
        var zones = new Dictionary<string, ZoneSceneVM>(StringComparer.Ordinal);
        ZoneSceneVM Zone(string nom) => zones.TryGetValue(nom.ToUpperInvariant(), out var z) ? z : zones[nom.ToUpperInvariant()] = new ZoneSceneVM();
        foreach (var z in schema.Zones)
        {
            Zone(z.Zone).Reseaux.Add(z.Code);
            Zone(z.Code).Reseaux.Add(z.Code);
        }
        foreach (var n in schema.Noeuds.Where(n => n.Zone.Length > 0)) Zone(n.Zone).Noeuds.Add(n.Id);
        var ids = schema.Noeuds.Select(n => n.Id).ToHashSet();
        foreach (var h in pol.Hotes)
        {
            var noeud = h.Conteneur ?? h.Hote;
            var segment = etat == "B" ? h.SegmentB : h.SegmentA;
            if (ids.Contains(noeud) && segment.Length > 0 && !Zone(segment).Noeuds.Contains(noeud)) Zone(segment).Noeuds.Add(noeud);
        }
        return zones;
    }

    private static ResultatsVM Resultats(Synthese s) => new()
    {
        Illustration = s.Illustration,
        Source = s.Source,
        Remarques = s.Remarques,
        Indicateurs = s.Lignes.GroupBy(l => l.Indicateur).Select(g => new IndicateurVM
        {
            Code = g.Key,
            Libelle = g.First().Libelle,
            Unite = g.First().Unite,
            ValeurA = g.FirstOrDefault(x => x.Etat == "A")?.Valeur ?? 0,
            ValeurB = g.FirstOrDefault(x => x.Etat == "B")?.Valeur ?? 0,
        }).ToList(),
    };

    private async Task<ResumeVM> ResumeAsync(FichierScenes fichier, PolitiqueLaboratoire pol, Synthese resultats)
    {
        int Segments(string e) => pol.SegmentsDe(e).Count(s => s.Nom is not ("WAN" or "MGMT"));
        var (matA, matB) = (politiqueService.Matrice("A"), politiqueService.Matrice("B"));
        var t = fichier.Resume;
        async Task<string> S05(string e)
        {
            var r = await simulations.SimulerAsync(new DemandeSimulationDto
                { Source = t.S05.Source, Destination = t.S05.Destination, Protocole = t.S05.Protocole, Port = t.S05.Port, Etat = e });
            return r.VerdictTexte + (r.Regle is { Length: > 0 } regle ? " · " + regle : "");
        }

        var lignes = new List<LigneResumeVM>
        {
            new() { Libelle = "Segments", A = Segments("A").ToString(Fr), B = Segments("B").ToString(Fr) },
            new() { Libelle = "Flux autorisés", A = matA.NbFlux.ToString(Fr), B = matB.NbFlux.ToString(Fr) },
            new() { Libelle = "Tests générés", A = matA.NbTests.ToString(Fr), B = matB.NbTests.ToString(Fr) },
            new() { Libelle = $"S-05 ({t.S05.Source} → {t.S05.Destination}:{t.S05.Port})", A = await S05("A"), B = await S05("B") },
            new() { Libelle = "Détection", A = t.DetectionA, B = t.DetectionB },
        };
        // Indicateurs mesurés : seulement s'ils sont réels (pas d'illustration sur l'écran de fin).
        if (!resultats.Illustration)
            lignes.AddRange(Resultats(resultats).Indicateurs.Select(i => new LigneResumeVM
            {
                Libelle = i.Libelle,
                A = $"{i.ValeurA.ToString("0.##", Fr)} {i.Unite}".Trim(),
                B = $"{i.ValeurB.ToString("0.##", Fr)} {i.Unite}".Trim(),
            }));

        var equipe = (await roles.ListerCartesAsync()).OrderBy(r => r.Numero).Select(r => new CarteEquipeVM
        {
            Numero = r.Numero,
            Titre = $"Rôle {r.Numero} — {r.CourtNom}",
            Prenoms = fichier.Equipe.GetValueOrDefault(r.Numero.ToString(CultureInfo.InvariantCulture)) is { Length: > 0 } p ? p : null,
        }).ToList();
        equipe.Add(new CarteEquipeVM { Titre = "Plateforme graphique", Prenoms = fichier.Equipe.GetValueOrDefault("plateforme") is { Length: > 0 } p ? p : null });

        return new ResumeVM { Lignes = lignes, Equipe = equipe, Phrase = t.Phrase, Reproduire = t.Reproduire };
    }
}
