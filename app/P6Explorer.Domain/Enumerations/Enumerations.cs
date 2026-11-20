using System.Text.Json.Serialization;

namespace P6Explorer.Domain.Enumerations;

/// <summary>Là où la commande est tapée (ou cliquée).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Shell>))]
public enum Shell
{
    Bash,            // Debian, Kali, conteneurs
    PowerShell,      // hôte Windows
    OpnsenseShell,   // console OPNsense, option 8
    OpnsenseGui,     // interface web d'OPNsense (chemin de menu)
    VmwareGui,       // VMware Workstation
    WindowsGui,      // paramètres de Windows, pare-feu Windows
    Navigateur,      // une page web (panneau, Tailscale…)
    Editeur,         // écriture d'un fichier de configuration
    Document         // texte de référence (tableau, plan) : rien n'est exécuté
}

[JsonConverter(typeof(JsonStringEnumConverter<EtatVm>))]
public enum EtatVm { Creee, Allumee, Eteinte, Suspendue }

/// <summary>Protocole affiché sur l'étiquette du paquet (le transport s'en déduit).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Protocole>))]
public enum Protocole { Icmp, Tcp, Udp, Dns, Http, Https, Ssh, Ntp, Syslog, PostgreSql, Iperf3, Smb, Rdp, Vnc }

/// <summary>Ce qui arrive au paquet. ACalculer : le moteur réseau décide à la lecture, selon l'état A ou B.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Verdict>))]
public enum Verdict
{
    ACalculer,
    Passe,          // la connexion aboutit
    Refuse,         // le paquet arrive, mais aucun service n'écoute (RST)
    Bloque,         // une règle de pare-feu le jette
    Injoignable     // pas de route
}

[JsonConverter(typeof(JsonStringEnumConverter<ActionRegle>))]
public enum ActionRegle { Ajouter, Modifier, Activer, Desactiver, Supprimer, ViderEtats }

[JsonConverter(typeof(JsonStringEnumConverter<ActionFiltre>))]
public enum ActionFiltre { Pass, Block }

/// <summary>
/// Champ « Firewall rule » d'une Destination NAT d'OPNsense.
/// Aucune : il faut une règle de filtrage vers l'adresse interne. Associee (« Register rule ») : OPNsense crée
/// la règle « any → cible:port », qui laisse aussi passer un accès direct à la cible (E36).
/// Pass : le paquet redirigé passe sans évaluer les règles de filtrage (choix de la plateforme réelle).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FiltrageNat>))]
public enum FiltrageNat { Aucune, Associee, Pass }

[JsonConverter(typeof(JsonStringEnumConverter<StatutValidation>))]
public enum StatutValidation { Brouillon, AValider, Validee }

[JsonConverter(typeof(JsonStringEnumConverter<StatutFil>))]
public enum StatutFil { Ouvert, Resolu }

[JsonConverter(typeof(JsonStringEnumConverter<TypeNoeud>))]
public enum TypeNoeud { Hote, Vm, PareFeu, Conteneur, Exterieur }

[JsonConverter(typeof(JsonStringEnumConverter<Zone>))]
public enum Zone { Wan, Dmz, Lan, Mgmt, Trunk, Hote, Exterieur }

public static class ProtocoleExtensions
{
    /// <summary>Protocole de transport utilisé sous un protocole applicatif.</summary>
    public static Protocole Transport(this Protocole p) => p switch
    {
        Protocole.Icmp => Protocole.Icmp,
        Protocole.Udp or Protocole.Dns or Protocole.Ntp or Protocole.Syslog => Protocole.Udp,
        _ => Protocole.Tcp
    };
}
