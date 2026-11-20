using System.Globalization;
using AutoMapper;
using P6Explorer.Application.Dto;
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
            .ForMember(d => d.PremiereEtape, o => o.MapFrom(s => s.Etapes.OrderBy(e => e.Numero).Select(e => (int?)e.Numero).FirstOrDefault()))
            .ForMember(d => d.IdsEtapes, o => o.MapFrom(s => s.Etapes.Select(e => e.Id)))
            .ForMember(d => d.NbComprises, o => o.Ignore())
            .ForMember(d => d.NbMaitrisees, o => o.Ignore())
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

        // Page d'une étape : les textes riches (HTML) sont remplis par le service, avec le glossaire.
        CreateMap<Etape, EtapeDetailVM>()
            .ForMember(d => d.NbCommandes, o => o.MapFrom(s => s.Commandes.Count))
            .ForMember(d => d.Criteres, o => o.MapFrom(s => s.Criteres.OrderBy(c => c.Ordre).Select(c => c.Texte)))
            .ForMember(d => d.Preuves, o => o.MapFrom(s => s.Preuves))
            .ForMember(d => d.Commandes, o => o.MapFrom(s => s.Commandes.OrderBy(c => c.Ordre)))
            .ForMember(d => d.ButHtml, o => o.Ignore())
            .ForMember(d => d.PourquoiHtml, o => o.Ignore())
            .ForMember(d => d.PrerequisHtml, o => o.Ignore())
            .ForMember(d => d.LivrableHtml, o => o.Ignore())
            .ForMember(d => d.MiseAJourHtml, o => o.Ignore())
            .ForMember(d => d.DateModification, o => o.Ignore());
        CreateMap<Panne, PanneVM>();
        CreateMap<Preuve, PreuveVM>().ForMember(d => d.Url, o => o.MapFrom(s => "/preuves/" + s.NomFichier));
        CreateMap<Commande, CommandeLigneVM>()
            .ForMember(d => d.Shell, o => o.MapFrom(s => s.Shell.ToString()))
            .ForMember(d => d.Ou, o => o.MapFrom(s => s.Ou.Valeur))
            .ForMember(d => d.Apercu, o => o.MapFrom(s => s.Texte.Length > 90 ? s.Texte.Substring(0, 90) + "…" : s.Texte));

        // DTO du lecteur (Web API) : les champs calculés sont remplis par EtapeService.
        CreateMap<Commande, CommandeDto>()
            .ForMember(d => d.Ou, o => o.MapFrom(s => s.Ou.Valeur))
            .ForMember(d => d.Shell, o => o.MapFrom(s => s.Shell.ToString()))
            .ForMember(d => d.NbSousCommandes, o => o.MapFrom(s => s.SousCommandes.Count))
            .ForMember(d => d.Effets, o => o.Ignore())
            .ForMember(d => d.Index, o => o.Ignore())
            .ForMember(d => d.Niveau, o => o.Ignore())
            .ForMember(d => d.ParentCode, o => o.Ignore())
            .ForMember(d => d.OuNom, o => o.Ignore())
            .ForMember(d => d.Invite, o => o.Ignore())
            .ForMember(d => d.Graphique, o => o.Ignore())
            .ForMember(d => d.ExplicationHtml, o => o.Ignore())
            .ForMember(d => d.QuestionsOuvertes, o => o.Ignore())
            .ForMember(d => d.Comprise, o => o.Ignore());

        // Formulaires d'édition.
        CreateMap<Etape, EtapeFormVM>();
        CreateMap<Commande, CommandeFormVM>()
            .ForMember(d => d.Ou, o => o.MapFrom(s => s.Ou.Valeur))
            .ForMember(d => d.RoleNumero, o => o.Ignore())
            .ForMember(d => d.EtapeNumero, o => o.Ignore())
            .ForMember(d => d.Effets, o => o.Ignore())
            .ForMember(d => d.Machines, o => o.Ignore());
    }

    /// <summary>Points d'une polyligne au format de l'attribut SVG « points » (séparateur décimal : point).</summary>
    private static string EnPoints(IEnumerable<double[]> points)
        => string.Join(" ", points.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p[0]},{p[1]}")));
}
