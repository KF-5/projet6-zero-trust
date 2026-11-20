using Microsoft.AspNetCore.Razor.TagHelpers;

namespace P6Explorer.Web.TagHelpers;

/// <summary>
/// Tag Helper personnalisé : &lt;zone nom="DMZ" /&gt; affiche la pastille de couleur d'une zone
/// (couleurs imposées du guide : WAN rouge, DMZ ambre, LAN vert, MGMT violet, trunk bleu…).
/// </summary>
[HtmlTargetElement("zone", TagStructure = TagStructure.NormalOrSelfClosing)]
public class ZoneTagHelper : TagHelper
{
    private static readonly Dictionary<string, (string Classe, string Explication)> Zones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WAN"] = ("wan", "Le « dehors » simulé : 203.0.113.0/24, là où se trouvent Kali et client-ext."),
        ["DMZ"] = ("dmz", "Zone des serveurs publiés (nginx, bind9, API), séparée du LAN."),
        ["LAN"] = ("lan", "Réseau interne des postes et de la base de données."),
        ["MGMT"] = ("mgmt", "Réseau de gestion, hors bande : sert seulement à administrer les VM."),
        ["TRUNK"] = ("trunk", "Un seul câble qui transporte plusieurs VLAN, chacun marqué par son numéro (802.1Q)."),
        ["IDS"] = ("ids", "Suricata : il inspecte les paquets et lève des alertes (ou bloque, en mode IPS)."),
        ["OPNSENSE"] = ("opn", "Le pare-feu : seul point de passage entre WAN, DMZ et LAN."),
    };

    /// <summary>Nom de la zone : WAN, DMZ, LAN, MGMT, TRUNK, IDS ou OPNSENSE.</summary>
    public string Nom { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (classe, explication) = Zones.TryGetValue(Nom, out var z) ? z : ("autre", "");
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"pastille-zone z-{classe}");
        if (explication.Length > 0) output.Attributes.SetAttribute("title", explication);
        output.Content.SetHtmlContent("<span class=\"puce\" aria-hidden=\"true\"></span>");
        output.Content.Append(Nom.Equals("OPNSENSE", StringComparison.OrdinalIgnoreCase) ? "OPNsense" : Nom.ToUpperInvariant());
    }
}
