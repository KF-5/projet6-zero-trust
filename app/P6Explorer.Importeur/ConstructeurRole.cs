using System.Text.Json;
using System.Text.RegularExpressions;
using P6Explorer.Domain.Amorcage;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Valeurs;

namespace P6Explorer.Importeur;

/// <summary>Ce qui reste à faire à la main, écrit dans docs/a-completer.md.</summary>
public sealed class Carnet
{
    public List<(string Code, string Texte)> EffetsGeneriques { get; } = [];
    public SortedDictionary<string, SortedSet<string>> ScriptsADeplier { get; } = new();
    public List<string> Remarques { get; } = [];
    public int CommandesReecrites { get; set; }
}

/// <summary>Transforme le JSON brut d'un rôle du guide (donnees/guide/role-N.json) en FichierRole.</summary>
public sealed partial class ConstructeurRole(string racine, Dictionary<string, List<Correction>> corrections, Carnet carnet, Depliage depliage)
{
    public FichierRole Construire(JsonElement brut)
    {
        var role = new Role
        {
            Numero = brut.GetProperty("numero").GetInt32(),
            Cle = Chaine(brut, "cle") ?? "",
            Titre = Chaine(brut, "titre") ?? "",
            CourtNom = Chaine(brut, "court") ?? "",
            Couleur = Chaine(brut, "couleur") ?? "",
            Mission = Texte.Nettoyer(Chaine(brut, "mission")) ?? "",
            Introduction = Texte.Nettoyer(Chaine(brut, "intro")),
            Demonstration = Demonstration(brut),
        };
        foreach (var e in brut.GetProperty("etapes").EnumerateArray())
            role.Etapes.Add(ConstruireEtape(role.Numero, e));

        var fichier = new FichierRole { Source = $"sources/guide/data (rôle {role.Cle})", Role = role };
        if (brut.TryGetProperty("questions", out var qs))
            foreach (var q in qs.EnumerateArray())
                fichier.QuestionsJury.Add(new QuestionJury
                {
                    Question = Texte.Nettoyer(q[0].GetString()) ?? "",
                    Reponse = Secrets.Masquer(Texte.Nettoyer(q[1].GetString())) ?? ""
                });
        return fichier;
    }

    private Etape ConstruireEtape(int numeroRole, JsonElement s)
    {
        var numero = s.GetProperty("numero").GetInt32();
        var fait = s.TryGetProperty("fait", out var f) && f.ValueKind == JsonValueKind.True;
        var etatTexte = f.ValueKind switch { JsonValueKind.True => "déjà fait", JsonValueKind.String => f.GetString(), _ => null };
        var etape = new Etape
        {
            Numero = numero,
            Code = $"r{numeroRole}-e{numero}",
            Titre = Texte.Nettoyer(Chaine(s, "titre")) ?? "",
            Quand = Chaine(s, "quand"),
            Fait = fait,
            EtatTexte = etatTexte,
            Statut = StatutValidation.Validee,
            MiseAJour = Secrets.Masquer(Texte.Nettoyer(Chaine(s, "maj"))),
            But = Texte.Nettoyer(Chaine(s, "but")) ?? "",
            Pourquoi = Texte.Nettoyer(Chaine(s, "pourquoi")) ?? "",
            Prerequis = Prerequis(s),
            Livrable = Secrets.Masquer(Texte.Nettoyer(Chaine(s, "livrer"))),
            Schema = Chaine(s, "schema"),
        };

        var etat = new EtatEtape(etape, numeroRole, fait, corrections.GetValueOrDefault(etape.Code) ?? []);

        if (s.TryGetProperty("ui", out var ui))
            foreach (var u in ui.EnumerateArray()) AjouterGui(etat, u);
        if (s.TryGetProperty("config", out var config))
            foreach (var c in config.EnumerateArray()) AjouterConfig(etat, c);
        if (s.TryGetProperty("tester", out var tester))
            foreach (var t in tester.EnumerateArray())
                AjouterBloc(etat, Chaine(t, "cmd") ?? "", Chaine(t, "t"), true, Chaine(t, "sortie"), Sens(t), null);

        if (s.TryGetProperty("pannes", out var pannes))
        {
            var i = 0;
            foreach (var p in pannes.EnumerateArray())
                etape.Pannes.Add(new Panne
                {
                    Ordre = ++i,
                    Symptome = Secrets.Masquer(Texte.Nettoyer(p[0].GetString())) ?? "",
                    Cause = Secrets.Masquer(Texte.Nettoyer(p[1].GetString())) ?? "",
                    Solution = Secrets.Masquer(Texte.Nettoyer(p[2].GetString())) ?? ""
                });
        }
        if (s.TryGetProperty("fini", out var fini))
        {
            var i = 0;
            foreach (var c in fini.EnumerateArray())
                etape.Criteres.Add(new CritereFin { Ordre = ++i, Texte = Texte.Nettoyer(c.GetString()) ?? "" });
        }
        if (numeroRole == 3 && numero == 0)
            carnet.Remarques.Add("r3-e0 (mini-laboratoire WSL sur le PC d'un membre) : rien ne circule dans le laboratoire ; effets ramenés à une mise en valeur du PC du membre (plan, décision 3). Le schéma du mini-laboratoire n'est pas dessiné : il reprend les adresses du laboratoire, que la carte principale montre déjà.");
        // Bascule A → B (rôle 2 étape 8 : règles F ; rôle 4 étape 8 : réseaux Docker B) : le schéma propose l'état B.
        if ((numeroRole, numero) is (2, 8) or (4, 8)
            && !etape.Commandes.SelectMany(c => c.Effets).OfType<ChangementEtat>().Any()
            && etape.Commandes.LastOrDefault(c => !c.EstTest && c.Shell != Shell.Document) is { } derniere)
        {
            derniere.Effets.Add(new ChangementEtat { Etat = "B", Ordre = derniere.Effets.Count + 1, Automatique = true });
        }
        return etape;
    }

