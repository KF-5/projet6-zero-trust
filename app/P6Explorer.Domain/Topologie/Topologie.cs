using System.Text.Json.Serialization;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Domain.Topologie;

/// <summary>État du laboratoire : A (périmétrique) ou B (Zero Trust).</summary>
public class EtatReseau
{
    public string Code { get; set; } = "A";
    public string Libelle { get; set; } = "";
    public string? Description { get; set; }
    public List<Reseau> Reseaux { get; set; } = [];
    public List<Regle> Regles { get; set; } = [];
    public List<RegleNat> ReglesNat { get; set; } = [];
}

/// <summary>Une machine du schéma, avec sa position dans le SVG.</summary>
public class Noeud
{
    public NoeudId Id { get; set; }
    public string Nom { get; set; } = "";
    public string? SousTitre { get; set; }            // « p6-opnsense · fw-p6.p6.lab »
    public TypeNoeud Type { get; set; }
    public Zone Zone { get; set; }
    public string? Description { get; set; }
    public string? Passerelle { get; set; }           // route par défaut
    public NoeudId? Hote { get; set; }                // conteneur : la VM qui le fait tourner
    public bool MembresSeulement { get; set; }        // Q11 : informations réservées aux membres connectés
    // Géométrie (viewBox 1000 × 700) : centre et taille du cadre.
    public double X { get; set; }
    public double Y { get; set; }
    public double Largeur { get; set; } = 96;
    public double Hauteur { get; set; } = 48;
    public List<Interface> Interfaces { get; set; } = [];
    /// <summary>Ports en écoute (lus par le moteur réseau : connexion établie ou refusée par le service).</summary>
    public List<ServiceReseau> Services { get; set; } = [];
}

/// <summary>Un port en écoute sur une machine (type owned de Noeud).</summary>
public class ServiceReseau
{
    public string Nom { get; set; } = "";             // « nginx (HTTPS) », « sshd »
    public string Proto { get; set; } = "tcp";        // tcp ou udp
    public int Port { get; set; }
    /// <summary>Adresse d'écoute ; null : toutes les adresses de la machine. 127.0.0.1 : la machine elle-même.</summary>
    public string? Adresse { get; set; }
    public bool Actif { get; set; } = true;
    public string? Description { get; set; }
}

/// <summary>Une carte réseau d'une machine (em0, ens33.10, eth0…).</summary>
public class Interface
{
    [JsonIgnore] public int Id { get; set; }
    [JsonIgnore] public NoeudId NoeudId { get; set; }
    [JsonIgnore] public Noeud? Noeud { get; set; }
    public string Nom { get; set; } = "";
    public string? Description { get; set; }
    public string? ReseauCode { get; set; }
    /// <summary>Nom de l'interface dans OPNsense (« WAN », « LAN », « DMZ-WEB »…) : c'est là que les règles s'écrivent.</summary>
    public string? Affectation { get; set; }
    [JsonIgnore] public int? ReseauId { get; set; }
    [JsonIgnore] public Reseau? Reseau { get; set; }
    public AdresseIp? Adresse { get; set; }
    public int? Vlan { get; set; }
    public bool Branchee { get; set; } = true;
    public string? EtatReseauCode { get; set; }       // null : présente dans les deux états
}

/// <summary>Un réseau (segment VMware, VLAN, réseau Docker), avec le cadre de sa zone dans le SVG.</summary>
public class Reseau
{
    [JsonIgnore] public int Id { get; set; }
    public string Code { get; set; } = "";            // « p6-wan », « lan-a », « dmz-web »
    public string Nom { get; set; } = "";
    public Zone Zone { get; set; }
    public int? Vlan { get; set; }
    public AdresseIp? Prefixe { get; set; }
    public string? Passerelle { get; set; }
    public string? EtatReseauCode { get; set; }       // null : commun aux deux états
    [JsonIgnore] public EtatReseau? EtatReseau { get; set; }
    public double? CadreX { get; set; }
    public double? CadreY { get; set; }
    public double? CadreLargeur { get; set; }
    public double? CadreHauteur { get; set; }
    [JsonIgnore] public List<Interface> Interfaces { get; set; } = [];
}

/// <summary>Un câble du schéma (polyligne), branché ou non.</summary>
public class Lien
{
    public LienId Id { get; set; }
    public NoeudId De { get; set; }
    public NoeudId Vers { get; set; }
    public Zone Zone { get; set; }
    public string? Libelle { get; set; }
    public bool Branche { get; set; } = true;
    public string? EtatReseauCode { get; set; }
    /// <summary>Points de la polyligne : [[x, y], …].</summary>
    public List<double[]> Points { get; set; } = [];
}

/// <summary>Règle de filtrage (OPNsense, nftables d'une VM, pare-feu Windows).</summary>
public class Regle
{
    [JsonIgnore] public int Id { get; set; }
    public string EtatReseauCode { get; set; } = "A";
    public NoeudId Equipement { get; set; }           // opnsense, docker (p6_ssh), hote (pare-feu Windows)…
    public string Code { get; set; } = "";            // « FA1 », « Admin du pare-feu », « p6_ssh »
    public string Interface { get; set; } = "";       // interface d'entrée (« WAN », « LAN », « MGMT », « input »)
    public int Ordre { get; set; }
    public ActionFiltre Action { get; set; }
    public string Proto { get; set; } = "any";
    public string Source { get; set; } = "any";
    public string Destination { get; set; } = "any";
    public string Ports { get; set; } = "any";
    public bool Journal { get; set; } = true;
    public bool Active { get; set; } = true;
    public string Description { get; set; } = "";
}

/// <summary>Redirection de port (Destination NAT d'OPNsense), appliquée avant le filtrage.</summary>
public class RegleNat
{
    [JsonIgnore] public int Id { get; set; }
    public string EtatReseauCode { get; set; } = "A";
    public string Code { get; set; } = "";
    public string Interface { get; set; } = "WAN";
    public string Proto { get; set; } = "tcp";
    public string AdressePublique { get; set; } = "";
    public string PortPublic { get; set; } = "";
    public string Cible { get; set; } = "";
    public string PortCible { get; set; } = "";
    public string? SourceAutorisee { get; set; }
    /// <summary>Champ « Firewall rule » de la redirection (E36 : « Pass » sur la plateforme réelle).</summary>
    public FiltrageNat Filtrage { get; set; } = FiltrageNat.Aucune;
    public bool Active { get; set; } = true;
    public string Description { get; set; } = "";
}
