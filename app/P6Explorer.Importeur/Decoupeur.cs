namespace P6Explorer.Importeur;

/// <summary>Une commande extraite d'un bloc : son texte et les commentaires du guide qui l'entourent.</summary>
public sealed record CommandeBrute(string Texte, string? CommentaireAvant, string? CommentaireFin);

/// <summary>
/// Découpe un bloc de code du guide en commandes : une ligne utile = une commande, sauf quand la commande
/// continue sur les lignes suivantes (« \ » final, here-document, boucle for … done, bloc { … }).
/// Les lignes de commentaire deviennent l'explication de la commande qui suit.
/// </summary>
public static class Decoupeur
{
    public static List<CommandeBrute> Decouper(string code)
    {
        var lignes = code.Replace("\r", "").Split('\n');
        var resultat = new List<CommandeBrute>();
        var commentaires = new List<string>();

        for (var i = 0; i < lignes.Length; i++)
        {
            var brute = lignes[i];
            var ligne = brute.Trim();
            if (ligne.Length == 0) continue;
            if (EstCommentaire(ligne))
            {
                commentaires.Add(ligne.TrimStart('#', '/', ' ').Trim());
                continue;
            }

            var tampon = new List<string> { brute.TrimEnd() };
            var (sansCommentaire, commentaireFin) = Texte.SeparerCommentaire(ligne);
            var finHeredoc = Texte.Heredoc(sansCommentaire);
            var profondeur = Texte.Profondeur(sansCommentaire);
            var courante = sansCommentaire;

            while (i + 1 < lignes.Length && (finHeredoc is not null || profondeur > 0
                   || courante.EndsWith('\\') || courante.EndsWith('|') || courante.EndsWith("&&")))
            {
                i++;
                var suivante = lignes[i].Replace("\r", "");
                tampon.Add(suivante.TrimEnd());
                if (finHeredoc is not null)
                {
                    if (suivante.Trim() == finHeredoc) finHeredoc = null;
                    courante = "";
                    continue;
                }
                var (s, _) = Texte.SeparerCommentaire(suivante.Trim());
                finHeredoc = Texte.Heredoc(s);
                profondeur += Texte.Profondeur(s);
                courante = s;
            }

            string texte;
            if (tampon.Count == 1)
            {
                texte = sansCommentaire;
            }
            else
            {
                texte = string.Join("\n", tampon);
                commentaireFin = null;
            }

            resultat.Add(new CommandeBrute(texte.Trim(),
                commentaires.Count > 0 ? string.Join("\n", commentaires) : null, commentaireFin));
            commentaires.Clear();
        }

        // Commentaires restés seuls à la fin : rattachés à la dernière commande.
        if (commentaires.Count > 0 && resultat.Count > 0)
        {
            var d = resultat[^1];
            resultat[^1] = d with { CommentaireFin = string.Join(" ", new[] { d.CommentaireFin }.Concat(commentaires).Where(x => x is not null)) };
        }
        return resultat;
    }

    /// <summary>Texte complet des commentaires d'un bloc qui n'en contient pas d'autre.</summary>
    public static string Commentaires(string code)
        => string.Join("\n", code.Replace("\r", "").Split('\n')
            .Select(l => l.Trim()).Where(EstCommentaire).Select(l => l.TrimStart('#', '/', ' ')));

    public static bool EstCommentaire(string ligne)
        => (ligne.StartsWith('#') && !ligne.StartsWith("#!")) || ligne.StartsWith("//");
}