    // ---------------------------------------------------------------- actions dans les interfaces

    private void AjouterGui(EtatEtape etat, JsonElement u)
    {
        var chemin = Texte.Nettoyer(Chaine(u, "path")) ?? "";
        var faire = Chaine(u, "faire") ?? "";
        var (ou, shell) = MachineGui(chemin, etat);
        var notes = new List<string>();
        string? texteGuide = null;
        var correction = etat.Fait ? etat.Corrections.FirstOrDefault(c => c.Bloc == chemin && c.Texte is not null) : null;
        var explication = Texte.Nettoyer(faire) ?? "";
        if (correction is not null)
        {
            texteGuide = explication;
            explication = correction.Texte!;
            if (correction.Note is not null) notes.Add(correction.Note);
            carnet.CommandesReecrites++;
        }
        if (u.TryGetProperty("verif", out var v))
            notes.Add(v.ValueKind == JsonValueKind.String ? $"Chemin de menu à vérifier sur ta version d'OPNsense ({v.GetString()})." : "Chemin de menu à vérifier sur ta version.");

        var commande = NouvelleCommande(etat, ou, shell, chemin, chemin, null);
        commande.TexteGuide = texteGuide is null ? null : Secrets.Masquer(texteGuide);
        commande.Explication = Secrets.Masquer(string.Join("\n", new[] { explication }.Concat(notes).Where(x => x.Length > 0))) ?? "";
        commande.Effets.AddRange(EffetsGui(chemin.ToLowerInvariant(), explication, ou));
        Numeroter(commande.Effets);
    }

    private static (NoeudId, Shell) MachineGui(string chemin, EtatEtape etat)
    {
        var p = chemin.ToLowerInvariant();
        if (p.StartsWith("console opnsense") || p.StartsWith("console de p6-opnsense")) return (NoeudId.Opnsense, Shell.OpnsenseShell);
        if (p.StartsWith("opnsense") || p.StartsWith("interfaces") || p.StartsWith("firewall") || p.StartsWith("system")
            || p.StartsWith("services") || p.StartsWith("assistant web") || p.StartsWith("…") || p.StartsWith("..."))
            return (NoeudId.Opnsense, Shell.OpnsenseGui);
        if (p.Contains("vmware") || p.Contains("paramètres d'une vm") || p.Contains("paramètres de la vm") || p.StartsWith("modifier")
            || p.Contains("carte nat") || p.Contains("installateur"))
            return (NoeudId.Hote, Shell.VmwareGui);
        if (p.Contains("tailscale") || p.Contains("broadcom") || p.Contains("panneau")) return (NoeudId.Hote, Shell.Navigateur);
        if (p.Contains("windows")) return (NoeudId.Hote, Shell.WindowsGui);
        if (p.Contains("wireshark") || p.Contains("mini-laboratoire")) return (NoeudId.PcMembre, Shell.Document);
        if (p.Contains("règle permanente") || p.StartsWith("jusqu")) return (NoeudId.Hote, Shell.Document);
        return etat.Role is 2 or 3 ? (NoeudId.Opnsense, Shell.OpnsenseGui) : (etat.Defaut.Ou, Shell.Document);
    }

