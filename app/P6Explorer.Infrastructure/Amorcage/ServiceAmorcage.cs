using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using P6Explorer.Application.Services;
using System.Text.RegularExpressions;
using P6Explorer.Domain.Amorcage;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Enumerations;
using P6Explorer.Domain.Suivi;
using P6Explorer.Domain.Exceptions;
using P6Explorer.Domain.Topologie;
using P6Explorer.Infrastructure.Identite;
using P6Explorer.Infrastructure.Persistance;

namespace P6Explorer.Infrastructure.Amorcage;

/// <summary>Réglages de l'amorçage (section « Amorcage » de appsettings.json et des user-secrets).</summary>
public class OptionsAmorcage
{
    public const string Section = "Amorcage";
    /// <summary>Dossier « donnees » du dépôt ; vide : cherché en remontant depuis le dossier de l'application.</summary>
    public string? DossierDonnees { get; set; }
    public string AdminEmail { get; set; } = "admin@p6.lab";
    /// <summary>Jamais dans le code ni dans appsettings.json : dotnet user-secrets set "Amorcage:MotDePasseAdmin" "…".</summary>
    public string? MotDePasseAdmin { get; set; }
    /// <summary>Faux pour les tests : la base est créée par EnsureCreated au lieu des migrations.</summary>
    public bool AppliquerMigrations { get; set; } = true;
    /// <summary>Seuil de réussite des quiz (part de bonnes réponses) : 0,8 par défaut, configurable.</summary>
    public double SeuilQuiz { get; set; } = 0.8;
}

