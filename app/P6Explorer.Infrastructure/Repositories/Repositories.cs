using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Interfaces;
using P6Explorer.Domain.Moteur;
using P6Explorer.Domain.Suivi;
using P6Explorer.Domain.Topologie;
using P6Explorer.Infrastructure.Persistance;

namespace P6Explorer.Infrastructure.Repositories;

/// <summary>Repository générique : enregistré une seule fois pour toutes les entités (AddScoped(typeof(…))).</summary>
public class GenericRepository<T>(P6DbContext contexte) : IGenericRepository<T> where T : class
{
    protected P6DbContext Contexte { get; } = contexte;
    protected DbSet<T> Table => Contexte.Set<T>();

    public async Task<T?> TrouverAsync(params object[] cle) => await Table.FindAsync(cle);

    public async Task<IReadOnlyList<T>> ListerAsync(Expression<Func<T, bool>>? filtre = null)
        => await (filtre is null ? Table : Table.Where(filtre)).AsNoTracking().ToListAsync();

    public Task<int> CompterAsync(Expression<Func<T, bool>>? filtre = null)
        => filtre is null ? Table.CountAsync() : Table.CountAsync(filtre);

    public async Task AjouterAsync(T entite) => await Table.AddAsync(entite);

    public void Modifier(T entite) => Table.Update(entite);

    public void Supprimer(T entite) => Table.Remove(entite);

    public Task<int> EnregistrerAsync() => Contexte.SaveChangesAsync();

    /// <summary>Pagination commune : Skip / Take après un comptage.</summary>
    protected static async Task<Page<TE>> PaginerAsync<TE>(IQueryable<TE> requete, int page, int taille)
    {
        page = Math.Max(1, page);
        var total = await requete.CountAsync();
        var elements = await requete.Skip((page - 1) * taille).Take(taille).ToListAsync();
        return new Page<TE>(elements, page, taille, total);
    }
}

public class RoleRepository(P6DbContext contexte) : GenericRepository<Role>(contexte), IRoleRepository
{
    public async Task<IReadOnlyList<Role>> ListerAvecEtapesAsync()
        => await Table.AsNoTracking()
            .Include(r => r.Etapes.OrderBy(e => e.Numero))      // chargement eager, filtré et trié
            .OrderBy(r => r.Numero)
            .ToListAsync();

    public async Task<IReadOnlyDictionary<int, int>> CompterCommandesParRoleAsync()
    {
        // LINQ en syntaxe de requête : jointure et regroupement, traduits en un seul SELECT … GROUP BY.
        var requete = from c in Contexte.Commandes
                      join e in Contexte.Etapes on c.EtapeId equals e.Id
                      where c.ParentId == null
                      group c by e.RoleNumero into g
                      select new { Role = g.Key, Nombre = g.Count() };
        return await requete.ToDictionaryAsync(x => x.Role, x => x.Nombre);
    }
}

