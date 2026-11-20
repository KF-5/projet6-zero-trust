using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using P6Explorer.Application.Services;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Suivi;

namespace P6Explorer.Infrastructure.Persistance;

/// <summary>
/// Intercepteur EF Core (SaveChangesInterceptor), deux rôles :
/// 1. remplit les propriétés shadow DateCreation et DateModification des entités IHorodate ;
/// 2. écrit l'historique (Revision) de chaque ajout, modification ou suppression d'une entité IRevisable :
///    qui, quand, quel champ, valeur avant et après. Les ajouts n'ont leur identifiant qu'après l'enregistrement :
///    l'historique est donc préparé avant (SavingChanges) et écrit juste après (SavedChanges).
/// </summary>
public sealed class IntercepteurHorodatage(TimeProvider horloge, IUtilisateurCourant utilisateur) : SaveChangesInterceptor
{
    public const string Creation = "(création)";
    public const string Suppression = "(suppression)";

    private sealed record Changement(EntityEntry Entree, EntityState Etat, List<(string Champ, string? Avant, string? Apres)> Champs, int? EtapeId);

    private readonly ConditionalWeakTable<DbContext, List<Changement>> _enAttente = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Preparer(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Preparer(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (Ecrire(eventData.Context)) eventData.Context!.SaveChanges();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (Ecrire(eventData.Context)) await eventData.Context!.SaveChangesAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Preparer(DbContext? contexte)
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

        if (contexte is not P6DbContext { JournaliserRevisions: true } p6) return;
        var changements = new List<Changement>();
        foreach (var entree in contexte.ChangeTracker.Entries().Where(e => e.Entity is IRevisable
                     && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var champs = new List<(string, string?, string?)>();
            var proprietes = entree.Properties.Where(p => !p.Metadata.IsPrimaryKey() && !p.Metadata.IsShadowProperty()).ToList();
            switch (entree.State)
            {
                case EntityState.Added:
                    champs.Add((Creation, null, Resumer(entree.Entity)));
                    break;
                case EntityState.Modified:
                    foreach (var p in proprietes.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)))
                        champs.Add((p.Metadata.Name, ValeurTexte.Ecrire(p.OriginalValue), ValeurTexte.Ecrire(p.CurrentValue)));
                    break;
                case EntityState.Deleted:
                    champs.Add((Suppression, Resumer(entree.Entity), null));
                    foreach (var p in proprietes)
                        champs.Add((p.Metadata.Name, ValeurTexte.Ecrire(p.OriginalValue), null));
                    break;
            }
            if (champs.Count > 0) changements.Add(new Changement(entree, entree.State, champs, EtapeDe(p6, entree)));
        }
        if (changements.Count > 0) _enAttente.AddOrUpdate(contexte, changements);
    }

    /// <summary>Ajoute les révisions préparées ; vrai s'il faut les enregistrer.</summary>
    private bool Ecrire(DbContext? contexte)
    {
        if (contexte is not P6DbContext p6 || !_enAttente.TryGetValue(contexte, out var changements)) return false;
        _enAttente.Remove(contexte);
        var date = horloge.GetUtcNow().UtcDateTime;
        var auteur = utilisateur.Identifiant;
        var lot = Guid.NewGuid();   // un enregistrement = un lot : on l'annule d'un coup
        foreach (var c in changements)
        {
            var id = c.Entree.Properties.First(p => p.Metadata.IsPrimaryKey()).CurrentValue is int i ? i : 0;
            var etapeId = c.EtapeId ?? (c.Entree.Entity is Etape e ? e.Id : null);
            foreach (var (champ, avant, apres) in c.Champs)
                p6.Revisions.Add(new Revision
                {
                    Lot = lot, Entite = c.Entree.Metadata.ClrType.Name, EntiteId = id, EtapeId = etapeId,
                    Champ = champ, Avant = Tronquer(avant), Apres = Tronquer(apres), AuteurId = auteur, Date = date
                });
        }
        return true;
    }

    private static int? EtapeDe(P6DbContext contexte, EntityEntry entree) => entree.Entity switch
    {
        Etape e => e.Id == 0 ? null : e.Id,
        Commande c => c.EtapeId,
        Panne p => p.EtapeId,
        CritereFin f => f.EtapeId,
        Effet f => f.Commande?.EtapeId ?? contexte.Commandes.Where(c => c.Id == f.CommandeId).Select(c => (int?)c.EtapeId).FirstOrDefault(),
        _ => null
    };

    private static string Resumer(object entite) => entite switch
    {
        Etape e => $"Étape {e.Code} : {e.Titre}",
        Commande c => $"Commande {c.Code} : {c.Texte.Split('\n')[0]}",
        Effet f => $"Effet {f.GetType().Name} : {f.Resumer()}",
        Panne p => $"Panne : {p.Symptome}",
        CritereFin f => $"Critère : {f.Texte}",
        _ => entite.GetType().Name
    };

    private static string? Tronquer(string? t) => t is { Length: > 4000 } ? t[..4000] : t;
}
