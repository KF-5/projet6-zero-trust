using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using P6Explorer.Application.ViewModels;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Exceptions;
using P6Explorer.Domain.Interfaces;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Application.Services;

/// <summary>Modification du contenu par le responsable de chaque rôle (CRUD étapes, commandes, effets, preuves, historique).</summary>
public interface IEditionService
{
    Task<int?> RoleDeEtapeAsync(int etapeId);
    Task<int?> RoleDeCommandeAsync(int commandeId);

    Task<EtapeFormVM?> EtapeAsync(int id);
    Task ModifierEtapeAsync(EtapeFormVM formulaire);

    Task<CommandeFormVM?> CommandeAsync(int id);
    Task<CommandeFormVM> NouvelleCommandeAsync(int etapeId);
    Task<int> EnregistrerCommandeAsync(CommandeFormVM formulaire);
    Task<(int Role, int Etape)> SupprimerCommandeAsync(int id);

    Task<EffetFormVM?> EffetAsync(int commandeId, int effetId);
    Task EnregistrerEffetAsync(EffetFormVM formulaire);
    Task SupprimerEffetAsync(int commandeId, int effetId);

    Task AjouterPreuveAsync(int etapeId, string nomFichier, string nomOriginal, string typeMime, long taille, string? legende, string? auteurId);
    Task<string?> SupprimerPreuveAsync(int preuveId);

    Task<HistoriqueVM?> HistoriqueAsync(int etapeId, int page);
    Task<string> AnnulerAsync(Guid lot);
}

