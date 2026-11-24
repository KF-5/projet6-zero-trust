using Microsoft.Extensions.Configuration;

namespace P6Explorer.Infrastructure.Fichiers;

/// <summary>
/// Où sont les fichiers du dépôt. donnees/ : <c>Amorcage:DossierDonnees</c> s'il est réglé (comme pour l'amorçage),
/// sinon cherché en remontant depuis l'application. resultats/ : <c>Amorcage:DossierResultats</c>, sinon à côté de donnees/.
/// </summary>
public sealed class DossiersDepot
{
    public string? Donnees { get; }
    public string? Resultats { get; }

    public DossiersDepot(IConfiguration configuration)
    {
        Donnees = configuration["Amorcage:DossierDonnees"] is { Length: > 0 } d
            ? Path.GetFullPath(d, Directory.GetCurrentDirectory())
            : Chercher(Directory.GetCurrentDirectory()) ?? Chercher(AppContext.BaseDirectory);
        Resultats = configuration["Amorcage:DossierResultats"] is { Length: > 0 } r
            ? Path.GetFullPath(r, Directory.GetCurrentDirectory())
            : Donnees is null ? null : Path.Combine(Path.GetDirectoryName(Donnees.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!, "resultats");
    }

    /// <summary>Chemin d'un fichier de donnees/, ou null si le dossier est introuvable.</summary>
    public string? CheminDonnees(params string[] morceaux) => Donnees is null ? null : Path.Combine([Donnees, .. morceaux]);

    private static string? Chercher(string depart)
    {
        for (var d = new DirectoryInfo(depart); d is not null; d = d.Parent)
        {
            var candidat = Path.Combine(d.FullName, "donnees");
            if (Directory.Exists(Path.Combine(candidat, "commandes"))) return candidat;
        }
        return null;
    }
}
