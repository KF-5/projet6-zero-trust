using System.Text.Json;
using System.Text.RegularExpressions;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Importeur;

/// <summary>
/// Prompt, règle 4 : un script long se déplie en sous-commandes, chacune avec ses effets.
/// Sources, dans l'ordre : le script réel de plateforme/labo/ (ce qui a vraiment tourné), sinon le fichier
/// complet donné par le guide. Trois cas particuliers : tests.sh et tests-cloisonnement.sh (un test = une
/// sous-commande et un paquet), test_flux.sh (une ligne de tests.csv = une sous-commande), et les programmes
/// Python (une section « # ---------- … ---------- » ou une fonction = une sous-commande).
/// </summary>
public sealed partial class Depliage
{
    private readonly string _racine;
    private readonly Dictionary<string, string> _scriptsReels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _fichiersGuide = new(StringComparer.Ordinal);

    public Dictionary<string, int> Deplies { get; } = new(StringComparer.Ordinal);

    public Depliage(string racine, IEnumerable<JsonElement> guides)
    {
        _racine = racine;
        var labo = Path.Combine(racine, "plateforme", "labo");
        if (Directory.Exists(labo))
            foreach (var f in Directory.GetFiles(labo, "*.sh", SearchOption.AllDirectories))
                _scriptsReels.TryAdd(Path.GetFileName(f), File.ReadAllText(f).Replace("\r", ""));
        foreach (var g in guides) Collecter(g);
    }

