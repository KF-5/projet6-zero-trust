// P6Explorer.Importeur — lit les données du guide (converties en JSON par outils/guide-vers-json.mjs)
// et écrit donnees/commandes/role-1.json … role-6.json, plus docs/a-completer.md.
//
// Usage, à la racine du dépôt :
//   node outils/guide-vers-json.mjs
//   dotnet run --project app/P6Explorer.Importeur [-- <racine du dépôt>]
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using P6Explorer.Domain.Commandes;
using P6Explorer.Importeur;

Console.OutputEncoding = Encoding.UTF8;
var racine = args.Length > 0 ? Path.GetFullPath(args[0]) : TrouverRacine(Directory.GetCurrentDirectory());
var dossierGuide = Path.Combine(racine, "donnees", "guide");
if (!File.Exists(Path.Combine(dossierGuide, "role-1.json")))
{
    Console.Error.WriteLine($"Données du guide absentes dans {dossierGuide}.");
    Console.Error.WriteLine("Lance d'abord, à la racine du dépôt :  node outils/guide-vers-json.mjs");
    return 1;
}

var corrections = Corrections.Charger(Path.Combine(racine, "donnees", "corrections", "etapes-faites.json"));
var carnet = new Carnet();
var guides = Enumerable.Range(1, 6).Select(n => JsonDocument.Parse(File.ReadAllText(Path.Combine(dossierGuide, $"role-{n}.json"))).RootElement).ToList();
var depliage = new Depliage(racine, guides);
var constructeur = new ConstructeurRole(racine, corrections, carnet, depliage);
var options = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // accents lisibles dans le fichier
};
var sortie = Path.Combine(racine, "donnees", "commandes");
Directory.CreateDirectory(sortie);

var bilan = new StringBuilder();
int totalEtapes = 0, totalCommandes = 0, totalEffets = 0, totalSous = 0;
// 1. Construction des six rôles, 2. quiz (les choix faux viennent de tous les rôles), 3. écriture.
var fichiers = new List<P6Explorer.Domain.Amorcage.FichierRole>();
for (var n = 1; n <= 6; n++)
    fichiers.Add(constructeur.Construire(guides[n - 1]));
GenerateurQuiz.Generer(fichiers);

foreach (var fichier in fichiers)
{
    var n = fichier.Role.Numero;
    var json = JsonSerializer.Serialize(fichier, options);
    if (Secrets.ContientSecret(json))
        throw new InvalidOperationException($"Un secret subsiste dans role-{n}.json : importation arrêtée.");
    File.WriteAllText(Path.Combine(sortie, $"role-{n}.json"), json + "\n", new UTF8Encoding(false));

    var commandes = fichier.Role.Etapes.SelectMany(e => e.Commandes).ToList();
    var sous = commandes.Sum(c => c.SousCommandes.Count);
    var effets = commandes.Sum(c => c.Effets.Count + c.SousCommandes.Sum(x => x.Effets.Count));
    var parShell = commandes.GroupBy(c => c.Shell).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}");
    var questions = fichier.Quiz.Sum(q => q.Questions.Count);
    Console.WriteLine($"rôle {n} : {fichier.Role.Etapes.Count} étapes, {commandes.Count} commandes (+{sous} sous-commandes), {effets} effets, {questions} questions de quiz ({string.Join(", ", parShell)})");
    bilan.AppendLine($"| {n} | {fichier.Role.Etapes.Count} | {commandes.Count} | {commandes.Count(c => c.Shell is not (P6Explorer.Domain.Enumerations.Shell.Document))} | {sous} | {effets} |");
    totalSous += sous;
    totalEtapes += fichier.Role.Etapes.Count;
    totalCommandes += commandes.Count;
    totalEffets += effets;
}
Console.WriteLine($"Total : {totalEtapes} étapes, {totalCommandes} commandes, {totalEffets} effets ; {carnet.EffetsGeneriques.Count} commandes à compléter.");
Console.WriteLine($"Scripts dépliés : {string.Join(", ", depliage.Deplies.Select(d => $"{d.Key} ({d.Value})"))}.");

