using System.Text.Json;
using P6Explorer.Domain.Amorcage;

namespace P6Explorer.Tests.Outils;

/// <summary>Accès aux fichiers du dépôt (donnees/…) depuis les tests.</summary>
public static class Depot
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static string Racine { get; } = TrouverRacine();

    public static string Chemin(params string[] morceaux) => Path.Combine([Racine, .. morceaux]);

    public static FichierRole LireRole(int numero)
        => JsonSerializer.Deserialize<FichierRole>(File.ReadAllText(Chemin("donnees", "commandes", $"role-{numero}.json")), Json)!;

    public static IReadOnlyList<FichierRole> LireRoles() => Enumerable.Range(1, 6).Select(LireRole).ToList();

    public static FichierTopologie LireTopologie(string etat)
        => JsonSerializer.Deserialize<FichierTopologie>(File.ReadAllText(Chemin("donnees", "topologie", $"etat-{etat}.json")), Json)!;

    private static string TrouverRacine()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "donnees", "commandes"))) return d.FullName;
        throw new DirectoryNotFoundException("Dossier donnees/commandes introuvable au-dessus des tests.");
    }
}