    private void Collecter(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var x in e.EnumerateArray()) Collecter(x);
                break;
            case JsonValueKind.Object:
                if (e.TryGetProperty("file", out var f) && f.ValueKind == JsonValueKind.String && e.TryGetProperty("code", out var c))
                    _fichiersGuide.TryAdd(NomDeBase(f.GetString()!), c.GetString() ?? "");
                foreach (var p in e.EnumerateObject()) Collecter(p.Value);
                break;
        }
    }

    /// <summary>« capture/capture.sh (installé dans …) » → « capture.sh ».</summary>
    public static string NomDeBase(string chemin)
        => Path.GetFileName(chemin.Split(" (")[0].Split(',')[0].Trim().TrimEnd('/'));

    /// <summary>Ajoute les sous-commandes du script à la commande ; faux si le script est introuvable.</summary>
    public bool Deplier(string script, Commande commande, int role, int etape)
    {
        if (commande.SousCommandes.Count > 0) return true;
        var nom = NomDeBase(script);
        // Script lancé par « ssh compte@adresse "…" » : ses commandes tournent sur la machine visée.
        var cible = SshCible().Match(commande.Texte);
        var ou = cible.Success && Laboratoire.Resoudre(cible.Groups[1].Value) is { } n ? n : commande.Ou;
        var ajoutees = nom switch
        {
            "tests.sh" when _scriptsReels.TryGetValue(nom, out var s) => TestsSh(s),
            "tests-cloisonnement.sh" when _scriptsReels.TryGetValue(nom, out var s) => Cloisonnement(s),
            "test_flux.sh" => TestFlux(role == 2 && etape == 9 ? "B" : "A"),
            _ when nom.EndsWith(".py") && _fichiersGuide.TryGetValue(nom, out var py) => Python(py, ou, nom),
            _ when _scriptsReels.TryGetValue(nom, out var s) => Bash(s, ou, nom),
            _ when _fichiersGuide.TryGetValue(nom, out var s) => Bash(s, ou, nom),
            _ => null
        };
        if (ajoutees is null || ajoutees.Count == 0) return false;
        for (var i = 0; i < ajoutees.Count; i++)
        {
            var s = ajoutees[i];
            s.Ordre = i + 1;
            s.Code = $"{commande.Code}-s{i + 1:00}";
            s.Compte ??= s.Ou == NoeudId.Opnsense ? "root" : Laboratoire.EstVmLinux(s.Ou) ? commande.Compte ?? "karim" : null;
            s.Texte = Secrets.Masquer(s.Texte) ?? "";
            s.Explication = Secrets.Masquer(s.Explication) ?? "";
            for (var j = 0; j < s.Effets.Count; j++) { s.Effets[j].Ordre = j + 1; s.Effets[j].Automatique = true; }
            commande.SousCommandes.Add(s);
        }
        Deplies[nom] = ajoutees.Count;
        return true;
    }

    // ---------------------------------------------------------------- tests.sh (12 tests, depuis les conteneurs)

    private static List<Commande> TestsSh(string script)
    {
        var liste = new List<Commande>();
        foreach (Match m in TestFluxLigne().Matches(script))
        {
            var (desc, attendu, conteneur, cmd) = (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value.Trim());
            var de = new NoeudId(conteneur);
            var c = new Commande
            {
                Ou = NoeudId.Docker, Shell = Shell.Bash, Bloc = "tests.sh",
                Texte = $"sudo docker exec {conteneur} {cmd}",
                Explication = $"Test : {desc}. Attendu : {(attendu == "passe" ? "la connexion aboutit" : "bloqué (tout échec compte)")}. La commande est lancée dans le conteneur {conteneur}.",
                EstTest = true,
            };
            c.Effets.AddRange(PaquetsDe(cmd, de));
            liste.Add(c);
        }
        return liste;
    }

    // ---------------------------------------------------------------- tests-cloisonnement.sh (19 tests, depuis Kali)

    private static List<Commande> Cloisonnement(string script)
    {
        var liste = new List<Commande>();
        foreach (Match m in TestKali().Matches(script))
        {
            var (desc, attendu, cmd) = (m.Groups[1].Value.Trim(), m.Groups[2].Value, m.Groups[3].Value.Trim());
            var c = new Commande
            {
                Ou = NoeudId.Kali, Shell = Shell.Bash, Bloc = "tests-cloisonnement.sh", Texte = cmd, EstTest = true,
                Explication = $"Test : {desc}. Attendu : {(attendu == "passe" ? "passe" : "bloqué")} (ENF1 : rien ne doit sortir du laboratoire, Kali n'administre personne).",
            };
            c.Effets.AddRange(PaquetsDe(cmd, NoeudId.Kali));
            liste.Add(c);
        }
        return liste;
    }

    // ---------------------------------------------------------------- test_flux.sh (tests.csv de l'état A ou B)

    private List<Commande> TestFlux(string etat)
    {
        var chemin = Path.Combine(_racine, "donnees", "politique", $"tests-{etat}.csv");
        var hotes = Path.Combine(_racine, "donnees", "politique", "hosts.csv");
        if (!File.Exists(chemin) || !File.Exists(hotes)) return [];
        var conteneurs = File.ReadAllLines(hotes).Skip(1).Select(l => l.Split(',')).Where(c => c.Length >= 5).ToDictionary(c => c[0], c => c[4]);
        var liste = new List<Commande>();
        foreach (var l in File.ReadAllLines(chemin).Skip(1).Where(l => l.Length > 0))
        {
            var c = l.Split(',');
            var (id, src, dst, proto, port, attendu, flux) = (c[0], c[1], c[2], c[3], int.Parse(c[4]), c[5], c[6]);
            var conteneur = conteneurs.GetValueOrDefault(src, src);
            var dig = proto == "udp" && port == 53;
            var commande = new Commande
            {
                Ou = NoeudId.Docker, Shell = Shell.Bash, Bloc = $"test_flux.sh · tests.csv (état {etat})", EstTest = true,
                Texte = dig
                    ? $"sudo nsenter -t $(docker inspect -f '{{{{.State.Pid}}}}' {conteneur}) -n dig @{dst} www.labo.test +time=2 +tries=1"
                    : $"sudo nsenter -t $(docker inspect -f '{{{{.State.Pid}}}}' {conteneur}) -n nc -zv -w 2 {dst} {port}",
                Explication = $"{id} ({flux}) : {src} → {dst} {proto}/{port}, attendu « {attendu} ». test_flux entre dans l'espace réseau du conteneur (nsenter, E23) ; "
                              + (dig ? "dig doit obtenir une réponse." : "un RST du service compte « pass » : le pare-feu a laissé passer.")
                              + (etat == "B" ? " À rejouer avec l'état B affiché." : ""),
            };
            commande.Effets.Add(new Paquet
            {
                De = new NoeudId(conteneur), Vers = Laboratoire.Resoudre(dst) ?? NoeudId.Opnsense, AdresseVisee = dst,
                Proto = dig ? Protocole.Dns : Protocole.Tcp, Port = port
            });
            liste.Add(commande);
        }
        return liste;
    }

    // ---------------------------------------------------------------- scripts shell (réels ou du guide)

    private static readonly string[] Ignorer = ["set -", "echo ", "printf ", "cd \"$(dirname", "ok=0", "trap ", "exit", "fi", "done", "}"];

    private static List<Commande> Bash(string script, NoeudId ou, string nom)
    {
        var liste = new List<Commande>();
        string? section = null;
        foreach (var b in Decoupeur.Decouper(script))
        {
            var texte = b.Texte.Trim();
            var etape = EtapeScript().Match(texte);
            if (etape.Success) { section = etape.Groups[1].Value; continue; }
            if (texte.StartsWith("#!") || Ignorer.Any(texte.StartsWith) || FonctionShell().IsMatch(texte)
                || texte.StartsWith("[ \"$(id -u)\"") || texte.StartsWith("command -v") && texte.Contains("||") && !texte.Contains("apt")) continue;
            var c = new Commande
            {
                Ou = ou, Shell = Shell.Bash, Bloc = section is null ? nom : $"{nom} · {section}", Texte = texte,
                Explication = string.Join("\n", new[] { b.CommentaireAvant ?? b.CommentaireFin, Explications.PourCommande(texte) }
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
            };
            var analyse = Analyseur.Analyser(texte.Split('\n')[0], ou, Shell.Bash, null);
            c.Effets.AddRange(analyse.Effets);
            foreach (var f in FichierEcrit().Matches(texte).Select(m => m.Groups[1].Value).Distinct())
                if (!c.Effets.OfType<Fichier>().Any(x => x.Chemin == f)) c.Effets.Add(new Fichier { Noeud = ou, Chemin = f });
            liste.Add(c);
        }
        return liste;
    }

    // ---------------------------------------------------------------- programmes Python (du guide)

    private static List<Commande> Python(string programme, NoeudId ou, string nom)
    {
        var lignes = programme.Replace("\r", "").Split('\n');
        var sections = new List<(string Titre, List<string> Lignes)>();
        var marques = lignes.Select((l, i) => (l, i)).Where(x => SectionPython().IsMatch(x.l)).ToList();
        if (marques.Count >= 2)
        {
            for (var k = 0; k < marques.Count; k++)
            {
                var fin = k + 1 < marques.Count ? marques[k + 1].i : lignes.Length;
                sections.Add((SectionPython().Match(marques[k].l).Groups[1].Value.Trim(), lignes[(marques[k].i + 1)..fin].ToList()));
            }
        }
        else
        {
            // Pas de marques : une fonction de premier niveau = une étape du programme.
            var debuts = lignes.Select((l, i) => (l, i)).Where(x => FonctionPython().IsMatch(x.l)).ToList();
            for (var k = 0; k < debuts.Count; k++)
            {
                var fin = k + 1 < debuts.Count ? debuts[k + 1].i : lignes.Length;
                var corps = lignes[debuts[k].i..fin].ToList();
                var doc = corps.Skip(1).FirstOrDefault(x => x.Trim().StartsWith("\"\"\""))?.Trim().Trim('"');
                sections.Add(($"{FonctionPython().Match(debuts[k].l).Groups[1].Value}(){(string.IsNullOrWhiteSpace(doc) ? "" : " : " + doc)}", corps));
            }
        }
        var liste = new List<Commande>();
        foreach (var (titre, corps) in sections.Where(s => s.Lignes.Any(l => l.Trim().Length > 0)))
        {
            var texte = string.Join("\n", corps).Trim('\n');
            var c = new Commande
            {
                Ou = ou, Shell = Shell.Editeur, Bloc = $"{nom} · {titre}", Texte = texte,
                Explication = $"Partie « {titre} » du programme {nom}." + Resume(texte),
            };
            foreach (var f in FichierPython().Matches(texte).Select(m => m.Groups[1].Value).Distinct())
                c.Effets.Add(new Fichier { Noeud = ou, Chemin = f });
            foreach (Match m in SshPython().Matches(texte))
                if (Laboratoire.Resoudre(m.Groups[1].Value) is { } vers)
                    c.Effets.Add(new Paquet { De = ou, Vers = vers, AdresseVisee = m.Groups[1].Value, Proto = Protocole.Ssh, Port = 22 });
            if (c.Effets.Count == 0) c.Effets.Add(new Surbrillance { Noeud = ou, Fichier = nom, Texte = $"{nom} : {titre}" });
            liste.Add(c);
        }
        return liste;
    }

    private static string Resume(string code)
    {
        var parties = new List<string>();
        if (code.Contains("csv.")) parties.Add("lit ou écrit des fichiers CSV");
        if (code.Contains("json.")) parties.Add("lit ou écrit du JSON");
        if (code.Contains("subprocess") || code.Contains("ssh")) parties.Add("lance des commandes sur d'autres machines par SSH");
        if (code.Contains("sys.exit(1)")) parties.Add("s'arrête en erreur si une vérification échoue");
        return parties.Count == 0 ? "" : $" Cette partie {string.Join(", ", parties)}.";
    }

    // ---------------------------------------------------------------- paquets d'une ligne de test

    private static IEnumerable<Effet> PaquetsDe(string cmd, NoeudId de)
    {
        var socket = SocketPython().Match(cmd);
        if (socket.Success)
        {
            var ip = socket.Groups[1].Value;
            var port = int.Parse(socket.Groups[2].Value);
            return [new Paquet { De = de, Vers = Laboratoire.Resoudre(ip) ?? NoeudId.Opnsense, AdresseVisee = ip, Proto = port == 5432 ? Protocole.PostgreSql : Protocole.Tcp, Port = port }];
        }
        var effets = Analyseur.Analyser(cmd, de, Shell.Bash, null).Effets.Where(e => e is Paquet).ToList();
        foreach (var p in effets.OfType<Paquet>()) p.De = de;
        return effets.Count > 0 ? effets : [new Surbrillance { Noeud = de, Texte = "Test lancé sur cette machine." }];
    }

    [GeneratedRegex(@"test_flux\s+""([^""]+)""\s+(passe|bloque)\s+(\S+)\s*\\\n\s*(.+)")] private static partial Regex TestFluxLigne();
    [GeneratedRegex(@"^t\s+""([^""]+)""\s+(passe|bloque)\s+(.+)$", RegexOptions.Multiline)] private static partial Regex TestKali();
    [GeneratedRegex(@"^etape\s+""([^""]+)""")] private static partial Regex EtapeScript();
    [GeneratedRegex(@"^[\w-]+\s*\(\)\s*\{")] private static partial Regex FonctionShell();
    [GeneratedRegex(@"(?:cat\s*>|tee\s+(?:-a\s+)?)\s*(/[\w./-]+)")] private static partial Regex FichierEcrit();
    [GeneratedRegex(@"^\s*#\s*-{3,}\s*(.+?)\s*-{3,}\s*$")] private static partial Regex SectionPython();
    [GeneratedRegex(@"^def\s+(\w+)\s*\(")] private static partial Regex FonctionPython();
    [GeneratedRegex(@"open\(f?""([^""]+)"",\s*""[wa]")] private static partial Regex FichierPython();
    [GeneratedRegex(@"\bssh\b[^\n""']*?@([\d.]+)")] private static partial Regex SshCible();
    [GeneratedRegex(@"(?:ssh|scp)[^\n]*?@(10\.99\.0\.\d+|10\.\d+\.\d+\.\d+)")] private static partial Regex SshPython();
    [GeneratedRegex(@"create_connection\(\(""([\d.]+)"",\s*(\d+)\)")] private static partial Regex SocketPython();
}
