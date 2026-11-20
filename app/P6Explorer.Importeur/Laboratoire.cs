using System.Text.RegularExpressions;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Importeur;

/// <summary>Ce que l'importeur sait du laboratoire réel : adresses, conteneurs, machines par défaut.</summary>
public static partial class Laboratoire
{
    public static readonly NoeudId ConteneurTest = new("conteneur-test");

    /// <summary>Adresse → machine (plateforme réelle, ECARTS.md E31–E42).</summary>
    public static readonly IReadOnlyDictionary<string, NoeudId> Adresses = new Dictionary<string, NoeudId>
    {
        ["203.0.113.1"] = NoeudId.Opnsense, ["10.99.0.1"] = NoeudId.Opnsense,
        ["10.10.0.1"] = NoeudId.Opnsense, ["10.20.0.1"] = NoeudId.Opnsense,
        ["10.10.0.65"] = NoeudId.Opnsense, ["10.10.0.97"] = NoeudId.Opnsense,
        ["10.20.0.33"] = NoeudId.Opnsense, ["10.20.0.65"] = NoeudId.Opnsense,
        ["10.99.0.2"] = NoeudId.Hote, ["100.125.208.28"] = NoeudId.Hote,
        ["10.99.0.10"] = NoeudId.Collecteur, ["10.99.0.20"] = NoeudId.Docker,
        ["10.99.0.66"] = NoeudId.Kali, ["203.0.113.66"] = NoeudId.Kali,
        ["203.0.113.10"] = NoeudId.ClientExt,
        ["10.20.0.10"] = NoeudId.Nginx, ["10.20.0.20"] = NoeudId.DmzPivot,
        ["10.20.0.40"] = NoeudId.Bind9, ["10.20.0.70"] = NoeudId.Api,
        ["10.10.0.10"] = NoeudId.Client01, ["10.10.0.12"] = NoeudId.Client03,
        ["10.10.0.70"] = NoeudId.Postgres, ["10.10.0.100"] = NoeudId.Admin,
        ["10.10.0.50"] = ConteneurTest,
        ["192.168.1.1"] = NoeudId.Box,
        ["8.8.8.8"] = NoeudId.Internet, ["1.1.1.1"] = NoeudId.Internet, ["151.101.2.132"] = NoeudId.Internet,
        ["deb.debian.org"] = NoeudId.Internet, ["download.docker.com"] = NoeudId.Internet,
        ["debian.org"] = NoeudId.Internet,
    };

    /// <summary>Conteneurs de l'état A (docker-compose.yml du projet p6) et leur adresse.</summary>
    public static readonly IReadOnlyDictionary<string, (NoeudId Id, string Ip, string ReseauA, string ReseauB)> Conteneurs =
        new Dictionary<string, (NoeudId, string, string, string)>
        {
            ["nginx"] = (NoeudId.Nginx, "10.20.0.10", "p6_dmz", "dmz-web"),
            ["iperf3"] = (NoeudId.Iperf3, "10.20.0.10", "p6_dmz", "dmz-web"),
            ["dmz-pivot"] = (NoeudId.DmzPivot, "10.20.0.20", "p6_dmz", "dmz-web"),
            ["bind9"] = (NoeudId.Bind9, "10.20.0.40", "p6_dmz", "dmz-dns"),
            ["api"] = (NoeudId.Api, "10.20.0.70", "p6_dmz", "dmz-app"),
            ["client-01"] = (NoeudId.Client01, "10.10.0.10", "p6_lan", "lan-users"),
            ["client-03"] = (NoeudId.Client03, "10.10.0.12", "p6_lan", "lan-users"),
            ["postgres"] = (NoeudId.Postgres, "10.10.0.70", "p6_lan", "lan-db"),
            ["admin"] = (NoeudId.Admin, "10.10.0.100", "p6_lan", "lan-admin"),
            ["client-ext"] = (NoeudId.ClientExt, "203.0.113.10", "p6_wan", "wan"),
        };

    public static readonly IReadOnlyDictionary<string, NoeudId> Vm = new Dictionary<string, NoeudId>
    {
        ["p6-opnsense"] = NoeudId.Opnsense, ["p6-docker"] = NoeudId.Docker,
        ["p6-collecteur"] = NoeudId.Collecteur, ["p6-kali"] = NoeudId.Kali,
    };

