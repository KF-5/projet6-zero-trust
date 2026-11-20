using System.Globalization;
using AutoMapper;
using P6Explorer.Application.ViewModels;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Topologie;

namespace P6Explorer.Application.Mappings;

/// <summary>Profil AutoMapper : entités du domaine → ViewModels et DTO.</summary>
public class ProfilP6 : Profile
{
    public ProfilP6()
    {
        CreateMap<Role, RoleCarteVM>()
            .ForMember(d => d.NbEtapes, o => o.MapFrom(s => s.Etapes.Count))
            .ForMember(d => d.NbFaites, o => o.MapFrom(s => s.Etapes.Count(e => e.Fait)))
            .ForMember(d => d.ProchaineEtape, o => o.MapFrom(s => s.Etapes.Where(e => !e.Fait).OrderBy(e => e.Numero).Select(e => e.Titre).FirstOrDefault()))
            .ForMember(d => d.NbCommandes, o => o.Ignore());   // compté par une requête LINQ (RoleRepository)

        CreateMap<Reseau, ZoneVM>()
            .ForMember(d => d.Zone, o => o.MapFrom(s => s.Zone.ToString().ToLowerInvariant()))
            .ForMember(d => d.Prefixe, o => o.MapFrom(s => s.Prefixe == null ? null : s.Prefixe.ToString()))
            .ForMember(d => d.X, o => o.MapFrom(s => s.CadreX ?? 0))
            .ForMember(d => d.Y, o => o.MapFrom(s => s.CadreY ?? 0))
            .ForMember(d => d.Largeur, o => o.MapFrom(s => s.CadreLargeur ?? 0))
            .ForMember(d => d.Hauteur, o => o.MapFrom(s => s.CadreHauteur ?? 0));

        CreateMap<Lien, LienVM>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Valeur))
            .ForMember(d => d.De, o => o.MapFrom(s => s.De.Valeur))
            .ForMember(d => d.Vers, o => o.MapFrom(s => s.Vers.Valeur))
            .ForMember(d => d.Zone, o => o.MapFrom(s => s.Zone.ToString().ToLowerInvariant()))
            .ForMember(d => d.Points, o => o.MapFrom(s => EnPoints(s.Points)));

        CreateMap<Noeud, NoeudVM>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Valeur))
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString().ToLowerInvariant()))
            .ForMember(d => d.Zone, o => o.MapFrom(s => s.Zone.ToString().ToLowerInvariant()))
            .ForMember(d => d.Adresses, o => o.MapFrom(s => s.Interfaces
                .Where(i => i.Adresse != null && i.Branchee && !(i.Description ?? "").Contains("Tailscale"))
                .Select(i => i.Nom + " " + i.Adresse!.Adresse)))
            .ForMember(d => d.AdressesReservees, o => o.MapFrom(s => s.Interfaces
                .Where(i => i.Adresse != null && (i.Description ?? "").Contains("Tailscale"))
                .Select(i => "Tailscale " + i.Adresse!.Adresse)));
    }

    /// <summary>Points d'une polyligne au format de l'attribut SVG « points » (séparateur décimal : point).</summary>
    private static string EnPoints(IEnumerable<double[]> points)
        => string.Join(" ", points.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p[0]},{p[1]}")));
}
