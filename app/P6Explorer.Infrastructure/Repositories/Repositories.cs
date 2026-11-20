using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Interfaces;
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
}