    /// <summary>Machines Debian/Kali où l'on travaille avec le compte karim (sudo requis pour l'administration).</summary>
    public static bool EstVmLinux(NoeudId n) => n == NoeudId.Docker || n == NoeudId.Collecteur || n == NoeudId.Kali;

    public static NoeudId? Resoudre(string? cible)
    {
        if (string.IsNullOrWhiteSpace(cible)) return null;
        var c = cible.Trim().Trim('[', ']');
        var arobase = c.LastIndexOf('@');
        if (arobase >= 0) c = c[(arobase + 1)..];
        var slash = c.IndexOf('/');
        if (slash > 0) c = c[..slash];
        var deuxPoints = c.IndexOf(':');
        if (deuxPoints > 0) c = c[..deuxPoints];
        if (Adresses.TryGetValue(c, out var n)) return n;
        if (Conteneurs.TryGetValue(c, out var ct)) return ct.Id;
        if (c.EndsWith(".p6.lab") || c.EndsWith(".labo.test")) return NoeudId.Opnsense;   // zone publique : 203.0.113.1
        return null;
    }

    /// <summary>Premier hôte connu d'un réseau en notation CIDR (nmap … 10.10.0.64/27 → postgres).</summary>
    public static NoeudId? ResoudreReseau(string cidr)
    {
        var m = Cidr().Match(cidr);
        if (!m.Success) return null;
        var reseau = Ip(m.Groups[1].Value);
        var masque = int.Parse(m.Groups[2].Value) == 0 ? 0u : uint.MaxValue << (32 - int.Parse(m.Groups[2].Value));
        return Adresses.Where(a => IpValide().IsMatch(a.Key) && (Ip(a.Key) & masque) == (reseau & masque)
                                   && a.Value != NoeudId.Opnsense && a.Value != ConteneurTest)
            .Select(a => (NoeudId?)a.Value).FirstOrDefault();
    }

    private static uint Ip(string s) => s.Split('.').Select(uint.Parse).Aggregate(0u, (acc, o) => (acc << 8) | o);

    /// <summary>Machine et shell par défaut d'une étape (corrigés ensuite par le titre du bloc et les commentaires).</summary>
    public static (NoeudId Ou, Shell Shell) ParDefaut(int role, int etape) => (role, etape) switch
    {
        (1, 6) => (NoeudId.PcMembre, Shell.Bash),
        (1, 8) => (NoeudId.Collecteur, Shell.Bash),
        (1, _) => (NoeudId.Hote, Shell.PowerShell),
        (2, 6 or 7 or 9) => (NoeudId.Collecteur, Shell.Bash),
        (2, _) => (NoeudId.Opnsense, Shell.OpnsenseShell),
        (3, 0) => (NoeudId.PcMembre, Shell.Bash),
        (3, 3) => (NoeudId.Docker, Shell.Bash),
        (3, 5 or 6) => (NoeudId.Collecteur, Shell.Bash),
        (3, _) => (NoeudId.Opnsense, Shell.OpnsenseShell),
        (4, _) => (NoeudId.Docker, Shell.Bash),
        (5, 1) => (NoeudId.Kali, Shell.Bash),
        (5, _) => (NoeudId.Collecteur, Shell.Bash),
        _ => (NoeudId.Collecteur, Shell.Bash),
    };

