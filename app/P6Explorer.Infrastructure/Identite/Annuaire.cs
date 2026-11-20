using Microsoft.EntityFrameworkCore;
using P6Explorer.Application.Services;
using P6Explorer.Domain.Interfaces;
using P6Explorer.Infrastructure.Persistance;

namespace P6Explorer.Infrastructure.Identite;

/// <summary>Noms affichés des comptes, lus dans la table d'Identity (AspNetUsers).</summary>
public class Annuaire(P6DbContext contexte) : IAnnuaire
{
    public async Task<IReadOnlyDictionary<string, string>> NomsAsync(IEnumerable<string> identifiants)
    {
        var ids = identifiants.Distinct().ToList();
        return await contexte.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NomAffiche ?? u.Email ?? u.UserName ?? u.Id);
    }

    public async Task<IReadOnlyList<(string Id, string Nom, int? NumeroRole)>> MembresAsync()
        => (await contexte.Users.AsNoTracking().OrderBy(u => u.NumeroRole).ThenBy(u => u.Email)
                .Select(u => new { u.Id, Nom = u.NomAffiche ?? u.Email ?? u.UserName ?? u.Id, u.NumeroRole }).ToListAsync())
            .Select(u => (u.Id, u.Nom, u.NumeroRole)).ToList();
}

/// <summary>Hors requête web (amorçage, tests) : aucun auteur.</summary>
public sealed class AucunUtilisateur : IUtilisateurCourant
{
    public string? Identifiant => null;
}