EcrireACompleter(Path.Combine(racine, "docs", "a-completer.md"), carnet, depliage, bilan.ToString(), totalEtapes, totalCommandes, totalSous, totalEffets);
return 0;

static string TrouverRacine(string depart)
{
    for (var d = new DirectoryInfo(depart); d is not null; d = d.Parent)
        if (Directory.Exists(Path.Combine(d.FullName, "sources", "guide"))) return d.FullName;
    throw new DirectoryNotFoundException("Racine du dépôt introuvable (dossier sources/guide). Donne-la en argument.");
}

static void EcrireACompleter(string chemin, Carnet carnet, Depliage depliage, string bilan, int etapes, int commandes, int sousCommandes, int effets)
{
    var md = new StringBuilder();
    md.AppendLine("# Commandes à compléter");
    md.AppendLine();
    md.AppendLine("Fichier **généré** par `P6Explorer.Importeur` (ne pas modifier à la main : relancer l'importeur). Il liste ce que l'importation automatique n'a pas su traduire en effets précis.");
    md.AppendLine();
    md.AppendLine("## Bilan de l'importation");
    md.AppendLine();
    md.AppendLine("| Rôle | Étapes | Commandes | dont exécutables | Sous-commandes (scripts dépliés) | Effets |");
    md.AppendLine("|---|---|---|---|---|---|");
    md.Append(bilan);
    md.AppendLine($"| **Total** | **{etapes}** | **{commandes}** | | **{sousCommandes}** | **{effets}** |");
    md.AppendLine();
    md.AppendLine("Chaque commande a au moins un effet visible. « Document » = tableau ou plan du guide, affiché sans être exécuté. Tous les effets proposés sont marqués « automatique » jusqu'à leur relecture.");
    md.AppendLine();
    md.AppendLine($"## 1. Effet générique ({carnet.EffetsGeneriques.Count} commandes)");
    md.AppendLine();
    md.AppendLine("Aucun motif reconnu : la commande met seulement sa machine en valeur. À compléter (jalons 4 et 6).");
    md.AppendLine();
    foreach (var (code, texte) in carnet.EffetsGeneriques)
        md.AppendLine($"- `{code}` — `{Texte.Raccourcir(texte.Replace("`", "'"), 110)}`");
    md.AppendLine();
    md.AppendLine($"## 2. Scripts dépliés en sous-commandes ({depliage.Deplies.Count})");
    md.AppendLine();
    md.AppendLine("Prompt, règle 4 : chaque script long devient une suite de sous-commandes avec leurs effets. Source : le script réel de `plateforme/labo/` s'il existe, sinon le fichier complet du guide ; `test_flux.sh` est déplié depuis `donnees/politique/tests-A.csv` et `tests-B.csv`.");
    md.AppendLine();
    foreach (var (script, nombre) in depliage.Deplies.OrderBy(d => d.Key))
        md.AppendLine($"- `{script}` : {nombre} sous-commandes");
    md.AppendLine();
    md.AppendLine($"Scripts restant à déplier ({carnet.ScriptsADeplier.Count}) : {(carnet.ScriptsADeplier.Count == 0 ? "aucun." : "")}");
    foreach (var (script, codes) in carnet.ScriptsADeplier)
        md.AppendLine($"- `{script}` — {string.Join(", ", codes.Select(c => $"`{c}`"))} (contenu introuvable dans le guide et dans plateforme/labo/)");
    md.AppendLine();
    md.AppendLine("## 3. Remarques");
    md.AppendLine();
    md.AppendLine("- Les paquets ont un verdict « à calculer » : le moteur réseau (jalon 3) donnera le chemin, le NAT, le verdict et la règle selon l'état A ou B.");
    foreach (var r in carnet.Remarques.Distinct()) md.AppendLine($"- {r}");
    File.WriteAllText(chemin, md.ToString(), new UTF8Encoding(false));
}