    private static IEnumerable<Effet> EffetsGui(string p, string faire, NoeudId ou)
    {
        if (p.StartsWith("opnsense › ")) p = p["opnsense › ".Length..];
        if (p.Contains("installateur kali")) { yield return new EtatNoeud { Noeud = NoeudId.Kali, Etat = EtatVm.Allumee }; yield break; }
        if (p.Contains("installateur debian"))
        {
            yield return new EtatNoeud { Noeud = NoeudId.Docker, Etat = EtatVm.Allumee };
            yield return new EtatNoeud { Noeud = NoeudId.Collecteur, Etat = EtatVm.Allumee };
            yield break;
        }
        if (p.Contains("carte nat"))
        {
            foreach (var vm in new[] { "opnsense", "docker", "collecteur", "kali" })
                yield return new EtatLien { Lien = new LienId($"{vm}-nat"), Branche = false };
            yield break;
        }
        if (p.StartsWith("firewall") && p.Contains("nat"))
        {
            yield return new ChangementRegle { Action = ActionRegle.Ajouter, IdRegle = "Destination NAT", Resume = "redirections FA1 (80, 443 → 10.20.0.10), FA2 (53 → 10.20.0.40), FA6 désactivée" };
            yield break;
        }
        if (p.StartsWith("firewall") && p.Contains("rules"))
        {
            var codes = CodesRegle().Matches(faire).Select(m => m.Value).Distinct().ToList();
            yield return new ChangementRegle { Action = ActionRegle.Ajouter, IdRegle = codes.Count > 0 ? string.Join(", ", codes) : DernierMorceau(p), Resume = Texte.Raccourcir(faire.Split('\n')[0], 140) };
            yield break;
        }
        if (p.StartsWith("firewall")) { yield return new ChangementRegle { Action = ActionRegle.Modifier, IdRegle = DernierMorceau(p), Resume = Texte.Raccourcir(faire, 140) }; yield break; }
        if (p.Contains("logging")) { yield return new Journal { De = NoeudId.Opnsense, Ligne = "filterlog et suricata → 10.99.0.10, UDP 514" }; yield break; }
        if (p.Contains("network time")) { yield return new Service { Noeud = NoeudId.Opnsense, Nom = "ntp (serveur 10.99.0.10)", Demarre = true }; yield break; }
        if (p.Contains("intrusion detection")) { yield return new Service { Noeud = NoeudId.Opnsense, Nom = "suricata", Demarre = true }; yield break; }
        if ((p.StartsWith("interfaces") || p.Contains("set interface") || p.Contains("assign")) && Cidr().IsMatch(faire))
        {
            foreach (Match m in Cidr().Matches(faire))
                yield return new AttribuerIp { Noeud = NoeudId.Opnsense, Ip = m.Value, Carte = "" };
            yield break;
        }
        if (p.Contains("pare-feu windows")) { yield return new ChangementRegle { Action = ActionRegle.Ajouter, IdRegle = "P6 - bloquer le labo vers Windows", Resume = "entrées depuis 10.99.0.0/24 refusées" }; yield break; }
        yield return new Surbrillance { Noeud = ou, Texte = DernierMorceau(p) };
    }

    // ---------------------------------------------------------------- blocs de configuration

    private void AjouterConfig(EtatEtape etat, JsonElement c)
    {
        var titre = Chaine(c, "t");
        var fichier = Chaine(c, "file");
        var code = Chaine(c, "code") ?? "";
        var nomBloc = fichier ?? titre ?? "";
        string? noteVerif = c.TryGetProperty("verif", out var v)
            ? v.ValueKind == JsonValueKind.String ? $"À vérifier sur ta version ({v.GetString()})." : "À vérifier sur ta version."
            : null;

        var remplacements = etat.Fait
            ? etat.Corrections.Where(x => x.Bloc == nomBloc && (x.Commandes is not null || x.FichierReel is not null)).ToList()
            : [];
        if (remplacements.Count > 0)
        {
            var premier = true;
            foreach (var r in remplacements)
            {
                var guide = premier ? code : null;
                premier = false;
                if (r.FichierReel is not null)
                    AjouterFichier(etat, r.Chemin ?? r.FichierReel, LireFichierReel(r), guide, r.Note);
                else
                    for (var i = 0; i < r.Commandes!.Count; i++)
                        AjouterCommandeCorrigee(etat, r.Commandes[i], i == 0 ? guide : null, titre, r.Note,
                            i == r.Commandes.Count - 1 ? r.Sortie : null);
                carnet.CommandesReecrites++;
            }
        }
        else if (fichier is not null)
            AjouterFichier(etat, fichier, code, null, noteVerif);
        else
            AjouterBloc(etat, code, titre, false, null, [], noteVerif);

        var note = Chaine(c, "note");
        foreach (var enLigne in Texte.CodesEnLigne(note).Where(Laboratoire.RessembleACommande))
            AjouterBloc(etat, enLigne, titre, false, null, [], Texte.Nettoyer(note));
    }