/// <summary>
/// Au démarrage : crée ou met à jour la base (migrations), puis la remplit une seule fois depuis
/// donnees/topologie/*.json et donnees/commandes/role-N.json. Ensuite, la base est la source de vérité :
/// les fichiers JSON ne sont plus relus.
/// </summary>
public class ServiceAmorcage(
    P6DbContext contexte,
    RoleManager<IdentityRole> roles,
    UserManager<ApplicationUser> utilisateurs,
    IOptions<OptionsAmorcage> options,
    ILogger<ServiceAmorcage> journal) : IServiceAmorcage
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task AmorcerAsync(string dossierApplication, CancellationToken annulation = default)
    {
        if (options.Value.AppliquerMigrations && contexte.Database.IsRelational())
            await contexte.Database.MigrateAsync(annulation);
        else
            await contexte.Database.EnsureCreatedAsync(annulation);

        var dossier = TrouverDossierDonnees(dossierApplication);
        contexte.JournaliserRevisions = false;    // le chargement initial n'est pas une modification de l'équipe
        try
        {
            await AmorcerTopologieAsync(dossier, annulation);
            await AmorcerContenuAsync(dossier, annulation);
        }
        finally { contexte.JournaliserRevisions = true; }
        await AmorcerComptesAsync();
    }

    private async Task AmorcerTopologieAsync(string dossier, CancellationToken annulation)
    {
        if (await contexte.EtatsReseau.AnyAsync(annulation)) return;
        var chemins = Directory.GetFiles(Path.Combine(dossier, "topologie"), "etat-*.json").Order().ToList();
        var fichiers = chemins.Select(c => (Chemin: c, Fichier: Lire<FichierTopologie>(c))).ToList();
        var reseaux = new Dictionary<string, Reseau>();
        foreach (var (chemin, f) in fichiers)
        {
            contexte.EtatsReseau.Add(new EtatReseau { Code = f.Etat, Libelle = f.Libelle, Description = f.Description });
            foreach (var r in f.Reseaux)
                if (!reseaux.TryAdd(r.Code, r))
                    throw new DonneesAmorcageInvalidesException(chemin, $"réseau « {r.Code} » déjà décrit dans un autre fichier.");
        }
        // Une machine est décrite une fois ; les fichiers suivants n'ajoutent que ses cartes propres à leur état.
        foreach (var n in FichierTopologie.FusionnerNoeuds(fichiers.Select(x => x.Fichier)))
        {
            if (string.IsNullOrWhiteSpace(n.Nom))
                throw new DonneesAmorcageInvalidesException(chemins[0], $"machine « {n.Id} » sans description complète (nom manquant).");
            foreach (var i in n.Interfaces.Where(i => i.ReseauCode is not null))
                i.Reseau = reseaux.TryGetValue(i.ReseauCode!, out var r) ? r
                    : throw new DonneesAmorcageInvalidesException(chemins[0], $"réseau « {i.ReseauCode} » inconnu (interface {n.Id} {i.Nom}).");
            contexte.Noeuds.Add(n);
        }
        contexte.Reseaux.AddRange(reseaux.Values);
        var liensVus = new HashSet<string>();
        foreach (var (_, f) in fichiers)
        {
            contexte.Liens.AddRange(f.Liens.Where(l => liensVus.Add(l.Id.Valeur)));
            contexte.Regles.AddRange(f.Regles);
            contexte.ReglesNat.AddRange(f.ReglesNat);
        }
        await contexte.SaveChangesAsync(annulation);
        journal.LogInformation("Topologie amorcée : {Nombre} état(s) du laboratoire.", fichiers.Count);
    }

    private async Task AmorcerContenuAsync(string dossier, CancellationToken annulation)
    {
        if (await contexte.RolesProjet.AnyAsync(annulation)) return;
        var fichiers = Directory.GetFiles(Path.Combine(dossier, "commandes"), "role-*.json").Order().ToList();
        if (fichiers.Count == 0)
            throw new DonneesAmorcageInvalidesException(dossier, "aucun fichier commandes/role-N.json (lancer P6Explorer.Importeur).");
        var lus = new List<FichierRole>();
        foreach (var chemin in fichiers)
        {
            var fichier = Lire<FichierRole>(chemin);
            lus.Add(fichier);
            var role = fichier.Role;
            // Les sous-commandes (scripts dépliés) appartiennent aussi à l'étape de leur commande parente.
            foreach (var e in role.Etapes)
                foreach (var c in e.Commandes)
                    foreach (var s in c.SousCommandes) s.Etape = e;
            contexte.RolesProjet.Add(role);
        }
        var n = await contexte.SaveChangesAsync(annulation);
        journal.LogInformation("Contenu du guide amorcé : {Fichiers} rôles, {Lignes} lignes écrites.", fichiers.Count, n);
        await AmorcerQuizAsync(lus, annulation);
        await AmorcerGlossaireAsync(dossier, annulation);
    }

    /// <summary>Un quiz par étape (relation 1-1), questions « à valider » tirées du guide par l'importeur.</summary>
    private async Task AmorcerQuizAsync(IEnumerable<FichierRole> fichiers, CancellationToken annulation)
    {
        var etapes = await contexte.Etapes.ToDictionaryAsync(e => e.Code, annulation);
        var seuil = Math.Clamp(options.Value.SeuilQuiz, 0.1, 1);
        foreach (var q in fichiers.SelectMany(f => f.Quiz).Where(q => etapes.ContainsKey(q.Etape)))
        {
            var quiz = new Quiz { EtapeId = etapes[q.Etape].Id, Seuil = seuil };
            var ordre = 0;
            foreach (var question in q.Questions.Where(x => x.Choix.Count >= 2))
            {
                var qu = new Question { Ordre = ++ordre, Texte = question.Texte, Source = question.Source, Statut = StatutValidation.AValider };
                var o = 0;
                foreach (var c in question.Choix)
                    qu.Choix.Add(new Choix { Ordre = ++o, Texte = c.Texte, Correct = c.Correct, Explication = c.Explication });
                quiz.Questions.Add(qu);
            }
            contexte.Quiz.Add(quiz);
        }
        await contexte.SaveChangesAsync(annulation);
    }

    /// <summary>Glossaire (donnees/glossaire.json), relié n-n aux étapes dont le texte emploie le terme.</summary>
    private async Task AmorcerGlossaireAsync(string dossier, CancellationToken annulation)
    {
        var chemin = Path.Combine(dossier, "glossaire.json");
        if (!File.Exists(chemin) || await contexte.Glossaire.AnyAsync(annulation)) return;
        var termes = Lire<List<TermeAmorce>>(chemin);
        var etapes = await contexte.Etapes.Include(e => e.Commandes).ToListAsync(annulation);
        var textes = etapes.ToDictionary(e => e, e => string.Join(" ", new[] { e.Titre, e.But, e.Pourquoi, e.MiseAJour, e.Livrable }
            .Concat(e.Commandes.Select(c => c.Explication))));
        foreach (var t in termes)
        {
            var motif = new Regex($@"(?<![\p{{L}}\d_-]){Regex.Escape(t.Terme)}(?![\p{{L}}\d_-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var terme = new TermeGlossaire { Terme = t.Terme, Definition = t.Definition, Comparaison = t.Comparaison };
            terme.Etapes.AddRange(etapes.Where(e => motif.IsMatch(textes[e])));
            contexte.Glossaire.Add(terme);
        }
        await contexte.SaveChangesAsync(annulation);
        journal.LogInformation("Glossaire amorcé : {Nombre} termes.", termes.Count);
    }

    private async Task AmorcerComptesAsync()
    {
        foreach (var nom in new[] { RolesIdentite.Admin, RolesIdentite.Membre })
            if (!await roles.RoleExistsAsync(nom))
                await roles.CreateAsync(new IdentityRole(nom));

        var o = options.Value;
        if (await utilisateurs.FindByEmailAsync(o.AdminEmail) is not null) return;
        if (string.IsNullOrWhiteSpace(o.MotDePasseAdmin))
        {
            journal.LogWarning("Compte admin non créé : définir son mot de passe avec  dotnet user-secrets set \"Amorcage:MotDePasseAdmin\" \"…\"  (voir app/README.md).");
            return;
        }
        var admin = new ApplicationUser { UserName = o.AdminEmail, Email = o.AdminEmail, EmailConfirmed = true, NumeroRole = 1, NomAffiche = "Rôle 1 (admin)" };
        var resultat = await utilisateurs.CreateAsync(admin, o.MotDePasseAdmin);
        if (!resultat.Succeeded)
        {
            journal.LogError("Compte admin non créé : {Erreurs}", string.Join(" ; ", resultat.Errors.Select(e => e.Description)));
            return;
        }
        await utilisateurs.AddToRoleAsync(admin, RolesIdentite.Admin);
        journal.LogInformation("Compte admin {Email} créé.", o.AdminEmail);
    }

    private string TrouverDossierDonnees(string depart)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.DossierDonnees))
            return Path.GetFullPath(options.Value.DossierDonnees, depart);
        for (var d = new DirectoryInfo(depart); d is not null; d = d.Parent)
        {
            var candidat = Path.Combine(d.FullName, "donnees");
            if (Directory.Exists(Path.Combine(candidat, "commandes"))) return candidat;
        }
        throw new DonneesAmorcageInvalidesException(depart, "dossier « donnees/commandes » introuvable en remontant depuis l'application.");
    }

    private static T Lire<T>(string chemin)
    {
        using var flux = File.OpenRead(chemin);
        return JsonSerializer.Deserialize<T>(flux, Json)
               ?? throw new DonneesAmorcageInvalidesException(chemin, "fichier vide.");
    }
}