public class EditionService(
    IEtapeRepository etapes,
    IGenericRepository<Preuve> preuves,
    IRevisionRepository revisions,
    IAnnuaire annuaire,
    IMapper mapper,
    TimeProvider horloge) : IEditionService
{
    public const int TaillePageHistorique = 20;

    /// <summary>Lecture et écriture des effets en JSON polymorphe (propriété « type »), comme dans donnees/commandes.</summary>
    public static readonly JsonSerializerOptions JsonEffets = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<int?> RoleDeEtapeAsync(int etapeId) => (await etapes.TrouverAsync(etapeId))?.RoleNumero;

    public async Task<int?> RoleDeCommandeAsync(int commandeId) => (await etapes.ChargerCommandeAsync(commandeId))?.Etape?.RoleNumero;

    public async Task<EtapeFormVM?> EtapeAsync(int id)
        => await etapes.TrouverAsync(id) is { } e ? mapper.Map<EtapeFormVM>(e) : null;

    public async Task ModifierEtapeAsync(EtapeFormVM f)
    {
        var e = await etapes.ChargerPourEditionAsync(f.Id) ?? throw new EntiteIntrouvableException("Étape", f.Id);
        e.Titre = f.Titre.Trim();
        e.Quand = Vide(f.Quand);
        e.Fait = f.Fait;
        e.Statut = f.Statut;
        e.But = f.But.Trim();
        e.Pourquoi = f.Pourquoi.Trim();
        e.MiseAJour = Vide(f.MiseAJour);
        e.Livrable = Vide(f.Livrable);
        await etapes.EnregistrerAsync();
    }

    public async Task<CommandeFormVM?> CommandeAsync(int id)
    {
        var c = await etapes.ChargerCommandeAsync(id);
        if (c?.Etape is null) return null;
        var f = mapper.Map<CommandeFormVM>(c);
        f.RoleNumero = c.Etape.RoleNumero;
        f.EtapeNumero = c.Etape.Numero;
        f.Effets = c.Effets.OrderBy(x => x.Ordre).Select(x => new EffetLigneVM
        {
            Id = x.Id, Ordre = x.Ordre, Type = x.GetType().Name, Resume = x.Resumer(), Automatique = x.Automatique
        }).ToList();
        return f;
    }

    public async Task<CommandeFormVM> NouvelleCommandeAsync(int etapeId)
    {
        var e = await etapes.TrouverAsync(etapeId) ?? throw new EntiteIntrouvableException("Étape", etapeId);
        var nombre = await etapes.ListerCommandesAsync(etapeId);
        return new CommandeFormVM { EtapeId = etapeId, RoleNumero = e.RoleNumero, EtapeNumero = e.Numero, Ordre = nombre.Count + 1 };
    }

    public async Task<int> EnregistrerCommandeAsync(CommandeFormVM f)
    {
        Commande c;
        if (f.Id == 0)
        {
            var e = await etapes.ChargerPourEditionAsync(f.EtapeId) ?? throw new EntiteIntrouvableException("Étape", f.EtapeId);
            var existantes = await etapes.ListerCommandesAsync(f.EtapeId);
            var numero = existantes.Select(x => int.TryParse(x.Code.Split("-c").LastOrDefault(), out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
            c = new Commande { EtapeId = e.Id, Code = $"{e.Code}-c{numero:00}" };
            // Une commande ajoutée a au moins un effet visible (règle 1) : la machine où l'on tape.
            c.Effets.Add(new Surbrillance { Noeud = new NoeudId(f.Ou), Texte = "Commande ajoutée à la main : effets à préciser.", Ordre = 1 });
            e.Commandes.Add(c);
        }
        else c = await etapes.ChargerCommandeAsync(f.Id) ?? throw new EntiteIntrouvableException("Commande", f.Id);

        c.Ordre = f.Ordre;
        c.Ou = new NoeudId(f.Ou);
        c.Shell = f.Shell;
        c.Compte = Vide(f.Compte);
        if (c.Texte != f.Texte.TrimEnd()) c.TexteGuide ??= c.Texte.Length > 0 ? c.Texte : null;
        c.Texte = f.Texte.TrimEnd();
        c.Explication = f.Explication.Trim();
        c.Sortie = Vide(f.Sortie);
        c.EstTest = f.EstTest;
        await etapes.EnregistrerAsync();
        return c.Id;
    }

    public async Task<(int Role, int Etape)> SupprimerCommandeAsync(int id)
    {
        var c = await etapes.ChargerCommandeAsync(id) ?? throw new EntiteIntrouvableException("Commande", id);
        var position = (c.Etape!.RoleNumero, c.Etape.Numero);
        // Effets et sous-commandes chargés : leur suppression est suivie, donc écrite dans l'historique et annulable.
        etapes.SupprimerCommande(c);
        await etapes.EnregistrerAsync();
        return position;
    }

    public async Task<EffetFormVM?> EffetAsync(int commandeId, int effetId)
    {
        var c = await etapes.ChargerCommandeAsync(commandeId);
        if (c is null) return null;
        if (effetId == 0)
            return new EffetFormVM
            {
                CommandeId = commandeId, CommandeCode = c.Code,
                Json = JsonSerializer.Serialize<Effet>(new Paquet { De = c.Ou, Vers = NoeudId.Opnsense, Proto = Domain.Enumerations.Protocole.Https, Port = 443 }, JsonEffets)
            };
        var e = c.Effets.FirstOrDefault(x => x.Id == effetId);
        return e is null ? null : new EffetFormVM { Id = e.Id, CommandeId = commandeId, CommandeCode = c.Code, Json = JsonSerializer.Serialize(e, JsonEffets) };
    }

    public async Task EnregistrerEffetAsync(EffetFormVM f)
    {
        var c = await etapes.ChargerCommandeAsync(f.CommandeId) ?? throw new EntiteIntrouvableException("Commande", f.CommandeId);
        var nouveau = LireEffet(f.Json);
        nouveau.Automatique = false;   // relu par une personne
        var ancien = c.Effets.FirstOrDefault(x => x.Id == f.Id);
        if (ancien is null)
        {
            nouveau.Ordre = c.Effets.Select(x => x.Ordre).DefaultIfEmpty(0).Max() + 1;
            c.Effets.Add(nouveau);
        }
        else if (ancien.GetType() == nouveau.GetType())
        {
            nouveau.Id = ancien.Id;
            nouveau.CommandeId = ancien.CommandeId;
            if (nouveau.Ordre == 0) nouveau.Ordre = ancien.Ordre;
            etapes.CopierValeurs(ancien, nouveau);
        }
        else
        {
            nouveau.Ordre = ancien.Ordre;
            c.Effets.Remove(ancien);
            c.Effets.Add(nouveau);
        }
        await etapes.EnregistrerAsync();
    }

    public async Task SupprimerEffetAsync(int commandeId, int effetId)
    {
        var c = await etapes.ChargerCommandeAsync(commandeId) ?? throw new EntiteIntrouvableException("Commande", commandeId);
        var e = c.Effets.FirstOrDefault(x => x.Id == effetId) ?? throw new EntiteIntrouvableException("Effet", effetId);
        if (c.Effets.Count == 1) throw new ExceptionMetier("Une commande garde au moins un effet visible : modifie celui-ci plutôt que de le supprimer.");
        c.Effets.Remove(e);
        await etapes.EnregistrerAsync();
    }

    public static Effet LireEffet(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Effet>(json, JsonEffets) ?? throw new ExceptionMetier("Effet vide.");
        }
        catch (JsonException ex)
        {
            throw new ExceptionMetier($"JSON invalide : {ex.Message.Split(". Path")[0]}. Le champ « type » doit valoir paquet, surbrillance, service, fichier, journal, etatNoeud, etatLien, attribuerIp, changementRegle, instantane, alerteIds, resultatTest, changementEtat ou conteneurReseau.");
        }
        catch (NotSupportedException)
        {
            throw new ExceptionMetier("Le champ « type » est obligatoire et doit être écrit en premier (par exemple « \"type\": \"paquet\" »).");
        }
    }

    public async Task AjouterPreuveAsync(int etapeId, string nomFichier, string nomOriginal, string typeMime, long taille, string? legende, string? auteurId)
    {
        await preuves.AjouterAsync(new Preuve
        {
            EtapeId = etapeId, NomFichier = nomFichier, NomOriginal = nomOriginal, TypeMime = typeMime, Taille = taille,
            Legende = Vide(legende), AuteurId = auteurId, Date = horloge.GetUtcNow().UtcDateTime
        });
        await preuves.EnregistrerAsync();
    }

    public async Task<string?> SupprimerPreuveAsync(int preuveId)
    {
        var p = await preuves.TrouverAsync(preuveId);
        if (p is null) return null;
        preuves.Supprimer(p);
        await preuves.EnregistrerAsync();
        return p.NomFichier;
    }

    public async Task<HistoriqueVM?> HistoriqueAsync(int etapeId, int page)
    {
        var e = await etapes.TrouverAsync(etapeId);
        if (e is null) return null;
        var p = await revisions.ListerAsync(etapeId, page, TaillePageHistorique);
        var noms = await annuaire.NomsAsync(p.Elements.Where(r => r.AuteurId is not null).Select(r => r.AuteurId!));
        return new HistoriqueVM
        {
            EtapeId = etapeId, RoleNumero = e.RoleNumero, EtapeNumero = e.Numero, Titre = e.Titre,
            Lots = new PageVM<LotRevisionVM>
            {
                Page = p.Numero, NombrePages = p.NombrePages, Total = p.Total,
                Elements = p.Elements.GroupBy(r => r.Lot).Select(g => new LotRevisionVM
                {
                    Lot = g.Key, Date = g.Max(r => r.Date),
                    Auteur = g.First().AuteurId is { } a ? noms.GetValueOrDefault(a, "compte supprimé") : "système",
                    Element = string.Join(", ", g.Select(r => $"{NomEntite(r.Entite)} n° {r.EntiteId}").Distinct()),
                    Champs = g.Select(r => new ChampRevisionVM { Champ = r.Champ, Avant = r.Avant, Apres = r.Apres }).ToList()
                }).OrderByDescending(l => l.Date).ToList()
            }
        };
    }

    public Task<string> AnnulerAsync(Guid lot) => revisions.AnnulerLotAsync(lot);

    private static string NomEntite(string entite) => entite switch
    {
        "Etape" => "étape", "Commande" => "commande", "Panne" => "panne", "CritereFin" => "critère", _ => $"effet {entite}"
    };

    private static string? Vide(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
}
