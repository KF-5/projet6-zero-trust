namespace P6Explorer.Application.ViewModels;

/// <summary>Page d'accueil : la carte de l'architecture et les six cartes de rôle.</summary>
public class AccueilVM
{
    public SchemaVM Schema { get; set; } = new();
    public List<RoleCarteVM> Roles { get; set; } = [];
    public int TotalEtapes => Roles.Sum(r => r.NbEtapes);
    public int TotalCommandes => Roles.Sum(r => r.NbCommandes);
}

public class RoleCarteVM
{
    public int Numero { get; set; }
    public string Titre { get; set; } = "";
    public string CourtNom { get; set; } = "";
    public string Couleur { get; set; } = "";
    public string Mission { get; set; } = "";
    public int NbEtapes { get; set; }
    public int NbFaites { get; set; }
    public int NbCommandes { get; set; }
    public int Pourcentage => NbEtapes == 0 ? 0 : (int)Math.Round(100.0 * NbFaites / NbEtapes);
    public string? ProchaineEtape { get; set; }
}

/// <summary>Tout ce qu'il faut pour dessiner le schéma SVG d'un état (vue partielle _Schema).</summary>
public class SchemaVM
{
    public string Etat { get; set; } = "A";
    public string Titre { get; set; } = "";
    public string Description { get; set; } = "";
    public double Largeur { get; set; } = 1000;
    public double Hauteur { get; set; } = 700;
    public List<ZoneVM> Zones { get; set; } = [];
    public List<LienVM> Liens { get; set; } = [];
    public List<NoeudVM> Noeuds { get; set; } = [];
    /// <summary>Faux pour les visiteurs anonymes : les informations réservées (Tailscale) sont masquées (Q11).</summary>
    public bool MembreConnecte { get; set; }
}

public class ZoneVM
{
    public string Code { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Zone { get; set; } = "";          // wan, dmz, lan, mgmt, exterieur (classe CSS)
    public string? Prefixe { get; set; }
    public int? Vlan { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Largeur { get; set; }
    public double Hauteur { get; set; }
}

public class LienVM
{
    public string Id { get; set; } = "";
    public string De { get; set; } = "";
    public string Vers { get; set; } = "";
    public string Zone { get; set; } = "";
    public string? Libelle { get; set; }
    public bool Branche { get; set; }
    public string Points { get; set; } = "";        // « x1,y1 x2,y2 … » pour l'attribut points de <polyline>
}

public class NoeudVM
{
    public string Id { get; set; } = "";
    public string Nom { get; set; } = "";
    public string? SousTitre { get; set; }
    public string Type { get; set; } = "";
    public string Zone { get; set; } = "";
    public string? Description { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Largeur { get; set; }
    public double Hauteur { get; set; }
    public bool MembresSeulement { get; set; }
    public List<string> Adresses { get; set; } = [];
    public List<string> AdressesReservees { get; set; } = [];
    public double Gauche => X - Largeur / 2;
    public double Haut => Y - Hauteur / 2;
}
