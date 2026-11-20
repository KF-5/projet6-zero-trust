using System.Linq.Expressions;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Topologie;

namespace P6Explorer.Domain.Interfaces;

/// <summary>Accès générique aux données : enregistré une fois pour toutes les entités (AddScoped(typeof(…))).</summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> TrouverAsync(params object[] cle);
    Task<IReadOnlyList<T>> ListerAsync(Expression<Func<T, bool>>? filtre = null);
    Task<int> CompterAsync(Expression<Func<T, bool>>? filtre = null);
    Task AjouterAsync(T entite);
    void Modifier(T entite);
    void Supprimer(T entite);
    Task<int> EnregistrerAsync();
}

/// <summary>Rôles et étapes, avec leurs commandes quand il le faut.</summary>
public interface IRoleRepository : IGenericRepository<Role>
{
    /// <summary>Les 6 rôles, avec leurs étapes (sans les commandes), triés par numéro.</summary>
    Task<IReadOnlyList<Role>> ListerAvecEtapesAsync();

    /// <summary>Nombre de commandes de premier niveau par rôle.</summary>
    Task<IReadOnlyDictionary<int, int>> CompterCommandesParRoleAsync();
}

/// <summary>Topologie d'un état (A ou B), pour le schéma et le moteur réseau.</summary>
public interface ITopologieRepository
{
    Task<EtatReseau?> ChargerEtatAsync(string code);
    Task<IReadOnlyList<Noeud>> ListerNoeudsAsync(string etat);
    Task<IReadOnlyList<Reseau>> ListerReseauxAsync(string etat);
    Task<IReadOnlyList<Lien>> ListerLiensAsync(string etat);
}
