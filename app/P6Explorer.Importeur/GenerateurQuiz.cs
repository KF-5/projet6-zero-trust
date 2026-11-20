using System.Text.RegularExpressions;
using P6Explorer.Domain.Amorcage;
using P6Explorer.Domain.Commandes;

namespace P6Explorer.Importeur;

/// <summary>
/// Quiz d'auto-évaluation (un par étape), tirés du guide, au statut « à valider » pour que le responsable relise :
/// - « À quoi sert cette étape ? » : bonne réponse = le but ; les autres choix = les buts d'autres étapes du rôle ;
/// - une ou deux pannes : « Quelle est la cause ? » ; les autres choix = des causes d'autres pannes ;
/// - « Pourquoi cette étape ? » quand l'étape n'a pas de panne ;
/// - les 5 « questions probables du jury » du rôle, rattachées à l'étape dont le texte leur ressemble le plus.
/// Tout est déterministe : relancer l'importeur redonne les mêmes questions.
/// </summary>
public static partial class GenerateurQuiz
{
    private const int Distracteurs = 3;

    public static void Generer(IReadOnlyList<FichierRole> fichiers)
    {
        var toutesLesPannes = fichiers.SelectMany(f => f.Role.Etapes.SelectMany(e => e.Pannes.Select(p => (f.Role.Numero, e.Code, p)))).ToList();
        foreach (var f in fichiers)
        {
            var etapes = f.Role.Etapes;
            f.Quiz = etapes.Select(e => new QuizAmorce { Etape = e.Code }).ToList();
            var parCode = f.Quiz.ToDictionary(q => q.Etape);

            foreach (var e in etapes)
            {
                var quiz = parCode[e.Code];
                var autres = etapes.Where(x => x.Code != e.Code).ToList();
                quiz.Questions.Add(Question($"À quoi sert l'étape « {e.Titre} » ?", "but", Court(e.But),
                    Choisir(autres.Select(x => Court(x.But)), e.Code), "C'est le but de l'étape, tel que le guide l'écrit."));

                foreach (var p in e.Pannes.Take(2))
                {
                    var faux = toutesLesPannes.Where(x => x.p != p).OrderBy(x => x.Numero == f.Role.Numero ? 0 : 1)
                        .Select(x => Court(x.p.Cause));
                    quiz.Questions.Add(Question($"Panne : « {Court(p.Symptome, 160)} ». Quelle en est la cause la plus probable ?", "panne",
                        Court(p.Cause), Choisir(faux, e.Code + p.Ordre), $"Solution du guide : {Court(p.Solution)}"));
                }
                if (e.Pannes.Count == 0 && !string.IsNullOrWhiteSpace(e.Pourquoi))
                    quiz.Questions.Add(Question($"Pourquoi l'étape « {e.Titre} » est-elle nécessaire ?", "pourquoi", Court(e.Pourquoi),
                        Choisir(autres.Select(x => Court(x.Pourquoi)), e.Code + "p"), "C'est le « pourquoi » de l'étape (référence au rapport)."));
            }

            // Questions du jury : une réponse rédigée ; les autres réponses du rôle servent de choix faux.
            for (var i = 0; i < f.QuestionsJury.Count; i++)
            {
                var j = f.QuestionsJury[i];
                var cible = etapes.MaxBy(e => Ressemblance(j.Question + " " + j.Reponse, e)) ?? etapes[^1];
                var faux = f.QuestionsJury.Where((_, k) => k != i).Select(x => Court(x.Reponse));
                parCode[cible.Code].Questions.Add(Question(Court(j.Question, 300), "jury", Court(j.Reponse), Choisir(faux, j.Question),
                    "Réponse préparée pour la soutenance (guide, « questions probables du jury »)."));
            }
        }
    }

    private static QuestionAmorce Question(string texte, string source, string bonne, IEnumerable<string> fausses, string explication)
    {
        var q = new QuestionAmorce { Texte = texte, Source = source };
        q.Choix.Add(new ChoixAmorce { Texte = bonne, Correct = true, Explication = explication });
        foreach (var x in fausses.Where(x => x != bonne).Distinct().Take(Distracteurs))
            q.Choix.Add(new ChoixAmorce { Texte = x });
        return q;
    }

    /// <summary>Choix faux, pris dans un ordre stable propre à chaque question.</summary>
    private static IEnumerable<string> Choisir(IEnumerable<string> candidats, string graine)
    {
        var g = graine.Aggregate(17, (h, c) => h * 31 + c);
        return candidats.Where(c => c.Length > 0).Distinct().Select((c, i) => (c, k: (i * 7919 + g) & 0x7fffffff)).OrderBy(x => x.k).Select(x => x.c);
    }

    private static int Ressemblance(string texte, Etape e)
    {
        var mots = Mots(texte);
        return Mots($"{e.Titre} {e.But} {e.Pourquoi} {string.Join(" ", e.Pannes.Select(p => p.Symptome))}").Count(mots.Contains);
    }

    private static HashSet<string> Mots(string t)
        => MotsRegex().Matches(t.ToLowerInvariant()).Select(m => m.Value).Where(m => m.Length >= 5 && !Vides.Contains(m)).ToHashSet();

    private static readonly HashSet<string> Vides = ["comme", "cette", "votre", "notre", "leurs", "parce", "quand", "toute", "toutes", "chaque", "entre", "aussi", "alors", "après", "avant", "section", "rapport"];

    /// <summary>Texte d'un choix : sans Markdown, une seule ligne, coupé proprement.</summary>
    public static string Court(string? t, int max = 240)
    {
        var s = Espaces().Replace((t ?? "").Replace("**", "").Replace("`", ""), " ").Trim();
        if (s.Length <= max) return s;
        var coupe = s.LastIndexOf(' ', max - 1);
        return s[..(coupe > max / 2 ? coupe : max - 1)].TrimEnd(',', ';', ':', ' ') + "…";
    }

    [GeneratedRegex(@"[\p{L}\d-]+")] private static partial Regex MotsRegex();
    [GeneratedRegex(@"\s+")] private static partial Regex Espaces();
}
