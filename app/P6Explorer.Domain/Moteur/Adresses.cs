using System.Globalization;

namespace P6Explorer.Domain.Moteur;

/// <summary>Adresse IPv4 sous forme d'entier : les comparaisons de réseau deviennent de simples masques.</summary>
public readonly record struct Ipv4(uint Valeur)
{
    public static readonly Ipv4 Boucle = Lire("127.0.0.1");

    public static bool TryLire(string? texte, out Ipv4 ip)
    {
        ip = default;
        var morceaux = texte?.Trim().Split('.');
        if (morceaux is not { Length: 4 }) return false;
        uint v = 0;
        foreach (var m in morceaux)
        {
            if (!byte.TryParse(m, NumberStyles.None, CultureInfo.InvariantCulture, out var octet)) return false;
            v = (v << 8) | octet;
        }
        ip = new Ipv4(v);
        return true;
    }

    public static Ipv4 Lire(string texte)
        => TryLire(texte, out var ip) ? ip : throw new FormatException($"« {texte} » n'est pas une adresse IPv4.");

    public bool EstBoucle => (Valeur >> 24) == 127;

    public override string ToString()
        => $"{Valeur >> 24}.{(Valeur >> 16) & 255}.{(Valeur >> 8) & 255}.{Valeur & 255}";
}

/// <summary>Réseau IPv4 (10.10.0.0/26) ; une adresse seule vaut /32.</summary>
public readonly record struct Prefixe4(Ipv4 Reseau, int Longueur)
{
    public static Prefixe4 Creer(Ipv4 adresse, int longueur)
    {
        if (longueur is < 0 or > 32) throw new ArgumentOutOfRangeException(nameof(longueur));
        return new Prefixe4(new Ipv4(adresse.Valeur & Masque(longueur)), longueur);
    }

    public static bool TryLire(string? texte, out Prefixe4 prefixe)
    {
        prefixe = default;
        if (string.IsNullOrWhiteSpace(texte)) return false;
        var morceaux = texte.Split('/', 2);
        if (!Ipv4.TryLire(morceaux[0], out var ip)) return false;
        var longueur = 32;
        if (morceaux.Length == 2 && (!int.TryParse(morceaux[1], NumberStyles.None, CultureInfo.InvariantCulture, out longueur) || longueur > 32))
            return false;
        prefixe = Creer(ip, longueur);
        return true;
    }

    public bool Contient(Ipv4 ip) => (ip.Valeur & Masque(Longueur)) == Reseau.Valeur;

    private static uint Masque(int longueur) => longueur == 0 ? 0u : uint.MaxValue << (32 - longueur);

    public override string ToString() => $"{Reseau}/{Longueur}";
}

/// <summary>Lecture des champs d'une règle : « any », « self », « 10.99.0.2;10.99.0.10 », « 10.20.0.0/24 », « 80;443 », « 5901-5904 ».</summary>
public static class Champs
{
    public static bool AdresseCorrespond(string? champ, Ipv4 ip, Func<Ipv4, bool> estLocale)
    {
        if (EstAny(champ)) return true;
        foreach (var morceau in Decouper(champ!))
        {
            if (morceau.Equals("self", StringComparison.OrdinalIgnoreCase))
            {
                if (estLocale(ip)) return true;
            }
            else if (Prefixe4.TryLire(morceau, out var p) && p.Contient(ip)) return true;
        }
        return false;
    }

    public static bool PortCorrespond(string? champ, int? port)
    {
        if (EstAny(champ)) return true;
        if (port is null) return false;                     // ICMP : pas de port, une règle à ports ne s'applique pas
        foreach (var morceau in Decouper(champ!))
        {
            var bornes = morceau.Split('-', 2);
            if (bornes.Length == 2 && int.TryParse(bornes[0], out var de) && int.TryParse(bornes[1], out var a))
            {
                if (port >= de && port <= a) return true;
            }
            else if (int.TryParse(morceau, out var p) && p == port) return true;
        }
        return false;
    }

    /// <summary>« any », « tcp », « udp », « tcp/udp », « icmp » face au transport du paquet (tcp, udp, icmp).</summary>
    public static bool ProtoCorrespond(string? champ, string transport)
        => EstAny(champ) || champ!.Split('/', ';').Any(p => p.Trim().Equals(transport, StringComparison.OrdinalIgnoreCase));

    private static bool EstAny(string? champ) => string.IsNullOrWhiteSpace(champ) || champ.Trim().Equals("any", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> Decouper(string champ)
        => champ.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
