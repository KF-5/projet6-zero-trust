using System.Text.Json;
using P6Explorer.Application.ViewModels;

namespace P6Explorer.Web.Services;

/// <summary>
/// Schémas propres aux étapes, repris du guide et rendus en SVG statique par outils/schemas-vers-svg.mjs
/// (netmap, NAT, table d'états, macvlan…). Lus une fois au démarrage : ce sont des fichiers produits par le projet.
/// </summary>
public sealed class SchemasGuide
{
    private readonly Dictionary<string, SchemaGuideVM> _schemas;

    public SchemasGuide(IWebHostEnvironment environnement, IConfiguration configuration)
    {
        var dossier = configuration["Amorcage:DossierDonnees"] is { Length: > 0 } d ? Path.GetFullPath(d, environnement.ContentRootPath) : TrouverDonnees(environnement.ContentRootPath);
        var chemin = dossier is null ? null : Path.Combine(dossier, "schemas", "schemas.json");
        _schemas = chemin is not null && File.Exists(chemin)
            ? JsonSerializer.Deserialize<Dictionary<string, SchemaGuideVM>>(File.ReadAllText(chemin), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []
            : [];
    }

    public SchemaGuideVM? Trouver(string? nom) => nom is not null && _schemas.TryGetValue(nom, out var s) ? s : null;

    public int Nombre => _schemas.Count;

    private static string? TrouverDonnees(string depart)
    {
        for (var d = new DirectoryInfo(depart); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "donnees", "schemas"))) return Path.Combine(d.FullName, "donnees");
        return null;
    }
}
