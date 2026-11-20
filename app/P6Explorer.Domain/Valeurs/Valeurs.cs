using System.Text.Json;
using System.Text.Json.Serialization;

namespace P6Explorer.Domain.Valeurs;

/// <summary>Identifiant d'un lien du schéma (« opnsense-em3-nat », « kali-eth0-wan »…).</summary>
[JsonConverter(typeof(LienIdJsonConverter))]
public readonly record struct LienId
{
    public string Valeur { get; }

    public LienId(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            throw new ArgumentException("Un identifiant de lien ne peut pas être vide.", nameof(valeur));
        Valeur = valeur.Trim().ToLowerInvariant();
    }

    public override string ToString() => Valeur;
}

public sealed class LienIdJsonConverter : JsonConverter<LienId>
{
    public override LienId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? throw new JsonException("Identifiant de lien manquant."));

    public override void Write(Utf8JsonWriter writer, LienId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Valeur);
}

/// <summary>Adresse IPv4 avec sa longueur de préfixe (10.10.0.1/24). Type owned dans Interface et Reseau.</summary>
public sealed record AdresseIp
{
    public string Adresse { get; set; } = "";
    public int Prefixe { get; set; }

    public AdresseIp() { }

    public AdresseIp(string adresse, int prefixe)
    {
        Adresse = adresse;
        Prefixe = prefixe;
    }

    /// <summary>Lit « 10.10.0.1/24 » (préfixe 32 si absent).</summary>
    public static AdresseIp Lire(string texte)
    {
        var morceaux = texte.Split('/', 2);
        return new AdresseIp(morceaux[0].Trim(), morceaux.Length == 2 ? int.Parse(morceaux[1]) : 32);
    }

    public override string ToString() => $"{Adresse}/{Prefixe}";
}

/// <summary>Traduction d'adresse appliquée par OPNsense (Destination NAT). Type owned dans l'effet Paquet.</summary>
public sealed record Nat
{
    public string? Regle { get; set; }          // « FA1 », « FA2 »…
    public string AdresseAvant { get; set; } = "";
    public int? PortAvant { get; set; }
    public string AdresseApres { get; set; } = "";
    public int? PortApres { get; set; }
}
