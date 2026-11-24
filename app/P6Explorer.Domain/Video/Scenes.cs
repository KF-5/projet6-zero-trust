using System.Text.Json.Serialization;

namespace P6Explorer.Domain.Video;

/// <summary>Les douze types d'étapes qu'une scène de la vidéo peut enchaîner (donnees/video/scenes.json).</summary>
public enum TypeEtapeScene
{
    Pause, Legende, Titre, Cadrer, Allumer, SurlignerZone, Simulation, BasculeEtat, CoteACote, Matrice, Resultats, Resume,
}

public static class TypesEtapesScene
{
    /// <summary>Nom écrit dans scenes.json → type. Un nom absent de cette table est une erreur de validation.</summary>
    public static readonly IReadOnlyDictionary<string, TypeEtapeScene> ParNom = new Dictionary<string, TypeEtapeScene>(StringComparer.Ordinal)
    {
        ["pause"] = TypeEtapeScene.Pause,
        ["legende"] = TypeEtapeScene.Legende,
        ["titre"] = TypeEtapeScene.Titre,
        ["cadrer"] = TypeEtapeScene.Cadrer,
        ["allumer"] = TypeEtapeScene.Allumer,
        ["surligner_zone"] = TypeEtapeScene.SurlignerZone,
        ["simulation"] = TypeEtapeScene.Simulation,
        ["bascule_etat"] = TypeEtapeScene.BasculeEtat,
        ["cote_a_cote"] = TypeEtapeScene.CoteACote,
        ["matrice"] = TypeEtapeScene.Matrice,
        ["resultats"] = TypeEtapeScene.Resultats,
        ["resume"] = TypeEtapeScene.Resume,
    };
}

/// <summary>Contenu de donnees/video/scenes.json : les scènes P-1 à P-8 et les textes de l'écran de fin.</summary>
public class FichierScenes
{
    [JsonPropertyName("scenes")] public List<SceneVideo> Scenes { get; set; } = [];
    [JsonPropertyName("resume")] public TextesResume Resume { get; set; } = new();
    /// <summary>Prénoms facultatifs par carte : clés « 1 » à « 6 » (rôles) et « plateforme ». Vide par défaut.</summary>
    [JsonPropertyName("equipe")] public Dictionary<string, string> Equipe { get; set; } = [];
}

public class SceneVideo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("titre")] public string Titre { get; set; } = "";
    [JsonPropertyName("sequence")] public int Sequence { get; set; }
    [JsonPropertyName("duree_s")] public double DureeS { get; set; }
    [JsonPropertyName("etat")] public string Etat { get; set; } = "A";
    /// <summary>Vrai : bandeau permanent « Ce qui suit est la cible » (état pas encore construit).</summary>
    [JsonPropertyName("cible")] public bool Cible { get; set; }
    [JsonPropertyName("etapes")] public List<EtapeScene> Etapes { get; set; } = [];
}

/// <summary>Une étape de scène. Les paramètres utiles dépendent du type (voir app/README.md).</summary>
public class EtapeScene
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    /// <summary>Durée de l'étape ; facultative pour toutes (0 : l'étape suivante commence aussitôt l'action finie).</summary>
    [JsonPropertyName("duree_s")] public double? DureeS { get; set; }
    /// <summary>Pause seulement : attendre que la scène atteigne cet instant (en secondes depuis son début).</summary>
    [JsonPropertyName("jusqu_a_s")] public double? JusquAS { get; set; }
    [JsonPropertyName("texte")] public string? Texte { get; set; }
    /// <summary>Légende affichée pendant l'étape (simulation, cadrer, allumer…) ; elle reste jusqu'à la suivante.</summary>
    [JsonPropertyName("legende")] public string? Legende { get; set; }
    [JsonPropertyName("noeuds")] public List<string>? Noeuds { get; set; }
    [JsonPropertyName("zones")] public List<string>? Zones { get; set; }
    /// <summary>surligner_zone : légende « NOM · VLAN n · réseau » lue dans politique/segments.csv.</summary>
    [JsonPropertyName("segment")] public bool Segment { get; set; }
    [JsonPropertyName("source")] public string? Source { get; set; }
    [JsonPropertyName("destination")] public string? Destination { get; set; }
    [JsonPropertyName("protocole")] public string? Protocole { get; set; }
    [JsonPropertyName("port")] public int? Port { get; set; }
    [JsonPropertyName("etat")] public string? Etat { get; set; }
    /// <summary>simulation : vitesse de l'animation (×0,25 à ×4, 1 par défaut) pour tenir la durée du plan.</summary>
    [JsonPropertyName("vitesse")] public double? Vitesse { get; set; }
    [JsonPropertyName("ligne_mise_en_avant")] public string? LigneMiseEnAvant { get; set; }
    /// <summary>cote_a_cote : titres au-dessus des schémas A (gauche) et B (droite).</summary>
    [JsonPropertyName("gauche")] public string? Gauche { get; set; }
    [JsonPropertyName("droite")] public string? Droite { get; set; }

    [JsonIgnore] public TypeEtapeScene? TypeConnu => TypesEtapesScene.ParNom.TryGetValue(Type, out var t) ? t : null;
}

/// <summary>Textes de l'écran de fin (P-8), modifiables sans recompiler.</summary>
public class TextesResume
{
    [JsonPropertyName("detection_a")] public string DetectionA { get; set; } = "IDS : signale";
    [JsonPropertyName("detection_b")] public string DetectionB { get; set; } = "IPS : bloque";
    [JsonPropertyName("phrase")] public string Phrase { get; set; } = "";
    [JsonPropertyName("reproduire")] public string Reproduire { get; set; } = "";
    /// <summary>Le paquet S-05 rejoué par le moteur en A puis en B pour la ligne « S-05 » du résumé.</summary>
    [JsonPropertyName("s05")] public PaquetResume S05 { get; set; } = new();
}

public class PaquetResume
{
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("destination")] public string Destination { get; set; } = "";
    [JsonPropertyName("protocole")] public string Protocole { get; set; } = "Tcp";
    [JsonPropertyName("port")] public int? Port { get; set; }
}

/// <summary>Un fichier lu et vérifié : la valeur (si lisible) et les erreurs à afficher en clair.</summary>
public class ResultatValidation<T> where T : class
{
    public T? Valeur { get; init; }
    public List<string> Erreurs { get; init; } = [];
    public bool Valide => Valeur is not null && Erreurs.Count == 0;
}