    private void AjouterFichier(EtatEtape etat, string chemin, string contenu, string? texteGuide, string? note)
    {
        var ou = MachineFichier(chemin, etat);
        var commande = NouvelleCommande(etat, ou, Shell.Editeur, Secrets.Masquer(contenu.TrimEnd()) ?? "", chemin, null);
        commande.TexteGuide = Secrets.Masquer(texteGuide?.TrimEnd());
        var lignes = new List<string> { $"Contenu du fichier {chemin}." };
        if (note is not null) lignes.Add(note);
        commande.Explication = Secrets.Masquer(string.Join("\n", lignes)) ?? "";
        commande.Effets.Add(new Fichier { Noeud = ou, Chemin = NettoyerChemin(chemin), Cree = true, Automatique = true });
        Numeroter(commande.Effets);
    }

    private string LireFichierReel(Correction r)
    {
        var chemin = Path.Combine(racine, r.FichierReel!.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(chemin)) throw new FileNotFoundException($"Fichier réel introuvable : {r.FichierReel}", chemin);
        var nom = Path.GetFileName(chemin);
        if (nom is ".env" or "utilisateurs.json" or "secret.key")
            throw new InvalidOperationException($"Fichier secret refusé : {r.FichierReel}");
        var lignes = File.ReadAllLines(chemin);
        if (r.Lignes is [var debut, var fin]) lignes = lignes[(debut - 1)..Math.Min(fin, lignes.Length)];
        return string.Join("\n", lignes);
    }

    private static NoeudId MachineFichier(string chemin, EtatEtape etat)
    {
        var c = chemin.ToLowerInvariant();
        if (c.Contains("p6-docker") || c.StartsWith("services/") || c.StartsWith("sonde/") || c.StartsWith("capture/")
            || c.StartsWith("~/p6-etat-a") || c.Contains("test_flux") || c.StartsWith("/etc/p6-ssh"))
            return NoeudId.Docker;
        if (c.StartsWith("politique/") || c.StartsWith("campagne/") || c.StartsWith("analyse/") || c.Contains("rsyslog.d/10-")
            || c.Contains("p6-mgmt"))
            return NoeudId.Collecteur;
        if (c.Contains("chrony") && etat.Role == 6) return c.Contains("ajouts") ? NoeudId.Collecteur : NoeudId.Docker;
        return etat.Defaut.Ou;
    }

    private static string NettoyerChemin(string chemin)
    {
        var parenthese = chemin.IndexOf(" (", StringComparison.Ordinal);
        return parenthese > 0 ? chemin[..parenthese] : chemin;
    }

    // ---------------------------------------------------------------- blocs de commandes

