using System.Text;

namespace P6Explorer.Infrastructure.Fichiers;

/// <summary>Lecture CSV minimale (virgules, guillemets doubles facultatifs) pour les petits fichiers du dépôt.</summary>
public static class Csv
{
    /// <summary>En-tête et lignes (sans les lignes vides). Les valeurs sont débarrassées des espaces autour.</summary>
    public static (string[] Entete, List<string[]> Lignes) Lire(string chemin)
    {
        var lignes = File.ReadAllLines(chemin, Encoding.UTF8).Where(l => l.Trim().Length > 0).Select(Decouper).ToList();
        if (lignes.Count == 0) return ([], []);
        return (lignes[0], lignes.Skip(1).ToList());
    }

    public static string[] Decouper(string ligne)
    {
        var valeurs = new List<string>();
        var courant = new StringBuilder();
        var entreGuillemets = false;
        for (var i = 0; i < ligne.Length; i++)
        {
            var c = ligne[i];
            if (c == '"')
            {
                if (entreGuillemets && i + 1 < ligne.Length && ligne[i + 1] == '"') { courant.Append('"'); i++; }
                else entreGuillemets = !entreGuillemets;
            }
            else if (c == ',' && !entreGuillemets) { valeurs.Add(courant.ToString().Trim()); courant.Clear(); }
            else courant.Append(c);
        }
        valeurs.Add(courant.ToString().Trim());
        return valeurs.ToArray();
    }
}
