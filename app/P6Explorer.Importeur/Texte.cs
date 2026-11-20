using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace P6Explorer.Importeur;

/// <summary>Petits outils de texte : HTML léger du guide, guillemets, jetons.</summary>
public static partial class Texte
{
    /// <summary>
    /// Convertit le HTML léger du guide en texte balisé simple et sûr :
    /// `code`, **gras**, paragraphes séparés par une ligne vide, listes en « - ».
    /// </summary>
    public static string? Nettoyer(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var t = html.Replace("\r", "");
        t = BaliseCode().Replace(t, m => "`" + m.Groups[1].Value + "`");
        t = BaliseGras().Replace(t, m => "**" + m.Groups[2].Value + "**");
        t = BaliseLi().Replace(t, "\n- ");
        t = BaliseBr().Replace(t, "\n");
        t = BaliseParagraphe().Replace(t, "\n\n");
        t = AutreBalise().Replace(t, "");
        t = WebUtility.HtmlDecode(t);
        t = LignesVides().Replace(t, "\n\n");
        return t.Trim();
    }

    /// <summary>Les morceaux entre &lt;code&gt; d'un texte HTML.</summary>
    public static IEnumerable<string> CodesEnLigne(string? html)
    {
        if (string.IsNullOrEmpty(html)) yield break;
        foreach (Match m in BaliseCode().Matches(html))
            yield return WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
    }

    /// <summary>Découpe en jetons, en respectant les guillemets simples et doubles (retirés).</summary>
    public static List<string> Jetons(string ligne)
    {
        var jetons = new List<string>();
        var courant = new StringBuilder();
        char? guillemet = null;
        var dansJeton = false;
        for (var i = 0; i < ligne.Length; i++)
        {
            var c = ligne[i];
            if (guillemet is not null)
            {
                if (c == guillemet) { guillemet = null; continue; }
                if (c == '\\' && guillemet == '"' && i + 1 < ligne.Length && ligne[i + 1] == '"') { courant.Append('"'); i++; continue; }
                courant.Append(c);
                continue;
            }
            if (c is '\'' or '"') { guillemet = c; dansJeton = true; continue; }
            if (char.IsWhiteSpace(c))
            {
                if (dansJeton) { jetons.Add(courant.ToString()); courant.Clear(); dansJeton = false; }
                continue;
            }
            courant.Append(c);
            dansJeton = true;
        }
        if (dansJeton) jetons.Add(courant.ToString());
        return jetons;
    }

    /// <summary>Découpe une ligne sur des séparateurs (&amp;&amp;, ||, ;) situés hors guillemets et hors parenthèses.</summary>
    public static List<string> DecouperHorsGuillemets(string ligne, params string[] separateurs)
    {
        var morceaux = new List<string>();
        var debut = 0;
        char? guillemet = null;
        var profondeur = 0;
        for (var i = 0; i < ligne.Length; i++)
        {
            var c = ligne[i];
            if (guillemet is not null) { if (c == guillemet) guillemet = null; continue; }
            if (c is '\'' or '"') { guillemet = c; continue; }
            if (c is '(' or '{') profondeur++;
            if (c is ')' or '}') profondeur = Math.Max(0, profondeur - 1);
            if (profondeur > 0) continue;
            foreach (var s in separateurs)
            {
                if (string.CompareOrdinal(ligne, i, s, 0, s.Length) != 0) continue;
                morceaux.Add(ligne[debut..i]);
                i += s.Length - 1;
                debut = i + 1;
                break;
            }
        }
        morceaux.Add(ligne[debut..]);
        return morceaux.Select(m => m.Trim()).Where(m => m.Length > 0).ToList();
    }

    /// <summary>Sépare un commentaire de fin de ligne (« commande   # commentaire »), hors guillemets.</summary>
    public static (string Commande, string? Commentaire) SeparerCommentaire(string ligne)
    {
        char? guillemet = null;
        for (var i = 0; i < ligne.Length; i++)
        {
            var c = ligne[i];
            if (guillemet is not null) { if (c == guillemet) guillemet = null; continue; }
            if (c is '\'' or '"') { guillemet = c; continue; }
            if (c == '#' && i > 0 && char.IsWhiteSpace(ligne[i - 1]))
                return (ligne[..i].TrimEnd(), ligne[(i + 1)..].Trim());
        }
        return (ligne.TrimEnd(), null);
    }

    /// <summary>Compte les ouvertures moins les fermetures d'une ligne (accolades, parenthèses, mots-clés shell).</summary>
    public static int Profondeur(string ligne)
    {
        var p = 0;
        char? guillemet = null;
        foreach (var c in ligne)
        {
            if (guillemet is not null) { if (c == guillemet) guillemet = null; continue; }
            if (c is '\'' or '"') { guillemet = c; continue; }
            if (c is '{' or '(') p++;
            if (c is '}' or ')') p--;
        }
        var sansGuillemets = EntreGuillemets().Replace(ligne, "");
        p += MotDo().Count(sansGuillemets) - MotDone().Count(sansGuillemets);
        p += MotIf().Count(sansGuillemets) - MotFi().Count(sansGuillemets);
        p += MotCase().Count(sansGuillemets) - MotEsac().Count(sansGuillemets);
        return p;
    }

    /// <summary>Terminateur d'un here-document (« &lt;&lt;'EOF' » → EOF), ou null.</summary>
    public static string? Heredoc(string ligne)
    {
        var m = HeredocRx().Match(ligne);
        return m.Success ? m.Groups[1].Value : null;
    }

    public static string Raccourcir(string t, int max) => t.Length <= max ? t : t[..(max - 1)] + "…";

    [GeneratedRegex(@"<code>(.*?)</code>", RegexOptions.Singleline)] private static partial Regex BaliseCode();
    [GeneratedRegex(@"<(b|strong)>(.*?)</\1>", RegexOptions.Singleline)] private static partial Regex BaliseGras();
    [GeneratedRegex(@"<li[^>]*>")] private static partial Regex BaliseLi();
    [GeneratedRegex(@"<br\s*/?>")] private static partial Regex BaliseBr();
    [GeneratedRegex(@"</?p[^>]*>")] private static partial Regex BaliseParagraphe();
    [GeneratedRegex(@"<[^>]+>")] private static partial Regex AutreBalise();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex LignesVides();
    [GeneratedRegex(@"'[^']*'|""[^""]*""")] private static partial Regex EntreGuillemets();
    [GeneratedRegex(@"(^|[\s;])do(\s|$)")] private static partial Regex MotDo();
    [GeneratedRegex(@"(^|[\s;])done(\s|;|$)")] private static partial Regex MotDone();
    [GeneratedRegex(@"(^|;|then|else)\s*if\s")] private static partial Regex MotIf();
    [GeneratedRegex(@"(^|[\s;])fi(\s|;|$)")] private static partial Regex MotFi();
    [GeneratedRegex(@"(^|[\s;])case\s")] private static partial Regex MotCase();
    [GeneratedRegex(@"(^|[\s;])esac(\s|;|$)")] private static partial Regex MotEsac();
    [GeneratedRegex(@"<<-?\s*['""]?([A-Za-z_]+)['""]?")] private static partial Regex HeredocRx();
}
