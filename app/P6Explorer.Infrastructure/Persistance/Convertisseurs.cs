using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Infrastructure.Persistance;

/// <summary>Conversions entre les types valeur du domaine et les colonnes de la base.</summary>
public static class Convertisseurs
{
    public static readonly ValueConverter<NoeudId, string> Noeud =
        new(v => v.Valeur, v => new NoeudId(v));

    public static readonly ValueConverter<LienId, string> Lien =
        new(v => v.Valeur, v => new LienId(v));

    // Chemin d'un paquet : liste de nœuds stockée en JSON (["kali","opnsense","nginx"]).
    public static readonly ValueConverter<List<NoeudId>?, string?> Chemin =
        new(v => v == null ? null : JsonSerializer.Serialize(v.Select(n => n.Valeur), (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)!.Select(s => new NoeudId(s)).ToList());

    public static readonly ValueComparer<List<NoeudId>?> CheminComparateur =
        new((a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v == null ? 0 : v.Aggregate(17, (h, n) => HashCode.Combine(h, n)),
            v => v == null ? null : v.ToList());

    // Points d'une polyligne du schéma : [[x, y], …] en JSON.
    public static readonly ValueConverter<List<double[]>, string> Points =
        new(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<double[]>>(v, (JsonSerializerOptions?)null) ?? new List<double[]>());

    public static readonly ValueComparer<List<double[]>> PointsComparateur =
        new((a, b) => a!.Count == b!.Count && a.Zip(b).All(p => p.First.SequenceEqual(p.Second)),
            v => v.Aggregate(17, (h, p) => HashCode.Combine(h, p.Length > 0 ? p[0] : 0, p.Length > 1 ? p[1] : 0)),
            v => v.Select(p => p.ToArray()).ToList());
}
