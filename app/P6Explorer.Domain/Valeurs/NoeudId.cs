using System.Text.Json;
using System.Text.Json.Serialization;

namespace P6Explorer.Domain.Valeurs;

/// <summary>
/// Identifiant d'une machine du schéma (« opnsense », « nginx », « hote »…).
/// Un type valeur plutôt qu'une chaîne : impossible de confondre une machine avec un texte quelconque.
/// </summary>
[JsonConverter(typeof(NoeudIdJsonConverter))]
public readonly record struct NoeudId
{
    public string Valeur { get; }

    public NoeudId(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            throw new ArgumentException("Un identifiant de nœud ne peut pas être vide.", nameof(valeur));
        Valeur = valeur.Trim().ToLowerInvariant();
    }

    public override string ToString() => Valeur;

    public static implicit operator string(NoeudId id) => id.Valeur;

    // Machines du laboratoire (plateforme réelle, ECARTS.md E31–E42).
    public static readonly NoeudId Hote = new("hote");             // PC Windows, VMware, 10.99.0.2
    public static readonly NoeudId Opnsense = new("opnsense");     // p6-opnsense, fw-p6.p6.lab
    public static readonly NoeudId Docker = new("docker");         // p6-docker, 10.99.0.20
    public static readonly NoeudId Collecteur = new("collecteur"); // p6-collecteur, 10.99.0.10
    public static readonly NoeudId Kali = new("kali");             // p6-kali, 203.0.113.66 / 10.99.0.66
    public static readonly NoeudId Nginx = new("nginx");
    public static readonly NoeudId Iperf3 = new("iperf3");
    public static readonly NoeudId DmzPivot = new("dmz-pivot");
    public static readonly NoeudId Bind9 = new("bind9");
    public static readonly NoeudId Api = new("api");
    public static readonly NoeudId Client01 = new("client-01");
    public static readonly NoeudId Client03 = new("client-03");
    public static readonly NoeudId Postgres = new("postgres");
    public static readonly NoeudId Admin = new("admin");
    public static readonly NoeudId ClientExt = new("client-ext");
    // Hors du laboratoire.
    public static readonly NoeudId Box = new("box");
    public static readonly NoeudId Internet = new("internet");
    public static readonly NoeudId PcMembre = new("pc-membre");
}

public sealed class NoeudIdJsonConverter : JsonConverter<NoeudId>
{
    public override NoeudId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? throw new JsonException("Identifiant de nœud manquant."));

    public override void Write(Utf8JsonWriter writer, NoeudId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Valeur);
}
