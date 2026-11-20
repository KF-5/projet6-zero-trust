using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using P6Explorer.Domain.Commandes;

namespace P6Explorer.Infrastructure.Persistance;

/// <summary>
/// Intercepteur EF Core : juste avant chaque enregistrement, remplit les propriétés shadow
/// DateCreation (à l'ajout) et DateModification (à l'ajout et à chaque modification) des entités IHorodate.
/// Au jalon 4, il écrira aussi l'historique des révisions.
/// </summary>
public sealed class IntercepteurHorodatage(TimeProvider horloge) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Horodater(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Horodater(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Horodater(DbContext? contexte)
    {
        if (contexte is null) return;
        var maintenant = horloge.GetUtcNow().UtcDateTime;
        foreach (var entree in contexte.ChangeTracker.Entries<IHorodate>())
        {
            if (entree.State == EntityState.Added)
            {
                entree.Property(P6DbContext.DateCreation).CurrentValue = maintenant;
                entree.Property(P6DbContext.DateModification).CurrentValue = maintenant;
            }
            else if (entree.State == EntityState.Modified)
            {
                entree.Property(P6DbContext.DateModification).CurrentValue = maintenant;
            }
        }
    }
}