public class EtapeRepository(P6DbContext contexte) : GenericRepository<Etape>(contexte), IEtapeRepository
{
    public Task<Etape?> ChargerPageAsync(int role, int numero)
        => Table.AsNoTracking()
            .Include(e => e.Role)
            .Include(e => e.Pannes.OrderBy(p => p.Ordre))
            .Include(e => e.Criteres.OrderBy(c => c.Ordre))
            .Include(e => e.Preuves.OrderBy(p => p.Date))
            .Include(e => e.Commandes.Where(c => c.ParentId == null).OrderBy(c => c.Ordre))
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.RoleNumero == role && e.Numero == numero);

    public async Task<IReadOnlyList<Commande>> ListerCommandesAsync(int etapeId)
        => await Contexte.Commandes.AsNoTracking()
            .Where(c => c.EtapeId == etapeId && c.ParentId == null)
            .Include(c => c.Effets.OrderBy(f => f.Ordre))
            .Include(c => c.SousCommandes.OrderBy(s => s.Ordre)).ThenInclude(s => s.Effets.OrderBy(f => f.Ordre))
            .OrderBy(c => c.Ordre)
            .AsSplitQuery()
            .ToListAsync();

    public Task<Etape?> ChargerPourEditionAsync(int id)
        => Table.Include(e => e.Pannes).Include(e => e.Criteres).FirstOrDefaultAsync(e => e.Id == id);

    public Task<Commande?> ChargerCommandeAsync(int id)
        => Contexte.Commandes
            .Include(c => c.Etape)
            .Include(c => c.Effets.OrderBy(f => f.Ordre))
            .Include(c => c.SousCommandes).ThenInclude(s => s.Effets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);

    public void SupprimerCommande(Commande commande)
    {
        foreach (var s in commande.SousCommandes.ToList())
        {
            Contexte.Effets.RemoveRange(s.Effets);
            Contexte.Commandes.Remove(s);
        }
        Contexte.Effets.RemoveRange(commande.Effets);
        Contexte.Commandes.Remove(commande);
    }

    public void CopierValeurs(object cible, object source) => Contexte.Entry(cible).CurrentValues.SetValues(source);

    public async Task<IReadOnlyDictionary<int, DateTime>> DatesModificationAsync(int role)
        => await Table.Where(e => e.RoleNumero == role)
            .Select(e => new { e.Id, Date = EF.Property<DateTime>(e, P6DbContext.DateModification) })
            .ToDictionaryAsync(x => x.Id, x => x.Date);
}

public class TopologieRepository(P6DbContext contexte) : ITopologieRepository
{
    public Task<EtatReseau?> ChargerEtatAsync(string code)
        => contexte.EtatsReseau.AsNoTracking()
            .Include(e => e.Regles.OrderBy(r => r.Interface).ThenBy(r => r.Ordre))
            .Include(e => e.ReglesNat)
            .FirstOrDefaultAsync(e => e.Code == code);

    public async Task<IReadOnlyList<Noeud>> ListerNoeudsAsync(string etat)
        => await contexte.Noeuds.AsNoTracking()
            .Include(n => n.Interfaces.Where(i => i.EtatReseauCode == null || i.EtatReseauCode == etat))
            .OrderBy(n => n.Y).ThenBy(n => n.X)
            .ToListAsync();

    public async Task<IReadOnlyList<Reseau>> ListerReseauxAsync(string etat)
        => await contexte.Reseaux.AsNoTracking()
            .Where(r => r.EtatReseauCode == null || r.EtatReseauCode == etat)
            .OrderBy(r => r.Zone).ThenBy(r => r.Vlan)
            .ToListAsync();

    public async Task<IReadOnlyList<Lien>> ListerLiensAsync(string etat)
        => await contexte.Liens.AsNoTracking()
            .Where(l => l.EtatReseauCode == null || l.EtatReseauCode == etat)
            .ToListAsync();

    public async Task<TopologieSimulee> ChargerPourMoteurAsync(string etat)
    {
        var noeuds = await contexte.Noeuds.AsNoTracking().Include(n => n.Interfaces).ToListAsync();
        var reseaux = await contexte.Reseaux.AsNoTracking().ToListAsync();
        var regles = await contexte.Regles.AsNoTracking().Where(r => r.EtatReseauCode == etat).ToListAsync();
        var nat = await contexte.ReglesNat.AsNoTracking().Where(r => r.EtatReseauCode == etat).ToListAsync();
        return TopologieSimulee.Construire(etat, noeuds, reseaux, regles, nat);
    }
}

public class RevisionRepository(P6DbContext contexte) : IRevisionRepository
{
    public async Task<Page<Revision>> ListerAsync(int etapeId, int page, int taille)
    {
        page = Math.Max(1, page);
        var requete = contexte.Revisions.AsNoTracking().Where(r => r.EtapeId == etapeId)
            .OrderByDescending(r => r.Date).ThenBy(r => r.Id);
        var total = await requete.CountAsync();
        return new Page<Revision>(await requete.Skip((page - 1) * taille).Take(taille).ToListAsync(), page, taille, total);
    }

