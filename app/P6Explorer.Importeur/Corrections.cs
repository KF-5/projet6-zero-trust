using System.Text.Json;
using System.Text.RegularExpressions;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Importeur;

/// <summary>Une entrée de donnees/corrections/etapes-faites.json.</summary>
public sealed class Correction
{
    public string? Bloc { get; set; }
    public string? Contient { get; set; }
    public List<string>? Commandes { get; set; }
    public string? FichierReel { get; set; }
    public int[]? Lignes { get; set; }
    public string? Chemin { get; set; }
    public string? Texte { get; set; }
    public string? Sortie { get; set; }
    public string? Note { get; set; }
}

/// <summary>Résultat d'une correction générique sur une ligne de commande.</summary>
public sealed record Reecriture(string Texte, NoeudId? Ou, Shell? Shell, List<string> Notes);

/// <summary>
/// Plateforme réelle contre texte du guide (décision du 20 nov., Q4) :
/// étapes « fait » → commande réellement utilisée ; autres étapes → texte du guide, écart signalé.
/// </summary>
public static partial class Corrections
{
    public const string NoteRoot = "Sur la plateforme réelle, pas de connexion root : compte karim avec sudo (écart E40).";
    public const string NoteSshOpnsense = "Le SSH d'OPNsense est désactivé pour l'instant ; il sera réactivé sur MGMT, par clé seulement, pour l'orchestrateur (question Q13).";
    public const string NoteConsole = "SSH d'OPNsense désactivé : la commande est tapée dans la console d'OPNsense (VMware ou VNC 5901, option 8 › Shell).";
    public const string NoteJournaux = "Les journaux arrivent dans /var/log/p6/<adresse>.log sur le collecteur, pas dans /var/log/remote/ (écart E42).";
    public const string NoteReseaux = "Réseaux Docker réels : p6_lan, p6_dmz, p6_wan, créés par le projet Compose « p6 » (écart E38).";
    public const string NoteImage = "Image réelle des postes : p6/outils:1 (docker-compose.yml du projet p6).";
    public const string NoteZone = "Zone DNS réelle : p6.lab (labo.test est un reste de la conception).";
    public const string NoteDossier = "Les fichiers de l'état A sont dans ~/p6-etat-a sur p6-docker (copie de D:\\Labo-P6\\docker\\p6-etat-a).";
    public const string NoteStratum = "Le collecteur réel utilise « local stratum 8 » (setup-collecteur.sh).";
    public const string NoteDocker = "Docker réel : 26.1.5 et Compose 2.26.1, paquets Debian (écart E38).";
    public const string NoteSudo = "Commande lancée avec sudo : sur les VM, on travaille avec le compte karim (écart E40).";
    public const string NoteNginx = "Image nginx réelle : nginx:1.27-alpine.";

    public static Dictionary<string, List<Correction>> Charger(string chemin)
    {
        using var flux = File.OpenRead(chemin);
        using var doc = JsonDocument.Parse(flux);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var resultat = new Dictionary<string, List<Correction>>();
        foreach (var e in doc.RootElement.GetProperty("etapes").EnumerateObject())
            resultat[e.Name] = e.Value.Deserialize<List<Correction>>(options) ?? [];
        return resultat;
    }

