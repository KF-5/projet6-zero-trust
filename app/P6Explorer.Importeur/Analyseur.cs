using System.Text.RegularExpressions;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Importeur;

/// <summary>Effets proposés pour une commande, et ce qu'il reste à faire à la main.</summary>
public sealed class Analyse
{
    public List<Effet> Effets { get; } = [];
    /// <summary>Faux si aucun motif n'a été reconnu (effet générique : à compléter).</summary>
    public bool Reconnue { get; set; }
    /// <summary>Scripts lancés par la commande, à déplier en sous-commandes (prompt, règle 4).</summary>
    public List<string> ScriptsADeplier { get; } = [];
}

/// <summary>
/// Reconnaît les motifs courants (ping, curl, nc, dig, ssh, scp, docker, nmap, hping3, iperf3, psql,
/// systemctl, nft, pfctl, vmrun…) et propose les effets correspondants. Chemin, NAT et verdict des paquets
/// restent vides : le moteur réseau les calcule (jalon 3).
/// </summary>
public static partial class Analyseur
{
    public static Analyse Analyser(string texte, NoeudId ou, Shell shell, string? sortie)
    {
        var a = new Analyse();
        if (shell == Shell.Document)
        {
            a.Effets.Add(new Surbrillance { Noeud = ou, Texte = "Document de référence : rien n'est exécuté." });
            a.Reconnue = true;
        }
        else
        {
            var premiereLigne = texte.Split('\n')[0];
            AnalyserLigne(premiereLigne, texte, ou, a, 0);
        }
        AjouterResultatTest(sortie, a);
        if (a.Effets.Count == 0)
        {
            // Reconnue sans effet propre (cd, affectation de variable…) : on montre la machine où l'on tape.
            a.Effets.Add(new Surbrillance { Noeud = ou, Texte = a.Reconnue ? "Préparation (dossier, variable) : rien ne change sur le réseau." : "Commande tapée sur cette machine." });
        }
        for (var i = 0; i < a.Effets.Count; i++)
        {
            a.Effets[i].Ordre = i + 1;
            a.Effets[i].Automatique = true;
        }
        return a;
    }