    public Task<string> AnnulerLotAsync(Guid lot) => new AnnulationRevisions(contexte).AnnulerAsync(lot);
}

public class QuizRepository(P6DbContext contexte) : IQuizRepository
{
    public Task<Quiz?> ChargerParEtapeAsync(int etapeId)
        => contexte.Quiz
            .Include(q => q.Etape)
            .Include(q => q.Questions.OrderBy(x => x.Ordre)).ThenInclude(x => x.Choix.OrderBy(c => c.Ordre))
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.EtapeId == etapeId);

    public Task<Quiz?> ChargerAsync(int quizId)
        => contexte.Quiz
            .Include(q => q.Etape)
            .Include(q => q.Questions.OrderBy(x => x.Ordre)).ThenInclude(x => x.Choix.OrderBy(c => c.Ordre))
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == quizId);

    public async Task AjouterTentativeAsync(Tentative tentative)
    {
        await contexte.Tentatives.AddAsync(tentative);
        await contexte.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Tentative>> TentativesAsync(string utilisateurId, int quizId)
        => await contexte.Tentatives.AsNoTracking().Where(t => t.UtilisateurId == utilisateurId && t.QuizId == quizId)
            .OrderByDescending(t => t.Date).Take(10).ToListAsync();

    public async Task<IReadOnlyList<MeilleurScore>> MeilleursScoresAsync()
    {
        // join + group by + Max / Count : meilleur score de chaque membre, étape par étape.
        var requete = from t in contexte.Tentatives
                      join q in contexte.Quiz on t.QuizId equals q.Id
                      group new { t, q } by new { t.UtilisateurId, q.EtapeId, q.Seuil } into g
                      select new
                      {
                          g.Key.UtilisateurId, g.Key.EtapeId, g.Key.Seuil,
                          Score = g.Max(x => x.t.Score),
                          Date = g.Max(x => x.t.Date),
                          Nombre = g.Count()
                      };
        return (await requete.ToListAsync())
            .Select(x => new MeilleurScore(x.UtilisateurId, x.EtapeId, x.Score, x.Date, x.Nombre, x.Seuil)).ToList();
    }

    public async Task<IReadOnlySet<int>> EtapesMaitriseesAsync(string utilisateurId)
        => (await contexte.Tentatives.Where(t => t.UtilisateurId == utilisateurId && t.Score >= t.Quiz!.Seuil)
            .Select(t => t.Quiz!.EtapeId).Distinct().ToListAsync()).ToHashSet();

    public Task<Question?> ChargerQuestionAsync(int id)
        => contexte.Questions.Include(q => q.Choix).Include(q => q.Quiz).FirstOrDefaultAsync(q => q.Id == id);

    public Task<int> EnregistrerAsync() => contexte.SaveChangesAsync();
}

public class ProgressionRepository(P6DbContext contexte) : IProgressionRepository
{
    public async Task<IReadOnlySet<int>> CommandesComprisesAsync(string utilisateurId, int etapeId)
        => (await contexte.Progressions.Where(p => p.UtilisateurId == utilisateurId && p.Comprise && p.Commande!.EtapeId == etapeId)
            .Select(p => p.CommandeId).ToListAsync()).ToHashSet();

    public async Task BasculerAsync(string utilisateurId, int commandeId, DateTime date)
    {
        var p = await contexte.Progressions.FirstOrDefaultAsync(x => x.UtilisateurId == utilisateurId && x.CommandeId == commandeId);
        if (p is null) contexte.Progressions.Add(new Progression { UtilisateurId = utilisateurId, CommandeId = commandeId, Comprise = true, Date = date });
        else { p.Comprise = !p.Comprise; p.Date = date; }
        await contexte.SaveChangesAsync();
    }

