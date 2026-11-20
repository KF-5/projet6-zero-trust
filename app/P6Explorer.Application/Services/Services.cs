using AutoMapper;
using P6Explorer.Application.ViewModels;
using P6Explorer.Domain.Exceptions;
using P6Explorer.Domain.Interfaces;

namespace P6Explorer.Application.Services;

/// <summary>Création de la base et chargement initial des données (implémenté par l'infrastructure).</summary>
public interface IServiceAmorcage
{
    Task AmorcerAsync(string dossierApplication, CancellationToken annulation = default);
}

public interface IRoleService
{
    Task<List<RoleCarteVM>> ListerCartesAsync();
}

public interface ITopologieService
{
    Task<SchemaVM> ChargerSchemaAsync(string etat, bool membreConnecte);
}

public class RoleService(IRoleRepository roles, IMapper mapper) : IRoleService
{
    public async Task<List<RoleCarteVM>> ListerCartesAsync()
    {
        var liste = await roles.ListerAvecEtapesAsync();
        var commandes = await roles.CompterCommandesParRoleAsync();
        var cartes = mapper.Map<List<RoleCarteVM>>(liste);
        foreach (var c in cartes)
            c.NbCommandes = commandes.GetValueOrDefault(c.Numero);
        return cartes;
    }
}

public class TopologieService(ITopologieRepository topologie, IMapper mapper) : ITopologieService
{
    public async Task<SchemaVM> ChargerSchemaAsync(string etat, bool membreConnecte)
    {
        var e = await topologie.ChargerEtatAsync(etat) ?? throw new EntiteIntrouvableException("État du laboratoire", etat);
        var reseaux = await topologie.ListerReseauxAsync(etat);
        var schema = new SchemaVM
        {
            Etat = etat,
            Titre = $"Architecture du laboratoire — {e.Libelle}",
            Description = e.Description ?? "",
            MembreConnecte = membreConnecte,
            // Seuls les réseaux qui ont un cadre sont dessinés comme des zones (le trunk est un bus).
            Zones = mapper.Map<List<ZoneVM>>(reseaux.Where(r => r.CadreLargeur is > 0)),
            Liens = mapper.Map<List<LienVM>>(await topologie.ListerLiensAsync(etat)),
            Noeuds = mapper.Map<List<NoeudVM>>(await topologie.ListerNoeudsAsync(etat)),
        };
        return schema;
    }
}
