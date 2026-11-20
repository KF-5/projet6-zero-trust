using P6Explorer.Domain.Commandes;

namespace P6Explorer.Application.Dto;

/// <summary>Une commande telle que lecteur.js la rejoue (api/etapes/{id}/commandes).</summary>
public class CommandeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    /// <summary>Position dans la liste rejouée (sous-commandes comprises), à partir de 1 : sert dans l'URL ?commande=N.</summary>
    public int Index { get; set; }
    public int Niveau { get; set; }                     // 0 : commande ; 1 : sous-commande d'un script déplié
    public string? ParentCode { get; set; }
    public string Ou { get; set; } = "";
    public string OuNom { get; set; } = "";
    public string Shell { get; set; } = "";
    public string Invite { get; set; } = "";            // « karim@p6-docker:~$ », « OPNsense › »
    public bool Graphique { get; set; }                 // action dans une interface graphique (pas de frappe)
    public string? Bloc { get; set; }
    public string Texte { get; set; } = "";
    public string? TexteGuide { get; set; }
    public string ExplicationHtml { get; set; } = "";
    public string? Sortie { get; set; }
    public bool EstTest { get; set; }
    public int NbSousCommandes { get; set; }
    public int QuestionsOuvertes { get; set; }
    public bool Comprise { get; set; }
    public List<EffetDto> Effets { get; set; } = [];
}

/// <summary>Un effet : ses données (JSON polymorphe, propriété « type »), sa phrase, et pour un paquet le calcul du moteur.</summary>
public class EffetDto
{
    public Effet Donnees { get; set; } = null!;
    public string Resume { get; set; } = "";
    /// <summary>Machine à mettre en valeur pendant l'effet.</summary>
    public string? Noeud { get; set; }
    public ResultatPaquetDto? Paquet { get; set; }
}

/// <summary>Ce que le moteur réseau a calculé pour un paquet.</summary>
public class ResultatPaquetDto
{
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";       // adresse visée
    public string? Arrivee { get; set; }                // machine atteinte (après NAT)
    public string Protocole { get; set; } = "";
    public string Transport { get; set; } = "";
    public int? Port { get; set; }
    public string Verdict { get; set; } = "";           // Passe, Refuse, Bloque, Injoignable, ACalculer
    public string VerdictTexte { get; set; } = "";
    public string? Regle { get; set; }
    public string? Equipement { get; set; }
    public bool Filterlog { get; set; }
    public string Explication { get; set; } = "";
    public bool Calcule { get; set; } = true;           // faux : hors du laboratoire simulé
    public NatDto? Nat { get; set; }
    public List<SautDto> Chemin { get; set; } = [];
    public TrameDto? Trame { get; set; }
    /// <summary>Phrase « pour un débutant » : ce qui décide, en une ligne.</summary>
    public string Resume { get; set; } = "";
}

public class NatDto
{
    public string? Regle { get; set; }
    public string Avant { get; set; } = "";
    public string Apres { get; set; } = "";
}

public class SautDto
{
    public string Noeud { get; set; } = "";
    public string? Entree { get; set; }
    public string? Sortie { get; set; }
    public int? Vlan { get; set; }
    public string Texte { get; set; } = "";
}

/// <summary>Trame décodée octet par octet (DecodeurPaquet).</summary>
public class TrameDto
{
    public string Titre { get; set; } = "";
    public string Hexa { get; set; } = "";              // octets en hexadécimal, sans séparateur
    public int Longueur { get; set; }
    public List<CoucheDto> Couches { get; set; } = [];
}

public class CoucheDto
{
    public string Nom { get; set; } = "";
    public string Code { get; set; } = "";              // eth, vlan, ip, tcp, udp, icmp, app (classe CSS)
    public int Debut { get; set; }
    public int Longueur { get; set; }
    public List<ChampDto> Champs { get; set; } = [];
}

public class ChampDto
{
    public string Nom { get; set; } = "";
    public int Debut { get; set; }
    public int Longueur { get; set; }
    public string Valeur { get; set; } = "";
    public string Explication { get; set; } = "";
    public bool Exemple { get; set; }
}

/// <summary>Demande de simulation « Et si ? » (api/simulation).</summary>
public class DemandeSimulationDto
{
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Protocole { get; set; } = "Tcp";
    public int? Port { get; set; }
    public string Etat { get; set; } = "A";
    public bool Mesures { get; set; }                   // FA6 / F09 actives (mesures iperf3)
    public bool SshOpnsense { get; set; }               // Q13
    public bool DurcissementQ16 { get; set; }           // p6_syslog
}