    private void AjouterBloc(EtatEtape etat, string code, string? titre, bool estTest, string? sortie,
        List<(string Motif, string Sens)> sens, string? noteBloc)
    {
        var brutes = Decoupeur.Decouper(code);
        var commandes = brutes.Count(b => Laboratoire.RessembleACommande(b.Texte));
        var contexte = Laboratoire.Indice(titre) ?? etat.Defaut;

        if (brutes.Count == 0 || commandes * 2 < brutes.Count || code.Trim().StartsWith('('))
        {
            // Tableau, plan, pseudo-commande « (lecture du tableau) » : document de référence
            var doc = NouvelleCommande(etat, contexte.Ou, Shell.Document, Secrets.Masquer(code.Trim()) ?? "", titre, sortie);
            doc.EstTest = estTest;
            doc.Explication = Secrets.Masquer(string.Join("\n", new[] { titre is null ? null : $"{(estTest ? "Test" : "Document")} : {titre}.", noteBloc,
                "Texte de référence du guide : rien n'est exécuté." }.Concat(sens.Select(x => $"« {x.Motif} » : {x.Sens}")).Where(x => x is not null))) ?? "";
            var analyse = Analyseur.Analyser(doc.Texte, doc.Ou, Shell.Document, sortie);
            doc.Effets.AddRange(analyse.Effets);
            return;
        }

        for (var i = 0; i < brutes.Count; i++)
        {
            var b = brutes[i];
            if (Laboratoire.IndiceCommentaire(b.CommentaireAvant) is { } indice) contexte = indice;
            var (ou, shell) = Laboratoire.IndiceCommande(b.Texte) ?? contexte;

            // « ssh … karim@10.99.0.66 » se tape ailleurs que sur la machine visée
            var premier = Texte.Jetons(b.Texte).FirstOrDefault();
            if (premier is "ssh" or "scp")
            {
                var cible = Texte.Jetons(b.Texte).Skip(1).FirstOrDefault(x => x.Contains('@'));
                if (cible is not null && Laboratoire.Resoudre(cible.Split(':')[0]) == ou)
                    ou = etat.Defaut.Ou == ou ? NoeudId.Hote : etat.Defaut.Ou;
                if (ou == NoeudId.Hote) shell = Shell.PowerShell;
            }

            var dernier = i == brutes.Count - 1;
            var texteGuide = b.Texte;
            var notes = new List<string>();
            var correction = etat.Fait && estTest
                ? etat.Corrections.FirstOrDefault(c => c.Contient is not null && b.Texte.Contains(c.Contient))
                : null;
            var texte = b.Texte;
            var sortieCommande = dernier ? sortie : null;
            if (correction is not null)
            {
                if (correction.Texte is not null) texte = correction.Texte;
                if (correction.Sortie is not null) sortieCommande = correction.Sortie;
                if (correction.Note is not null) notes.Add(correction.Note);
                carnet.CommandesReecrites++;
            }

            var reecriture = Corrections.Appliquer(texte, ou, etat.Fait);
            texte = reecriture.Texte;
            ou = reecriture.Ou ?? ou;
            shell = reecriture.Shell ?? shell;
            notes.AddRange(reecriture.Notes);
            if (etat.Fait && texte != texteGuide) carnet.CommandesReecrites++;
            if (etat.Fait && sortieCommande is not null) sortieCommande = Corrections.Appliquer(sortieCommande, ou, true).Texte;

            var commande = NouvelleCommande(etat, ou, shell, texte, titre, sortieCommande);
            commande.EstTest = estTest;
            commande.TexteGuide = texte != texteGuide ? Secrets.Masquer(texteGuide) : null;

            var lignes = new List<string?>();
            var commentaire = string.Join(" ", new[] { b.CommentaireAvant?.Replace('\n', ' '), b.CommentaireFin }.Where(x => !string.IsNullOrWhiteSpace(x)));
            lignes.Add(commentaire.Length > 0 ? Majuscule(commentaire) : Explications.PourCommande(texte));
            if (estTest && titre is not null && i == 0) lignes.Add($"Test : {titre}.");
            lignes.Add(noteBloc);
            lignes.AddRange(notes);
            if (dernier) lignes.AddRange(sens.Select(x => $"Dans la sortie, « {x.Motif} » : {x.Sens}"));
            commande.Explication = Secrets.Masquer(string.Join("\n", lignes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())) ?? "";

            AjouterEffets(etat, commande);
        }
    }

    private void AjouterCommandeCorrigee(EtatEtape etat, string texte, string? texteGuide, string? titre, string? note, string? sortie)
    {
        var (ou, shell) = Laboratoire.Indice(titre) ?? etat.Defaut;
        var commande = NouvelleCommande(etat, ou, shell, texte, titre, sortie);
        commande.TexteGuide = Secrets.Masquer(texteGuide?.TrimEnd());
        commande.Explication = Secrets.Masquer(string.Join("\n", new[] { Explications.PourCommande(texte), note }.Where(x => x is not null))) ?? "";
        AjouterEffets(etat, commande);
    }