    /// <summary>Applique les règles génériques (étape faite) ou repère les écarts (étape à venir).</summary>
    public static Reecriture Appliquer(string texte, NoeudId ou, bool etapeFaite)
    {
        var notes = new List<string>();
        var t = texte;
        NoeudId? nouvelOu = null;
        Shell? nouveauShell = null;

        var console = SshRootOpnsense().Match(t);
        if (console.Success)
        {
            if (etapeFaite)
            {
                t = console.Groups[1].Value;
                nouvelOu = NoeudId.Opnsense;
                nouveauShell = Shell.OpnsenseShell;
                notes.Add(NoteConsole);
            }
            else
            {
                notes.Add(NoteRoot);
                notes.Add(NoteSshOpnsense);
            }
        }
        else if (t.Contains("root@"))
        {
            notes.Add(NoteRoot);
            if (t.Contains("root@10.99.0.1:") || t.Contains("root@10.99.0.1 ")) notes.Add(NoteSshOpnsense);
            if (etapeFaite) t = t.Replace("root@", "karim@");
        }

        Remplacer(ref t, "/var/log/remote/opnsense.log", "/var/log/p6/10.99.0.1.log", NoteJournaux, etapeFaite, notes);
        Remplacer(ref t, "/var/log/remote/docker.log", "/var/log/p6/10.99.0.20.log", NoteJournaux, etapeFaite, notes);
        Remplacer(ref t, "/var/log/remote/", "/var/log/p6/", NoteJournaux, etapeFaite, notes);
        if (ReseauGuide().IsMatch(t))
        {
            notes.Add(NoteReseaux);
            if (etapeFaite) t = ReseauGuide().Replace(t, m => m.Groups[1].Value + "p6_" + m.Groups[2].Value);
        }
        Remplacer(ref t, "projet6/outils", "p6/outils:1", NoteImage, etapeFaite, notes);
        Remplacer(ref t, "labo.test", "p6.lab", NoteZone, etapeFaite, notes);
        Remplacer(ref t, "nginx:stable", "nginx:1.27-alpine", NoteNginx, etapeFaite, notes);
        Remplacer(ref t, "docker compose --env-file etat-A.env ", "docker compose ", NoteDossier, etapeFaite, notes);
        Remplacer(ref t, "/opt/projet6/services", "~/p6-etat-a", NoteDossier, etapeFaite, notes);
        Remplacer(ref t, "local stratum 10", "local stratum 8", NoteStratum, etapeFaite, notes);
        if (t.Contains("docker-ce")) notes.Add(NoteDocker);

        if (etapeFaite && Laboratoire.EstVmLinux(nouvelOu ?? ou))
        {
            var avecSudo = AjouterSudo(t);
            if (avecSudo != t) { t = avecSudo; notes.Add(NoteSudo); }
        }
        return new Reecriture(t, nouvelOu, nouveauShell, notes.Distinct().ToList());
    }

    private static void Remplacer(ref string t, string ancien, string nouveau, string note, bool faite, List<string> notes)
    {
        if (!t.Contains(ancien)) return;
        notes.Add(note);
        if (faite) t = t.Replace(ancien, nouveau);
    }

    /// <summary>Préfixe sudo aux commandes d'administration tapées sur une VM avec le compte karim.</summary>
    public static string AjouterSudo(string ligne)
    {
        if (ligne.Contains('\n')) return ligne;
        var morceaux = Texte.DecouperHorsGuillemets(ligne, "&&");
        if (morceaux.Count == 0) return ligne;
        var modifie = false;
        for (var i = 0; i < morceaux.Count; i++)
        {
            var m = morceaux[i];
            if (m.StartsWith("sudo ") || !BesoinRoot().IsMatch(m)) continue;
            morceaux[i] = "sudo " + m;
            modifie = true;
        }
        return modifie ? string.Join(" && ", morceaux) : ligne;
    }

    // ssh root@10.99.0.1 'commande' (ou "commande") : SSH d'OPNsense désactivé.
    [GeneratedRegex(@"^ssh\s+(?:-\S+\s+)*root@10\.99\.0\.1\s+['""](.+)['""]\s*$")]
    private static partial Regex SshRootOpnsense();
    [GeneratedRegex(@"(--network\s+)(lan|dmz|wan)\b")]
    private static partial Regex ReseauGuide();
    [GeneratedRegex(@"^(apt|apt-get|nft|tcpdump|nsenter|usermod|iptables|add-apt-repository|install\s|systemctl\s+(restart|enable|start|stop|disable|reload)|ip\s+(addr|link|route)\s+(add|del|set)|mkdir\s+-p\s+/(etc|opt|srv|var)|tee\s+/etc|chmod\s+\S+\s+/etc|chown\s|cp\s+.*\s/etc/)")]
    private static partial Regex BesoinRoot();
}