    private static void AnalyserLigne(string ligne, string texteComplet, NoeudId de, Analyse a, int profondeur)
    {
        if (profondeur > 4) return;

        // Boucles d'une ligne : for x in a b c; do … ; done  /  foreach ($v in 'a','b') { … }
        var boucle = BoucleBash().Match(ligne);
        if (boucle.Success)
        {
            foreach (var valeur in boucle.Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                AnalyserLigne(Variable(boucle.Groups[3].Value, boucle.Groups[1].Value, valeur), texteComplet, de, a, profondeur + 1);
            a.Reconnue = true;
            return;
        }
        var boucleBashMulti = BoucleBashDebut().Match(ligne);
        if (boucleBashMulti.Success && texteComplet.Contains('\n'))
        {
            var corps = string.Join("\n", texteComplet.Split('\n').Skip(1).Where(l => l.Trim() is not ("done" or "do")));
            foreach (var valeur in boucleBashMulti.Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                foreach (var l in corps.Split('\n').Where(l => l.Trim().Length > 0))
                    AnalyserLigne(Variable(l.Trim(), boucleBashMulti.Groups[1].Value, valeur), l, de, a, profondeur + 1);
            a.Reconnue = true;
            return;
        }
        var foreachPs = ForeachPowerShell().Match(texteComplet);
        if (foreachPs.Success)
        {
            var valeurs = ValeursPs().Matches(foreachPs.Groups[2].Value).Select(m => m.Groups[1].Value);
            foreach (var v in valeurs)
                AnalyserLigne(Variable(foreachPs.Groups[3].Value.Trim(), foreachPs.Groups[1].Value, v), foreachPs.Groups[3].Value, de, a, profondeur + 1);
            a.Reconnue = true;
            return;
        }

        foreach (var segment in Texte.DecouperHorsGuillemets(ligne, "&&", "||", ";"))
        {
            var premier = Texte.DecouperHorsGuillemets(segment, "|").FirstOrDefault() ?? segment;
            AnalyserSegment(premier, texteComplet, de, a, profondeur);
        }
    }

    private static void AnalyserSegment(string segment, string texteComplet, NoeudId de, Analyse a, int profondeur)
    {
        var j = Texte.Jetons(segment);
        // redirections et mise en arrière-plan : « > /tmp/j1.txt », « 2>/dev/null », « & » final
        for (var i = j.Count - 1; i >= 1; i--)
        {
            if (j[i] is ">" or ">>" or "2>" && i + 1 < j.Count) { j.RemoveRange(i, 2); continue; }
            if (j[i] == "&" || j[i].StartsWith('>') || j[i].StartsWith("2>")) j.RemoveAt(i);
        }
        // préfixes sans effet propre
        while (j.Count > 0 && (j[0] is "sudo" or "nohup" or "exec" || Affectation().IsMatch(j[0]) || j[0] == "timeout"))
        {
            if (j[0] == "timeout" && j.Count > 1) j.RemoveAt(1);
            j.RemoveAt(0);
        }
        if (j.Count == 0) { a.Reconnue = true; return; }
        var cmd = j[0];
        var reste = j.Skip(1).ToList();
        // PowerShell : « & $vr -T ws snapshot … » appelle vmrun.exe
        if (cmd == "&" && (reste.FirstOrDefault() == "$vr" || segment.Contains("vmrun"))) { Vmrun(reste, segment, a); return; }

        switch (cmd)
        {
            case "sh" or "bash" when reste.Count >= 2 && reste[0] == "-c":
                AnalyserLigne(reste[1], reste[1], de, a, profondeur + 1);
                return;
            case "bash" or "sh":
            {
                var script = reste.FirstOrDefault(r => r.EndsWith(".sh"));
                if (script is not null) Script(script, de, a);
                return;
            }
            case "ping":
                Paquet(a, de, reste.LastOrDefault(r => !r.StartsWith('-') && !Nombre().IsMatch(r)), Protocole.Icmp, null);
                return;
            case "nc":
            {
                // nc -zv -w 2 <cible> <port> : la valeur de -w n'est ni la cible ni le port
                var args = new List<string>();
                for (var i = 0; i < reste.Count; i++)
                {
                    if (reste[i] is "-w" or "-W" or "-s" or "-p") { i++; continue; }
                    if (!reste[i].StartsWith('-')) args.Add(reste[i]);
                }
                var port = args.Count >= 2 && Nombre().IsMatch(args[^1]) ? int.Parse(args[^1]) : (int?)null;
                Paquet(a, de, args.FirstOrDefault(), reste.Any(r => r.StartsWith('-') && r.Contains('u')) ? Protocole.Udp : Protocole.Tcp, port);
                return;
            }
            case "curl" or "wget":
            {
                var url = reste.FirstOrDefault(r => r.StartsWith("http://") || r.StartsWith("https://"));
                if (url is null) { Generique(a, de, segment); return; }
                var u = Url().Match(url);
                var https = url.StartsWith("https");
                var port = u.Groups[2].Success ? int.Parse(u.Groups[2].Value) : https ? 443 : 80;
                Paquet(a, de, u.Groups[1].Value, https ? Protocole.Https : Protocole.Http, port);
                return;
            }
            case "dig":
            {
                var serveur = reste.FirstOrDefault(r => r.StartsWith('@'))?.TrimStart('@');
                if (serveur is null) { Generique(a, de, segment); return; }
                Paquet(a, de, serveur, Protocole.Dns, 53);
                return;
            }
            case "nmap":
            {
                var cible = reste.LastOrDefault(r => !r.StartsWith('-') && (Cidr().IsMatch(r) || Laboratoire.Resoudre(r) is not null));
                if (cible is null) { Lecture(a, de, null, "version de nmap"); return; }
                var p = Option(reste, "-p");
                int? port = p is null ? null : int.Parse(PremierNombre().Match(p).Value);
                var proto = reste.Contains("-sn") ? Protocole.Icmp : Protocole.Tcp;
                if (cible is not null && Cidr().IsMatch(cible)) Paquet(a, de, Laboratoire.ResoudreReseau(cible)?.Valeur, proto, port, cible);
                else Paquet(a, de, cible, proto, port);
                return;
            }
            case "hping3":
            {
                var p = Option(reste, "-p");
                var cible = reste.LastOrDefault(r => Laboratoire.Resoudre(r) is not null);
                Paquet(a, de, cible, Protocole.Tcp, p is null ? null : int.Parse(p));
                return;
            }
            case "iperf3":
            {
                var cible = Option(reste, "-c");
                if (cible is null) { a.Effets.Add(new Service { Noeud = de, Nom = "iperf3", Demarre = true, Port = 5201 }); a.Reconnue = true; return; }
                var p = Option(reste, "-p");
                Paquet(a, de, cible, Protocole.Iperf3, p is null ? 5201 : int.Parse(p));
                return;
            }
            case "psql":
            {
                var h = Option(reste, "-h");
                if (h is null) { Lecture(a, de, null, "requête SQL locale"); return; }
                Paquet(a, de, h, Protocole.PostgreSql, 5432);
                return;
            }
            case "Test-NetConnection":
            {
                var cible = reste.FirstOrDefault(r => !r.StartsWith('-'));
                var p = Option(reste, "-Port");
                Paquet(a, de, cible, p is null ? Protocole.Icmp : Protocole.Tcp, p is null ? null : int.Parse(p));
                return;
            }
            case "ssh":
                Ssh(reste, de, a, profondeur);
                return;
            case "scp":
                Scp(reste, de, a);
                return;
            case "docker":
                Docker(reste, segment, texteComplet, de, a, profondeur);
                return;
            case "systemctl":
            {
                var verbe = reste.FirstOrDefault(r => !r.StartsWith('-')) ?? "";
                var services = reste.Where(r => !r.StartsWith('-') && r != verbe).ToList();
                if (verbe is "is-active" or "status" or "list-units") { Lecture(a, de, null, "état des services"); return; }
                foreach (var s in services.DefaultIfEmpty("service"))
                    a.Effets.Add(new Service { Noeud = de, Nom = s.Replace(".service", ""), Demarre = verbe is not ("stop" or "disable") });
                a.Reconnue = true;
                return;
            }
            case "service" when reste.Count >= 2:
                a.Effets.Add(new Service { Noeud = de, Nom = reste[0], Demarre = !reste[1].Contains("stop") });
                a.Reconnue = true;
                return;
            case "configctl" when reste.Count >= 2:
                if (reste[1] is "stop" or "start" or "restart" or "reload")
                    a.Effets.Add(new Service { Noeud = NoeudId.Opnsense, Nom = reste[0] == "ids" ? "suricata" : reste[0], Demarre = reste[1] != "stop" });
                else Lecture(a, de, null, $"configctl {reste[0]} {reste[1]}");
                a.Reconnue = true;
                return;
            case "pfctl":
                Pfctl(reste, a);
                return;
            case "nft":
                if (reste.FirstOrDefault() == "-f")
                    a.Effets.Add(new ChangementRegle { Action = ActionRegle.Ajouter, IdRegle = "p6_ssh", Resume = $"règles nftables chargées depuis {reste.ElementAtOrDefault(1)}" });
                else Lecture(a, de, null, "règles nftables");
                a.Reconnue = true;
                return;
            case "ip" when reste.Count >= 3 && reste[0] == "addr" && reste[1] == "add":
                a.Effets.Add(new AttribuerIp { Noeud = de, Ip = reste[2], Carte = Option(reste, "dev") ?? "" });
                a.Reconnue = true;
                return;
            case "vmrun":
                Vmrun(reste, segment, a);
                return;
            case "apt" or "apt-get":
            {
                var verbe = reste.FirstOrDefault(r => !r.StartsWith('-')) ?? "";
                var paquets = reste.Where(r => !r.StartsWith('-') && r != verbe).ToList();
                a.Effets.Add(new Surbrillance { Noeud = de, Texte = verbe == "install" ? $"Installe : {string.Join(", ", paquets)}" : "Met à jour la liste des paquets (carte NAT branchée le temps du téléchargement)." });
                a.Reconnue = true;
                return;
            }
            case "python3" or "python":
            {
                var prog = reste.FirstOrDefault(r => r.EndsWith(".py"));
                if (prog is not null && (prog.Contains("orchestrateur") || prog.Contains("c2.py") || prog.Contains("parse.py") || prog.Contains("correle.py")))
                    a.ScriptsADeplier.Add(prog);
                if (prog is null && reste.FirstOrDefault() == "-c")
                {
                    var url = UrlDansTexte().Match(segment);
                    if (url.Success && !url.Value.Contains("127.0.0.1")) { Paquet(a, de, Url().Match(url.Value).Groups[1].Value, Protocole.Http, 5000); return; }
                }
                Lecture(a, de, prog, prog is null ? "programme Python" : $"lance {prog}");
                return;
            }
            case "tcpdump":
                a.Effets.Add(new Surbrillance { Noeud = de, Texte = $"Capture des paquets sur {Option(reste, "-i") ?? Option(reste, "-nei") ?? "une carte"}" });
                a.Reconnue = true;
                return;
            case "cat" when segment.Contains('>') || texteComplet.Contains("<<"):
            case "tee":
            {
                var cible = Redirection().Match(segment) is { Success: true } r ? r.Groups[1].Value : reste.FirstOrDefault(x => !x.StartsWith('-') && !x.StartsWith("<<"));
                if (cible is null) { Lecture(a, de, null, "affiche un texte"); return; }
                a.Effets.Add(new Fichier { Noeud = de, Chemin = cible.Trim('\'', '"') });
                a.Reconnue = true;
                return;
            }
            case "mkdir" or "install" or "touch" or "nano" or "chmod" or "chown" or "cp" or "mv":
            {
                var cible = reste.LastOrDefault(r => !r.StartsWith('-') && (r.Contains('/') || r.Contains('.')));
                a.Effets.Add(new Fichier { Noeud = de, Chemin = cible ?? reste.LastOrDefault() ?? "" });
                a.Reconnue = true;
                return;
            }
            case "openssl":
                a.Effets.Add(new Fichier { Noeud = de, Chemin = Option(reste, "-out") ?? "certificat" });
                a.Reconnue = true;
                return;
            case "git":
                Lecture(a, de, null, $"Git : {string.Join(' ', reste.Take(2))}");
                return;
            case "cd" or "export" or "set" or "sleep" or "echo" or "printf" or "umask" or "history":
                if (Redirection().Match(segment) is { Success: true } re)
                    a.Effets.Add(new Fichier { Noeud = de, Chemin = re.Groups[1].Value });
                a.Reconnue = true;
                return;
            case "cat" or "tail" or "head" or "grep" or "ls" or "wc" or "column" or "jq" or "less" or "sort":
                Lecture(a, de, reste.LastOrDefault(r => r.Contains('/') && !r.StartsWith('-')), null);
                return;
            case "ifconfig" or "netstat" or "ss" or "ip" or "chronyc" or "pgrep" or "dmesg" or "ntpq" or "nmap" or "suricata"
                or "tshark" or "named-checkconf" or "named-checkzone" or "wsl" or "pkill" or "kill" or "add-apt-repository"
                or "usermod" or "nikto" or "find" or "test" or "sed" or "awk" or "nsenter":
                if (cmd == "nikto")
                {
                    Paquet(a, de, Option(reste, "-h") is { } h ? Url().Match(h).Groups[1].Value : null, Protocole.Https, 443);
                    return;
                }
                if (cmd == "nsenter")
                {
                    var interne = string.Join(' ', reste.SkipWhile(r => r.StartsWith('-') || Nombre().IsMatch(r) || r == "-t" || r == "-n"));
                    AnalyserLigne(interne, interne, de, a, profondeur + 1);
                    return;
                }
                Lecture(a, de, null, null);
                return;
        }

        if (cmd.EndsWith(".sh")) { Script(cmd, de, a); return; }
        if (cmd.StartsWith("Get-") || cmd is "Select-String" or "Select-Object" or "Where-Object")
        {
            Lecture(a, de, reste.FirstOrDefault(r => r.Contains('\\')), "réglage de Windows");
            return;
        }
        if (cmd.StartsWith("$vr") || cmd.StartsWith("$") && segment.Contains('='))
        {
            a.Reconnue = true;
            return;
        }
        Generique(a, de, segment);
    }

    private static void Ssh(List<string> reste, NoeudId de, Analyse a, int profondeur)
    {
        string? cible = null;
        var commande = new List<string>();
        for (var i = 0; i < reste.Count; i++)
        {
            var r = reste[i];
            if (cible is null)
            {
                if (r is "-i" or "-o" or "-p" or "-J") { i++; continue; }
                if (r == "-L" && i + 1 < reste.Count)
                {
                    // Tunnel : -L port_local:hôte:port → la machine qui reçoit le tunnel joint hôte:port
                    var morceaux = reste[++i].Split(':');
                    if (morceaux.Length == 3) a.Effets.Add(new Paquet { De = NoeudId.Hote, Vers = Laboratoire.Resoudre(morceaux[1]) is { } v && morceaux[1] != "127.0.0.1" ? v : NoeudId.Hote, AdresseVisee = morceaux[1], Proto = Protocole.Tcp, Port = int.Parse(morceaux[2]) });
                    continue;
                }
                if (r.StartsWith('-')) continue;
                cible = r;
                continue;
            }
            commande.Add(r);
        }
        var noeud = Laboratoire.Resoudre(cible);
        if (noeud is null) { Generique(a, de, "ssh " + string.Join(' ', reste)); return; }
        a.Effets.Add(new Paquet { De = de, Vers = noeud.Value, AdresseVisee = Hote(cible!), Proto = Protocole.Ssh, Port = 22 });
        a.Reconnue = true;
        if (commande.Count > 0)
        {
            var interne = string.Join(' ', commande);
            AnalyserLigne(interne, interne, noeud.Value, a, profondeur + 1);
        }
    }

    private static void Scp(List<string> reste, NoeudId de, Analyse a)
    {
        var args = new List<string>();
        for (var i = 0; i < reste.Count; i++)
        {
            if (reste[i] is "-o" or "-i" or "-P") { i++; continue; }
            if (reste[i].StartsWith('-')) continue;
            args.Add(reste[i]);
        }
        if (args.Count < 2) { Generique(a, de, "scp"); return; }
        var destination = args[^1];
        var distante = args.FirstOrDefault(x => x.Contains('@') && x.Contains(':'));
        if (distante is null) { Generique(a, de, "scp"); return; }
        var noeud = Laboratoire.Resoudre(distante.Split(':')[0]);
        if (noeud is null) { Generique(a, de, "scp"); return; }
        a.Effets.Add(new Paquet { De = de, Vers = noeud.Value, AdresseVisee = Hote(distante.Split(':')[0]), Proto = Protocole.Ssh, Port = 22 });
        if (destination == distante)
            a.Effets.Add(new Fichier { Noeud = noeud.Value, Chemin = distante.Split(':', 2)[1] is { Length: > 0 } c ? c : "~/" });
        else
            a.Effets.Add(new Fichier { Noeud = de, Chemin = destination });
        a.Reconnue = true;
    }

    private static void Docker(List<string> reste, string segment, string texteComplet, NoeudId de, Analyse a, int profondeur)
    {
        if (reste.Count == 0) { Generique(a, de, "docker"); return; }
        var sous = reste[0];
        if (sous == "compose")
        {
            var verbes = reste.Skip(1).ToList();
            for (var i = 0; i < verbes.Count; i++)
                if (verbes[i] is "--env-file" or "-p" or "-f") { verbes.RemoveAt(i); verbes.RemoveAt(i); i--; }
            var verbe = verbes.FirstOrDefault() ?? "";
            var etatB = segment.Contains("etat-B");
            if (verbe == "up")
            {
                var services = verbes.Skip(1).Where(v => !v.StartsWith('-')).ToList();
                var cibles = services.Count > 0 ? services : [.. Laboratoire.Conteneurs.Keys];
                if (etatB) a.Effets.Add(new ChangementEtat { Etat = "B" });
                foreach (var s in cibles)
                {
                    if (Laboratoire.Conteneurs.TryGetValue(s, out var c))
                        a.Effets.Add(new ConteneurReseau { Conteneur = c.Id, Ip = c.Ip, Reseau = etatB ? c.ReseauB : c.ReseauA });
                    else
                        a.Effets.Add(new Service { Noeud = de, Nom = s, Demarre = true });
                }
                a.Reconnue = true;
                return;
            }
            if (verbe == "down")
            {
                a.Effets.Add(new Service { Noeud = NoeudId.Docker, Nom = "conteneurs du projet", Demarre = false });
                a.Reconnue = true;
                return;
            }
            if (verbe == "exec" && verbes.Count >= 3)
            {
                // mini-laboratoire du rôle 3 (étape 0) : les conteneurs tournent sur le PC du membre
                var interne = string.Join(' ', verbes.Skip(2));
                AnalyserLigne(interne, interne, de, a, profondeur + 1);
                return;
            }
            Lecture(a, NoeudId.Docker, null, $"docker compose {verbe}");
            return;
        }
        if (sous == "exec")
        {
            var i = 1;
            while (i < reste.Count && reste[i].StartsWith('-')) i++;
            if (i >= reste.Count) { Generique(a, de, segment); return; }
            var conteneur = reste[i];
            var source = Laboratoire.Conteneurs.TryGetValue(conteneur, out var c) ? c.Id : de;
            var interne = string.Join(' ', reste.Skip(i + 1).Select(Requoter));
            AnalyserLigne(interne, interne, source, a, profondeur + 1);
            if (a.Effets.Count == 0) Lecture(a, source, null, $"commande dans le conteneur {conteneur}");
            a.Reconnue = true;
            return;
        }
        if (sous == "run")
        {
            var reseau = Option(reste, "--network");
            var ip = Option(reste, "--ip");
            if (reseau is not null && ip is not null)
                a.Effets.Add(new ConteneurReseau { Conteneur = Laboratoire.Resoudre(ip) ?? Laboratoire.ConteneurTest, Reseau = reseau, Ip = ip });
            // la commande lancée dans le conteneur suit l'image
            var idxImage = reste.FindIndex(1, r => !r.StartsWith('-') && r != reseau && r != ip);
            if (idxImage > 0 && idxImage + 1 < reste.Count)
            {
                var interne = string.Join(' ', reste.Skip(idxImage + 1).Select(Requoter));
                AnalyserLigne(interne, interne, ip is null ? de : Laboratoire.Resoudre(ip) ?? Laboratoire.ConteneurTest, a, profondeur + 1);
            }
            a.Reconnue = true;
            return;
        }
        if (sous == "network")
        {
            var verbe = reste.ElementAtOrDefault(1);
            var nom = reste.LastOrDefault();
            a.Effets.Add(new Surbrillance { Noeud = NoeudId.Docker, Texte = verbe switch { "create" => $"Réseau macvlan « {nom} » créé (parent {Option(reste, "parent=") ?? OptionO(reste)}).", "rm" => $"Réseaux supprimés : {string.Join(", ", reste.Skip(2))}.", _ => "Réseaux Docker." } });
            a.Reconnue = true;
            return;
        }
        if (sous == "cp")
        {
            var dest = reste.LastOrDefault() ?? "";
            var c = dest.Split(':')[0];
            a.Effets.Add(new Fichier { Noeud = Laboratoire.Conteneurs.TryGetValue(c, out var ct) ? ct.Id : de, Chemin = dest.Contains(':') ? dest.Split(':', 2)[1] : dest });
            a.Reconnue = true;
            return;
        }
        Lecture(a, NoeudId.Docker, null, $"docker {sous}");
    }

    private static string? OptionO(List<string> reste)
    {
        var o = Option(reste, "-o");
        return o?.Replace("parent=", "");
    }

    private static void Pfctl(List<string> reste, Analyse a)
    {
        var r = string.Join(' ', reste);
        if (r.StartsWith("-d")) a.Effets.Add(new ChangementRegle { Action = ActionRegle.Desactiver, IdRegle = "pf", Resume = "filtrage pf suspendu (dépannage)" });
        else if (r.StartsWith("-e")) a.Effets.Add(new ChangementRegle { Action = ActionRegle.Activer, IdRegle = "pf", Resume = "filtrage pf réactivé" });
        else if (r.StartsWith("-F states")) a.Effets.Add(new ChangementRegle { Action = ActionRegle.ViderEtats, IdRegle = "états", Resume = "toutes les connexions en cours sont oubliées" });
        else if (r.StartsWith("-k")) a.Effets.Add(new ChangementRegle { Action = ActionRegle.ViderEtats, IdRegle = "états", Resume = $"connexions de {reste.ElementAtOrDefault(1)} oubliées" });
        else a.Effets.Add(new Surbrillance { Noeud = NoeudId.Opnsense, Texte = "Règles pf affichées." });
        a.Reconnue = true;
    }

    private static void Vmrun(List<string> reste, string segment, Analyse a)
    {
        var vmx = VmVmx().Match(segment);
        var vm = vmx.Success && Laboratoire.Vm.TryGetValue(vmx.Groups[1].Value, out var v) ? v : NoeudId.Hote;
        var nomInstantane = reste.LastOrDefault() ?? "";
        if (segment.Contains("revertToSnapshot"))
        {
            a.Effets.Add(new Instantane { Vm = vm, Nom = nomInstantane, Restauration = true });
            a.Effets.Add(new EtatNoeud { Noeud = vm, Etat = EtatVm.Suspendue });
        }
        else if (segment.Contains(" snapshot "))
            a.Effets.Add(new Instantane { Vm = vm, Nom = nomInstantane });
        else
            a.Effets.Add(new Surbrillance { Noeud = vm, Texte = segment.Contains("listSnapshots") ? "Liste des instantanés." : "Commande vmrun." });
        a.Reconnue = true;
    }

    private static void Script(string script, NoeudId de, Analyse a)
    {
        a.ScriptsADeplier.Add(script);
        a.Effets.Add(new Surbrillance { Noeud = de, Fichier = script, Texte = $"Lance le script {Path.GetFileName(script)}." });
        a.Reconnue = true;
    }

    private static void Paquet(Analyse a, NoeudId de, string? cible, Protocole proto, int? port, string? adresseVisee = null)
    {
        var vers = cible is null ? null : cible is "127.0.0.1" or "localhost" ? de : Laboratoire.Resoudre(cible);
        if (vers is null) { Generique(a, de, cible ?? "cible inconnue"); return; }
        a.Effets.Add(new Paquet { De = de, Vers = vers.Value, AdresseVisee = adresseVisee ?? Hote(cible!), Proto = proto, Port = port });
        a.Reconnue = true;
    }

    private static void Lecture(Analyse a, NoeudId noeud, string? fichier, string? texte)
    {
        a.Effets.Add(new Surbrillance { Noeud = noeud, Fichier = fichier, Texte = texte });
        a.Reconnue = true;
    }

    private static void Generique(Analyse a, NoeudId de, string segment)
    {
        a.Effets.Add(new Surbrillance { Noeud = de, Texte = "Commande tapée sur cette machine." });
        a.Reconnue = false;
        _ = segment;
    }

    private static void AjouterResultatTest(string? sortie, Analyse a)
    {
        if (string.IsNullOrEmpty(sortie)) return;
        var m = Conformes().Match(sortie);
        if (m.Success)
        {
            a.Effets.Add(new ResultatTest { Nom = "Tests", Ok = m.Groups[1].Value == m.Groups[2].Value, Compteur = $"{m.Groups[1].Value}/{m.Groups[2].Value}" });
            return;
        }
        var e = ConformesEcarts().Match(sortie);
        if (e.Success)
            a.Effets.Add(new ResultatTest { Nom = "test_flux", Ok = e.Groups[2].Value == "0", Compteur = $"{e.Groups[1].Value} conformes, {e.Groups[2].Value} écart(s)" });
    }

    private static string? Option(List<string> reste, string nom)
    {
        for (var i = 0; i < reste.Count - 1; i++)
            if (reste[i] == nom) return reste[i + 1];
        var colle = reste.FirstOrDefault(r => r.StartsWith(nom) && r.Length > nom.Length && nom.StartsWith('-'));
        return colle?[nom.Length..];
    }

    private static string Hote(string cible)
    {
        var c = cible.Contains('@') ? cible[(cible.LastIndexOf('@') + 1)..] : cible;
        return c.Split(':')[0];
    }

    /// <summary>Remplace $nom (et pas $nomr…) par sa valeur dans le corps d'une boucle.</summary>
    private static string Variable(string corps, string nom, string valeur)
        => Regex.Replace(corps, @"\$\{?" + Regex.Escape(nom) + @"\}?(?![A-Za-z0-9_])", valeur.Replace("$", "$$"));

    private static string Requoter(string jeton) => jeton.Contains(' ') ? $"'{jeton}'" : jeton;

    [GeneratedRegex(@"^for\s+(\w+)\s+in\s+([^;]+);\s*do\s+(.+?);?\s*done")] private static partial Regex BoucleBash();
    [GeneratedRegex(@"^for\s+(\w+)\s+in\s+([^;]+);\s*do\s*$")] private static partial Regex BoucleBashDebut();
    [GeneratedRegex(@"foreach\s*\(\$(\w+)\s+in\s+([^)]+)\)\s*\{(.+?)\}", RegexOptions.Singleline)] private static partial Regex ForeachPowerShell();
    [GeneratedRegex(@"'([^']+)'")] private static partial Regex ValeursPs();
    [GeneratedRegex(@"^[A-Z_][A-Z0-9_]*=")] private static partial Regex Affectation();
    [GeneratedRegex(@"^\d+$")] private static partial Regex Nombre();
    [GeneratedRegex(@"\d+")] private static partial Regex PremierNombre();
    [GeneratedRegex(@"^https?://([^/:]+)(?::(\d+))?")] private static partial Regex Url();
    [GeneratedRegex(@"https?://[^\s'""]+")] private static partial Regex UrlDansTexte();
    [GeneratedRegex(@"^\d+\.\d+\.\d+\.\d+/\d+$")] private static partial Regex Cidr();
    [GeneratedRegex(@">\s*([^\s<>|&;]+)")] private static partial Regex Redirection();
    [GeneratedRegex(@"(p6-[a-z]+)\.vmx")] private static partial Regex VmVmx();
    [GeneratedRegex(@"(\d+)\s*/\s*(\d+)\s+conformes")] private static partial Regex Conformes();
    [GeneratedRegex(@"Conformes\s*:\s*(\d+)\s*·\s*Écarts\s*:\s*(\d+)")] private static partial Regex ConformesEcarts();
}