    private void AjouterEffets(EtatEtape etat, Commande commande)
    {
        var analyse = Analyseur.Analyser(commande.Texte, commande.Ou, commande.Shell, commande.Sortie);
        if (etat.Role == 3 && etat.Etape.Numero == 0)
        {
            // Mini-laboratoire sur le PC du membre : les adresses imitent le laboratoire, mais rien n'y circule.
            var resume = string.Join(" · ", analyse.Effets.Select(e => e.Resumer()));
            analyse.Effets.Clear();
            analyse.Effets.Add(new Surbrillance { Noeud = NoeudId.PcMembre, Texte = $"Mini-laboratoire (WSL) : {resume}", Ordre = 1, Automatique = true });
        }
        commande.Effets.AddRange(analyse.Effets);
        if (!analyse.Reconnue) carnet.EffetsGeneriques.Add((commande.Code, commande.Texte.Split('\n')[0]));
        foreach (var s in analyse.ScriptsADeplier)
        {
            if (depliage.Deplier(s, commande, etat.Role, etat.Etape.Numero)) continue;   // sous-commandes ajoutées
            if (!carnet.ScriptsADeplier.TryGetValue(s, out var codes)) carnet.ScriptsADeplier[s] = codes = [];
            codes.Add(commande.Code);
        }
    }

    // ---------------------------------------------------------------- outils

    private static Commande NouvelleCommande(EtatEtape etat, NoeudId ou, Shell shell, string texte, string? bloc, string? sortie)
    {
        var commande = new Commande
        {
            Ordre = etat.Etape.Commandes.Count + 1,
            Code = $"{etat.Etape.Code}-c{etat.Etape.Commandes.Count + 1:00}",
            Ou = ou,
            Shell = shell,
            Compte = ou == NoeudId.Opnsense ? "root" : Laboratoire.EstVmLinux(ou) ? "karim" : null,
            Bloc = bloc is null ? null : Texte.Nettoyer(bloc),
            Texte = Secrets.Masquer(texte) ?? "",
            Sortie = Secrets.Masquer(sortie?.TrimEnd()),
        };
        etat.Etape.Commandes.Add(commande);
        return commande;
    }

    private static void Numeroter(List<Effet> effets)
    {
        for (var i = 0; i < effets.Count; i++) { effets[i].Ordre = i + 1; effets[i].Automatique = true; }
    }

    private static string? Chaine(JsonElement e, string nom)
        => e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static List<(string, string)> Sens(JsonElement t)
    {
        var liste = new List<(string, string)>();
        if (t.TryGetProperty("sens", out var s))
            foreach (var x in s.EnumerateArray())
                liste.Add((x[0].GetString() ?? "", Texte.Nettoyer(x[1].GetString()) ?? ""));
        return liste;
    }

    private static string? Prerequis(JsonElement s)
    {
        if (!s.TryGetProperty("prereq", out var p)) return null;
        var morceaux = new List<string>();
        if (p.TryGetProperty("etapes", out var e)) morceaux.Add("Étapes : " + string.Join(", ", e.EnumerateArray().Select(x => x.GetString())));
        if (p.TryGetProperty("modules", out var m)) morceaux.Add("Modules du cours : " + string.Join(", ", m.EnumerateArray().Select(x => x.GetRawText())));
        if (Texte.Nettoyer(Chaine(p, "texte")) is { } t) morceaux.Add(t);
        return morceaux.Count == 0 ? null : string.Join("\n", morceaux);
    }

    private static string? Demonstration(JsonElement brut)
    {
        if (!brut.TryGetProperty("demo", out var d) || d.ValueKind != JsonValueKind.Object) return null;
        var lignes = new List<string?> { Texte.Nettoyer(Chaine(d, "intro")) };
        if (d.TryGetProperty("etapes", out var e))
            lignes.AddRange(e.EnumerateArray().Select((x, i) => $"{i + 1}. {Texte.Nettoyer(x.GetString())}"));
        if (Texte.Nettoyer(Chaine(d, "secours")) is { } s) lignes.Add($"Plan de secours : {s}");
        return string.Join("\n", lignes.Where(x => x is not null));
    }

    private static string DernierMorceau(string chemin) => chemin.Split('›').Last().Trim();

    private static string Majuscule(string t) => t.Length == 0 ? t : char.ToUpperInvariant(t[0]) + t[1..];

    [GeneratedRegex(@"\b(FA\d|F0\d|F99)\b")] private static partial Regex CodesRegle();
    [GeneratedRegex(@"\b\d+\.\d+\.\d+\.\d+/\d+\b")] private static partial Regex Cidr();

    private sealed class EtatEtape(Etape etape, int role, bool fait, List<Correction> corrections)
    {
        public Etape Etape { get; } = etape;
        public int Role { get; } = role;
        public bool Fait { get; } = fait;
        public List<Correction> Corrections { get; } = corrections;
        public (NoeudId Ou, Shell Shell) Defaut { get; } = Laboratoire.ParDefaut(role, etape.Numero);
    }
}