    public async Task<IReadOnlyDictionary<int, int>> CompterParRoleAsync(string utilisateurId)
        => await (from p in contexte.Progressions
                  join c in contexte.Commandes on p.CommandeId equals c.Id
                  join e in contexte.Etapes on c.EtapeId equals e.Id
                  where p.UtilisateurId == utilisateurId && p.Comprise && c.ParentId == null
                  group p by e.RoleNumero into g
                  select new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
}

public class DiscussionRepository(P6DbContext contexte) : GenericRepository<Fil>(contexte), IDiscussionRepository
{
    public Task<Page<Fil>> ListerAsync(int? commandeId, StatutFiltreFil statut, int page, int taille)
    {
        var requete = Contexte.Fils.AsNoTracking().Include(f => f.Commande).ThenInclude(c => c!.Etape)
            .Include(f => f.Messages)
            .Where(f => commandeId == null || f.CommandeId == commandeId)
            .Where(f => statut == StatutFiltreFil.Tous
                        || (statut == StatutFiltreFil.Ouverts && f.Statut == StatutFil.Ouvert)
                        || (statut == StatutFiltreFil.Resolus && f.Statut == StatutFil.Resolu))
            .OrderBy(f => f.Statut).ThenByDescending(f => f.Date);
        return PaginerAsync(requete, page, taille);
    }

    public async Task<Fil?> ChargerAsync(int id)
    {
        var fil = await Contexte.Fils.Include(f => f.Commande).ThenInclude(c => c!.Etape).FirstOrDefaultAsync(f => f.Id == id);
        if (fil is null) return null;
        // Chargement explicite : les messages ne sont lus qu'une fois le fil trouvé, dans l'ordre.
        await Contexte.Entry(fil).Collection(f => f.Messages).Query().OrderBy(m => m.Date).LoadAsync();
        return fil;
    }

    public async Task AjouterMessageAsync(Message message) => await Contexte.Messages.AddAsync(message);

    public async Task<IReadOnlyDictionary<int, int>> OuvertsParEtapeAsync(int role)
        => await Contexte.Fils.Where(f => f.Statut == StatutFil.Ouvert && f.Commande!.Etape!.RoleNumero == role)
            .GroupBy(f => f.Commande!.EtapeId).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N);

    public async Task<IReadOnlyDictionary<int, int>> OuvertsParCommandeAsync(int etapeId)
        => await Contexte.Fils.Where(f => f.Statut == StatutFil.Ouvert && f.Commande!.EtapeId == etapeId)
            .GroupBy(f => f.CommandeId).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N);
}

public class SimulationRepository(P6DbContext contexte) : GenericRepository<Simulation>(contexte), ISimulationRepository
{
    public Task<Page<Simulation>> ListerAsync(int page, int taille)
        => PaginerAsync(Table.AsNoTracking().OrderByDescending(s => s.Date).ThenByDescending(s => s.Id), page, taille);

    public async Task<IReadOnlyList<Simulation>> ListerDemoAsync()
        => await Table.AsNoTracking().Where(s => s.PourDemo).OrderBy(s => s.OrdreDemo).ThenBy(s => s.Id).ToListAsync();
}

public class GlossaireRepository(P6DbContext contexte) : IGlossaireRepository
{
    public async Task<IReadOnlyList<TermeGlossaire>> ListerAsync()
        => await contexte.Glossaire.AsNoTracking().Include(t => t.Etapes).OrderBy(t => t.Terme).ToListAsync();

    public async Task<IReadOnlyList<TermeGlossaire>> ListerPourEtapeAsync(int etapeId)
        => await contexte.Glossaire.AsNoTracking().Where(t => t.Etapes.Any(e => e.Id == etapeId)).OrderBy(t => t.Terme).ToListAsync();
}