    /// <summary>Indice de machine trouvé dans un titre de bloc ou un commentaire (null : rien de clair).</summary>
    public static (NoeudId Ou, Shell Shell)? Indice(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte)) return null;
        var t = texte.ToLowerInvariant();
        if (t.Contains("powershell") || t.Contains("sur windows") || t.Contains("depuis windows") || t.Contains("hôte windows"))
            return (NoeudId.Hote, Shell.PowerShell);
        if (t.Contains("ubuntu") || t.Contains("wsl") || t.Contains("sur le pc") || t.Contains("(pc)") || t.Contains("pc d'un membre"))
            return (NoeudId.PcMembre, Shell.Bash);
        if (t.Contains("opnsense") && (t.Contains("shell") || t.Contains("console") || t.StartsWith("opnsense") || t.Contains("sur opnsense") || t.Contains("dans la vm")))
            return (NoeudId.Opnsense, Shell.OpnsenseShell);
        if (t.Contains("kali")) return (NoeudId.Kali, Shell.Bash);
        if (t.Contains("collecteur")) return (NoeudId.Collecteur, Shell.Bash);
        if (t.Contains("hôte docker") || t.Contains("p6-docker") || t.Contains("conteneur de l'api") || t.Contains("dans le conteneur"))
            return (NoeudId.Docker, Shell.Bash);
        return null;
    }

    /// <summary>
    /// Indice tiré d'un commentaire : seulement s'il dit où l'on est (« sur l'hôte Docker », « depuis Kali »,
    /// « OPNsense : … »), pas s'il cite une machine en passant (« clé publique du collecteur »).
    /// </summary>
    public static (NoeudId Ou, Shell Shell)? IndiceCommentaire(string? commentaire)
    {
        if (string.IsNullOrWhiteSpace(commentaire)) return null;
        (NoeudId, Shell)? trouve = null;
        foreach (var ligne in commentaire.Split('\n'))
        {
            var t = ligne.Trim().ToLowerInvariant();
            var debut = CommentaireDeLieu().Match(t);
            if (debut.Success && Indice(t[..Math.Min(t.Length, debut.Length + 30)]) is { } i) trouve = i;
        }
        return trouve;
    }

    /// <summary>Indice tiré de la commande elle-même (syntaxe PowerShell, scripts du dépôt).</summary>
    public static (NoeudId Ou, Shell Shell)? IndiceCommande(string commande)
    {
        var c = commande.TrimStart();
        if (PowerShellRx().IsMatch(c)) return (NoeudId.Hote, Shell.PowerShell);
        if (c.StartsWith("python3 politique/") || c.StartsWith("python3 campagne/") || c.StartsWith("python3 analyse/")
            || c.StartsWith("cd /opt/projet6 && python3"))
            return (NoeudId.Collecteur, Shell.Bash);
        return null;
    }

    /// <summary>Premier mot qui ressemble à une commande (pour distinguer un bloc de commandes d'un tableau de texte).</summary>
    public static bool RessembleACommande(string ligne)
    {
        var l = ligne.TrimStart();
        if (l.Length == 0) return false;
        if (PowerShellRx().IsMatch(l) || Affectation().IsMatch(l)) return true;
        if (l[0] is '.' or '/' or '~' or '$' or '&') return true;
        var premier = Texte.Jetons(l).FirstOrDefault() ?? "";
        return CommandesConnues.Contains(premier);
    }

    public static readonly HashSet<string> CommandesConnues = new(StringComparer.Ordinal)
    {
        "ssh","scp","sudo","cd","ls","cat","grep","tail","head","echo","git","mkdir","cp","mv","rm","install","apt","apt-get",
        "systemctl","docker","nft","ip","ping","nc","curl","dig","nmap","hping3","iperf3","psql","python3","python","pfctl",
        "ifconfig","tcpdump","netstat","configctl","service","pgrep","dmesg","suricata","jq","chmod","chown","tee","openssl",
        "wsl","sed","awk","column","tshark","wc","sort","for","while","if","chronyc","timeout","nano","export","set","sleep",
        "kill","pkill","ss","nsenter","usermod","add-apt-repository","find","ln","touch","tar","unzip","zip","nikto","vmrun",
        "named-checkconf","named-checkzone","bash","sh","DEBUT","FIN","CAMP","umask","printf","ntpq","test","wget","history"
    };

    [GeneratedRegex(@"^(\$\w+\s*=|&\s*\$|&\s*'|foreach\s*\(|[A-Z][a-z]+-[A-Z][A-Za-z]+|Select-String|Test-NetConnection|scp\s.*[A-Z]:\\|ssh\s.*\$HOME\\)")]
    private static partial Regex PowerShellRx();
    [GeneratedRegex(@"^[A-Z_][A-Z0-9_]*=")] private static partial Regex Affectation();
    [GeneratedRegex(@"^(\d+\.\s*)?(puis,?\s+)?(sur|depuis|dans|hôte docker|opnsense|kali|collecteur|windows|console)\b")]
    private static partial Regex CommentaireDeLieu();
    [GeneratedRegex(@"^(\d+\.\d+\.\d+\.\d+)/(\d+)$")] private static partial Regex Cidr();
    [GeneratedRegex(@"^\d+\.\d+\.\d+\.\d+$")] private static partial Regex IpValide();
}
