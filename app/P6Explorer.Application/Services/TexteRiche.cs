using System.Text;
using System.Text.RegularExpressions;
using P6Explorer.Domain.Commandes;

namespace P6Explorer.Application.Services;

/// <summary>Identifiant du compte connecté (null : visiteur), lu par l'intercepteur pour signer l'historique.</summary>
public interface IUtilisateurCourant
{
    string? Identifiant { get; }
}

/// <summary>
/// Textes du guide en HTML sûr : tout est d'abord encodé, puis on rend le petit Markdown du guide
/// (**gras**, `code`, retours à la ligne) et on entoure la première occurrence de chaque terme du glossaire
/// d'un &lt;dfn&gt; dont la définition s'affiche au survol et au clavier.
/// </summary>
public static partial class TexteRiche
{
    public static string EnHtml(string? texte, IReadOnlyCollection<TermeGlossaire>? termes = null)
    {
        if (string.IsNullOrWhiteSpace(texte)) return "";
        var html = Encoder(texte.Trim());
        html = Code().Replace(html, "<code>$1</code>");
        html = Gras().Replace(html, "<strong>$1</strong>");
        if (termes is { Count: > 0 }) html = Annoter(html, termes);
        return html.Replace("\n", "<br>");
    }

    /// <summary>Encodage HTML minimal (les cinq caractères dangereux) : les accents restent lisibles dans le source.</summary>
    public static string Encoder(string t)
        => t.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    /// <summary>Texte brut (sans Markdown) pour un attribut ou un résumé.</summary>
    public static string Brut(string? texte)
        => string.IsNullOrEmpty(texte) ? "" : texte.Replace("**", "").Replace("`", "");

    private static string Annoter(string html, IReadOnlyCollection<TermeGlossaire> termes)
    {
        var dejaVus = new HashSet<int>();
        foreach (var t in termes.OrderByDescending(t => t.Terme.Length))
        {
            var motif = new Regex($@"(?<![\p{{L}}\d_-]){Regex.Escape(Encoder(t.Terme))}(?![\p{{L}}\d_-])", RegexOptions.IgnoreCase);
            var resultat = new StringBuilder();
            var remplace = false;
            // On ne touche qu'au texte hors balises et hors <code> / <dfn> déjà posés.
            foreach (var morceau in Morceaux().Split(html))
            {
                if (remplace || morceau.StartsWith('<') || dejaVus.Contains(t.Id)) { resultat.Append(morceau); continue; }
                var m = motif.Match(morceau);
                if (!m.Success) { resultat.Append(morceau); continue; }
                var definition = Encoder(t.Definition + (t.Comparaison is null ? "" : " " + t.Comparaison));
                resultat.Append(morceau[..m.Index])
                    .Append($"<dfn class=\"terme\" tabindex=\"0\" data-definition=\"{definition}\">{m.Value}</dfn>")
                    .Append(morceau[(m.Index + m.Length)..]);
                remplace = true;
                dejaVus.Add(t.Id);
            }
            html = resultat.ToString();
        }
        return html;
    }

    [GeneratedRegex(@"`([^`\n]+)`")] private static partial Regex Code();
    [GeneratedRegex(@"\*\*([^*\n]+)\*\*")] private static partial Regex Gras();
    // Découpe en conservant : les blocs <code>…</code> et <dfn>…</dfn> entiers, et chaque balise.
    [GeneratedRegex(@"(<code>.*?</code>|<dfn[^>]*>.*?</dfn>|<[^>]+>)")] private static partial Regex Morceaux();
}
