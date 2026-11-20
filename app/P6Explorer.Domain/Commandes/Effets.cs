using System.Text.Json.Serialization;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Domain.Commandes;

/// <summary>
/// Ce que le schéma montre quand une commande s'exécute. Petit langage déclaratif, indépendant du rendu :
/// le serveur décrit, lecteur.js anime. Stocké en héritage TPH (une table, une colonne discriminante)
/// et sérialisé en JSON polymorphe (propriété « type »).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(EtatNoeud), "etatNoeud")]
[JsonDerivedType(typeof(EtatLien), "etatLien")]
[JsonDerivedType(typeof(AttribuerIp), "attribuerIp")]
[JsonDerivedType(typeof(Paquet), "paquet")]
[JsonDerivedType(typeof(ChangementRegle), "changementRegle")]
[JsonDerivedType(typeof(Service), "service")]
[JsonDerivedType(typeof(Fichier), "fichier")]
[JsonDerivedType(typeof(Instantane), "instantane")]
[JsonDerivedType(typeof(Journal), "journal")]
[JsonDerivedType(typeof(AlerteIds), "alerteIds")]
[JsonDerivedType(typeof(ResultatTest), "resultatTest")]
[JsonDerivedType(typeof(Surbrillance), "surbrillance")]
[JsonDerivedType(typeof(ChangementEtat), "changementEtat")]
[JsonDerivedType(typeof(ConteneurReseau), "conteneurReseau")]
public abstract record Effet : IRevisable
{
    [JsonIgnore] public int Id { get; set; }
    [JsonIgnore] public int CommandeId { get; set; }
    [JsonIgnore] public Commande? Commande { get; set; }

    /// <summary>Ordre d'apparition dans la commande.</summary>
    public int Ordre { get; set; }

    /// <summary>Vrai si l'effet a été proposé par l'importeur (motif reconnu) et pas encore relu.</summary>
    public bool Automatique { get; set; }

    /// <summary>Phrase courte affichée dans le panneau d'explication pendant l'effet.</summary>
    public abstract string Resumer();
}

/// <summary>Une VM change d'état (création, démarrage, arrêt, suspension).</summary>
public sealed record EtatNoeud : Effet
{
    public NoeudId Noeud { get; set; }
    public EtatVm Etat { get; set; }
    public override string Resumer() => $"{Noeud} : {Etat.ToString().ToLowerInvariant()}";
}

/// <summary>Un câble virtuel est branché ou débranché (ex. carte NAT d'installation).</summary>
public sealed record EtatLien : Effet
{
    public LienId Lien { get; set; }
    public bool Branche { get; set; }
    public override string Resumer() => $"{Lien} : {(Branche ? "branché" : "débranché")}";
}

/// <summary>Une carte reçoit une adresse.</summary>
public sealed record AttribuerIp : Effet
{
    public NoeudId Noeud { get; set; }
    public string Carte { get; set; } = "";
    public string Ip { get; set; } = "";
    public override string Resumer() => $"{Noeud} {Carte} = {Ip}";
}

/// <summary>Un paquet part d'une machine vers une autre. Chemin, NAT et verdict vides : le moteur réseau les calcule.</summary>
public sealed record Paquet : Effet
{
    public NoeudId De { get; set; }
    public NoeudId Vers { get; set; }
    public Protocole Proto { get; set; }
    public int? Port { get; set; }
    /// <summary>Adresse visée telle qu'écrite dans la commande (203.0.113.1 avant NAT, par exemple).</summary>
    public string? AdresseVisee { get; set; }
    public List<NoeudId>? Chemin { get; set; }
    public Nat? Nat { get; set; }
    public Verdict Verdict { get; set; } = Verdict.ACalculer;
    /// <summary>Règle qui décide : « FA1 », « FA5 », « p6_ssh », « Admin du pare-feu »…</summary>
    public string? Regle { get; set; }
    public int? Vlan { get; set; }

    public override string Resumer()
        => $"{De} → {Vers}{(AdresseVisee is null ? "" : $" ({AdresseVisee})")} · {Proto}{(Port is null ? "" : $" {Port}")}";
}

/// <summary>Une règle de pare-feu (OPNsense, nftables, Windows) est ajoutée, modifiée, désactivée…</summary>
public sealed record ChangementRegle : Effet
{
    public ActionRegle Action { get; set; }
    public string IdRegle { get; set; } = "";
    public string Resume { get; set; } = "";
    public override string Resumer() => $"{IdRegle} : {Resume}";
}

/// <summary>Un service démarre ou s'arrête sur une machine.</summary>
public sealed record Service : Effet
{
    public NoeudId Noeud { get; set; }
    public string Nom { get; set; } = "";
    public bool Demarre { get; set; }
    public int? Port { get; set; }
    public override string Resumer() => $"{Noeud} : {Nom} {(Demarre ? "démarré" : "arrêté")}";
}

/// <summary>Un fichier est écrit (ou supprimé) sur une machine.</summary>
public sealed record Fichier : Effet
{
    public NoeudId Noeud { get; set; }
    public string Chemin { get; set; } = "";
    public bool Cree { get; set; } = true;
    public override string Resumer() => $"{Noeud} : {Chemin}";
}

/// <summary>Un instantané VMware est pris ou restauré.</summary>
public sealed record Instantane : Effet
{
    public NoeudId Vm { get; set; }
    public string Nom { get; set; } = "";
    public bool Restauration { get; set; }
    public override string Resumer() => $"{Vm} : instantané « {Nom} »{(Restauration ? " restauré" : "")}";
}

/// <summary>Une ligne de journal part vers le collecteur (syslog UDP 514).</summary>
public sealed record Journal : Effet
{
    public NoeudId De { get; set; }
    public string Ligne { get; set; } = "";
    public override string Resumer() => $"{De} → collecteur : {Ligne}";
}

/// <summary>Suricata lève une alerte (et bloque le paquet en mode IPS).</summary>
public sealed record AlerteIds : Effet
{
    public string Signature { get; set; } = "";
    public bool Blocage { get; set; }
    public override string Resumer() => $"Suricata : {Signature}{(Blocage ? " (bloqué)" : "")}";
}

/// <summary>Le résultat d'une suite de tests (« 12 / 12 conformes »).</summary>
public sealed record ResultatTest : Effet
{
    public string Nom { get; set; } = "";
    public bool Ok { get; set; }
    public string? Compteur { get; set; }
    public override string Resumer() => $"{Nom} : {Compteur ?? (Ok ? "conforme" : "écart")}";
}

/// <summary>La commande ne change rien (lecture) : on met en valeur la machine et le fichier lus.</summary>
public sealed record Surbrillance : Effet
{
    public NoeudId Noeud { get; set; }
    public string? Fichier { get; set; }
    public string? Texte { get; set; }
    public override string Resumer() => Texte ?? (Fichier is null ? $"{Noeud}" : $"{Noeud} : {Fichier}");
}

/// <summary>Le laboratoire bascule de l'état A à l'état B (ou l'inverse).</summary>
public sealed record ChangementEtat : Effet
{
    public string Etat { get; set; } = "A";
    public override string Resumer() => $"Passage à l'état {Etat}";
}

/// <summary>Un conteneur est relié à un réseau Docker (macvlan) avec son adresse.</summary>
public sealed record ConteneurReseau : Effet
{
    public NoeudId Conteneur { get; set; }
    public string Reseau { get; set; } = "";
    public string Ip { get; set; } = "";
    public override string Resumer() => $"{Conteneur} : {Ip} sur {Reseau}";
}
