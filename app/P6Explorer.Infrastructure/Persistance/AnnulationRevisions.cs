using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using P6Explorer.Domain.Suivi;

namespace P6Explorer.Infrastructure.Persistance;

/// <summary>Valeurs d'un champ en texte dans l'historique, et retour au type d'origine.</summary>
public static class ValeurTexte
{
    private static readonly JsonSerializerOptions Json = new();

    public static string? Ecrire(object? valeur) => valeur switch
    {
        null => null,
        string s => s,
        _ => JsonSerializer.Serialize(valeur, valeur.GetType(), Json)
    };

    public static object? Lire(string? texte, Type type)
    {
        if (texte is null) return null;
        if (type == typeof(string)) return texte;
        return JsonSerializer.Deserialize(texte, type, Json);
    }
}

/// <summary>
/// Retour à une version précédente : applique à l'envers un lot de révisions (un enregistrement).
/// Modification : remet les valeurs « avant ». Création : supprime l'élément créé. Suppression : le recrée
/// (avec un nouvel identifiant), enfants compris quand ils ont été supprimés dans le même lot.
/// L'annulation est elle-même enregistrée dans l'historique.
/// </summary>
public sealed class AnnulationRevisions(P6DbContext contexte)
{
    public async Task<string> AnnulerAsync(Guid lot)
    {
        var lignes = await contexte.Revisions.AsNoTracking().Where(r => r.Lot == lot).OrderBy(r => r.Id).ToListAsync();
        if (lignes.Count == 0) return "Modification introuvable dans l'historique.";

        var recrees = new Dictionary<(string, int), object>();
        var nombre = 0;
        // Les parents d'abord (étape, commande), les effets ensuite : leurs clés étrangères pointent vers les parents recréés.
        foreach (var groupe in lignes.GroupBy(r => (r.Entite, r.EntiteId))
                     .OrderBy(g => Rang(TypeDe(g.Key.Entite)))
                     .ThenBy(g => g.Any(r => r.Champ == "ParentId" && r.Avant is not null && r.Avant != "null") ? 1 : 0))   // commande avant ses sous-commandes
        {
            var type = TypeDe(groupe.Key.Entite);
            if (type is null) continue;
            var champs = groupe.ToList();
            if (champs.Any(c => c.Champ == IntercepteurHorodatage.Creation))
            {
                var entite = await contexte.FindAsync(type.ClrType, groupe.Key.EntiteId);
                if (entite is not null) { contexte.Remove(entite); nombre++; }
            }
            else if (champs.Any(c => c.Champ == IntercepteurHorodatage.Suppression))
            {
                var entite = Activator.CreateInstance(type.ClrType)!;
                foreach (var c in champs.Where(c => c.Champ != IntercepteurHorodatage.Suppression))
                {
                    var p = type.FindProperty(c.Champ);
                    if (p?.PropertyInfo is null || p.IsPrimaryKey()) continue;
                    p.PropertyInfo.SetValue(entite, ValeurTexte.Lire(c.Avant, p.ClrType));
                }
                RebrancherParents(type, entite, recrees);
                contexte.Add(entite);
                recrees[(Racine(type), groupe.Key.EntiteId)] = entite;
                nombre++;
            }
            else
            {
                var entite = await contexte.FindAsync(type.ClrType, groupe.Key.EntiteId);
                if (entite is null) continue;
                var entree = contexte.Entry(entite);
                foreach (var c in champs)
                {
                    var p = type.FindProperty(c.Champ);
                    if (p is null || p.IsPrimaryKey()) continue;
                    entree.Property(c.Champ).CurrentValue = ValeurTexte.Lire(c.Avant, p.ClrType);
                }
                nombre++;
            }
        }
        if (nombre == 0) return "Rien à annuler : l'élément a déjà été modifié ou supprimé depuis.";
        await contexte.SaveChangesAsync();
        return $"Modification annulée ({nombre} élément{(nombre > 1 ? "s" : "")}).";
    }

    /// <summary>Un enfant recréé (effet d'une commande recréée…) reprend son parent recréé, qui a un nouvel identifiant.</summary>
    private void RebrancherParents(IEntityType type, object entite, Dictionary<(string, int), object> recrees)
    {
        foreach (var fk in type.GetForeignKeys())
        {
            var propriete = fk.Properties.Single();
            if (propriete.PropertyInfo?.GetValue(entite) is not int ancien) continue;
            if (!recrees.TryGetValue((Racine(fk.PrincipalEntityType), ancien), out var parent)) continue;
            fk.DependentToPrincipal?.PropertyInfo?.SetValue(entite, parent);
        }
    }

    private static int Rang(IEntityType? type) => type is null ? 9 : Racine(type) switch
    {
        "Etape" => 0,
        "Commande" => 1,
        "Effet" => 3,
        _ => 2
    };

    private IEntityType? TypeDe(string nom) => contexte.Model.GetEntityTypes().FirstOrDefault(t => t.ClrType.Name == nom && !t.IsOwned());

    private static string Racine(IReadOnlyEntityType type) => type.GetRootType().ClrType.Name;
}
