using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using P6Explorer.Application.Dto;
using P6Explorer.Application.Services;
using P6Explorer.Application.ViewModels;
using P6Explorer.Domain.Exceptions;
using P6Explorer.Infrastructure.Identite;
using P6Explorer.Web.Filtres;
using P6Explorer.Web.Models;
using P6Explorer.Web.Securite;

namespace P6Explorer.Web.Controllers;

/// <summary>Auto-évaluation : un quiz par étape ; le score est enregistré pour les membres connectés.</summary>
public class QuizController(IQuizService quiz, IAuthorizationService autorisation) : Controller
{
    [AllowAnonymous]
    [HttpGet("/Quiz/{role:int}/{etape:int}")]
    public async Task<IActionResult> Index(int role, int etape)
    {
        var vm = await quiz.ChargerAsync(role, etape, User.Identifiant());
        if (vm is null) return NotFound();
        vm.PeutModifier = (await autorisation.AuthorizeAsync(User, role, Politiques.ModifierSonRole)).Succeeded;
        ViewData["Title"] = $"Quiz · {vm.EtapeTitre}";
        return View(vm);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Corriger(int quizId, IFormCollection formulaire)
    {
        // Champs « q-<question> » : identifiants des choix cochés (cases ou boutons radio).
        var reponses = formulaire.Keys.Where(k => k.StartsWith("q-"))
            .ToDictionary(k => int.Parse(k[2..]), k => formulaire[k].Select(v => int.TryParse(v, out var x) ? x : 0).ToList());
        var vm = await quiz.CorrigerAsync(quizId, reponses, User.Identifiant()!);
        ViewData["Title"] = "Résultat du quiz";
        return View("Resultat", vm);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Question(int id)
    {
        var f = await quiz.QuestionAsync(id);
        if (f is null) return NotFound();
        if (!(await autorisation.AuthorizeAsync(User, f.RoleNumero, Politiques.ModifierSonRole)).Succeeded) return Forbid();
        ViewData["Title"] = "Relire une question";
        return View(f);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken, ValiderModele]
    public async Task<IActionResult> Question(QuestionFormVM formulaire)
    {
        if (await quiz.RoleDeQuestionAsync(formulaire.Id) is not int role
            || !(await autorisation.AuthorizeAsync(User, role, Politiques.ModifierSonRole)).Succeeded) return Forbid();
        try { await quiz.EnregistrerQuestionAsync(formulaire); }
        catch (ExceptionMetier ex)
        {
            ViewBag.Erreurs = new List<string> { ex.Message };
            return View(formulaire);
        }
        TempData["Message"] = "Question enregistrée.";
        return RedirectToAction(nameof(Index), new { role = formulaire.RoleNumero, etape = formulaire.EtapeNumero });
    }
}

/// <summary>Questions et réponses sur une commande : un membre demande, le responsable du rôle répond et clôt.</summary>
public class DiscussionsController(IDiscussionService discussions, IAuthorizationService autorisation) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index(string filtre = "Ouverts", int page = 1)
    {
        ViewData["Title"] = "Questions et réponses";
        return View(await discussions.ListerAsync(filtre, page));
    }

    [AllowAnonymous]
    public async Task<IActionResult> Fil(int id)
    {
        var vm = await discussions.ChargerAsync(id);
        if (vm is null) return NotFound();
        vm.PeutResoudre = (await autorisation.AuthorizeAsync(User, vm.RoleNumero, Politiques.ModifierSonRole)).Succeeded;
        ViewData["Title"] = vm.Titre;
        return View(vm);
    }

    [Authorize]
    public async Task<IActionResult> Nouvelle(int commandeId)
    {
        var vm = await discussions.PreparerAsync(commandeId);
        if (vm is null) return NotFound();
        ViewData["Title"] = "Poser une question";
        return View(vm);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken, ValiderModele]
    public async Task<IActionResult> Nouvelle(NouveauFilVM formulaire)
    {
        var id = await discussions.CreerAsync(formulaire, User.Identifiant()!);
        TempData["Message"] = "Question envoyée au responsable du rôle.";
        return RedirectToAction(nameof(Fil), new { id });
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Repondre(NouveauMessageVM reponse)
    {
        if (!ModelState.IsValid)
        {
            TempData["Message"] = "Le message est vide.";
            return RedirectToAction(nameof(Fil), new { id = reponse.FilId });
        }
        await discussions.RepondreAsync(reponse, User.Identifiant()!);
        TempData["Message"] = "Réponse ajoutée.";
        return RedirectToAction(nameof(Fil), new { id = reponse.FilId });
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Statut(int id, bool resolu)
    {
        if (await discussions.RoleDuFilAsync(id) is not int role
            || !(await autorisation.AuthorizeAsync(User, role, Politiques.ModifierSonRole)).Succeeded) return Forbid();
        await discussions.ChangerStatutAsync(id, resolu);
        TempData["Message"] = resolu ? "Question marquée résolue." : "Question rouverte.";
        return RedirectToAction(nameof(Fil), new { id });
    }
}

/// <summary>Mode « Et si ? » : choisir une source, une destination et un port, voir le paquet passer ou être bloqué.</summary>
public class SimulationController(ISimulationService simulations, ITopologieService topologie) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index(string? source, string? destination, string? protocole, int? port, string? etat)
    {
        var e = etat is "A" or "B" ? etat : HttpContext.Session.Etat();
        var vm = await simulations.PreparerFormulaireAsync(e);
        vm.Schema = await topologie.ChargerSchemaAsync(e, User.Identity?.IsAuthenticated == true);
        if (source is not null) vm.Source = source;
        if (destination is not null) vm.Destination = destination;
        if (protocole is not null) vm.Protocole = protocole;
        if (port is not null) vm.Port = port;
        ViewData["Title"] = "Et si ?";
        return View(vm);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enregistrer(SimulationFormVM formulaire)
    {
        if (!ModelState.IsValid)
        {
            TempData["Message"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }
        await simulations.EnregistrerAsync(new DemandeSimulationDto
        {
            Source = formulaire.Source, Destination = formulaire.Destination, Protocole = formulaire.Protocole, Port = formulaire.Port,
            Etat = formulaire.Etat, Mesures = formulaire.Mesures, SshOpnsense = formulaire.SshOpnsense, DurcissementQ16 = formulaire.DurcissementQ16
        }, formulaire.Titre, User.Identifiant());
        TempData["Message"] = "Simulation enregistrée dans l'historique.";
        return RedirectToAction(nameof(Historique));
    }

    [AllowAnonymous]
    public async Task<IActionResult> Historique(int page = 1)
    {
        ViewData["Title"] = "Historique des simulations";
        return View(await simulations.HistoriqueAsync(page));
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Demo(int id, bool pourDemo, int page = 1)
    {
        await simulations.MarquerDemoAsync(id, pourDemo);
        TempData["Message"] = pourDemo ? "Ajoutée à la démonstration." : "Retirée de la démonstration.";
        return RedirectToAction(nameof(Historique), new { page });
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deplacer(int id, int sens)
    {
        await simulations.DeplacerDemoAsync(id, sens);
        return RedirectToAction(nameof(Demonstration));
    }

    /// <summary>Page de démonstration : les simulations choisies se rejouent dans l'ordre.</summary>
    [AllowAnonymous]
    public async Task<IActionResult> Demonstration()
    {
        ViewData["Title"] = "Démonstration";
        ViewBag.Schema = await topologie.ChargerSchemaAsync("A", User.Identity?.IsAuthenticated == true);
        return View(await simulations.DemoAsync());
    }
}

[AllowAnonymous]
public class GlossaireController(IGlossaireService glossaire) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Glossaire";
        return View(await glossaire.ListerAsync());
    }
}

/// <summary>Pages réservées au rôle 1 : comptes de l'équipe, avancement, préparation de la soutenance.</summary>
[Authorize(Roles = RolesIdentite.Admin)]
public class AdminController(UserManager<ApplicationUser> utilisateurs, IQuizService quiz, IProgressionService progression) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Administration";
        return View(await progression.AvancementEquipeAsync());
    }

    public async Task<IActionResult> Comptes()
    {
        ViewData["Title"] = "Comptes de l'équipe";
        return View(await ListerComptes());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Comptes(NouveauCompteVM nouveau)
    {
        if (ModelState.IsValid)
        {
            var compte = new ApplicationUser { UserName = nouveau.Email, Email = nouveau.Email, EmailConfirmed = true, NomAffiche = nouveau.Nom, NumeroRole = nouveau.NumeroRole };
            var r = await utilisateurs.CreateAsync(compte, nouveau.MotDePasse);
            if (r.Succeeded)
            {
                await utilisateurs.AddToRoleAsync(compte, nouveau.NumeroRole == 1 ? RolesIdentite.Admin : RolesIdentite.Membre);
                TempData["Message"] = $"Compte de {nouveau.Nom} créé (rôle {nouveau.NumeroRole}).";
                return RedirectToAction(nameof(Comptes));
            }
            foreach (var e in r.Errors) ModelState.AddModelError("", Traduire(e));
        }
        ViewBag.Erreurs = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
        ViewBag.Nouveau = nouveau;
        return View(await ListerComptes());
    }

    public async Task<IActionResult> Preparation()
    {
        ViewData["Title"] = "Préparation de la soutenance";
        return View(await quiz.PreparationAsync());
    }

    private async Task<List<CompteLigneVM>> ListerComptes()
    {
        var admins = (await utilisateurs.GetUsersInRoleAsync(RolesIdentite.Admin)).Select(u => u.Id).ToHashSet();
        var comptes = await utilisateurs.Users.OrderBy(u => u.NumeroRole).ThenBy(u => u.Email)
            .Select(u => new CompteLigneVM { Id = u.Id, Email = u.Email ?? "", Nom = u.NomAffiche, NumeroRole = u.NumeroRole, Verrouille = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow })
            .ToListAsync();
        foreach (var c in comptes) c.Admin = admins.Contains(c.Id);
        return comptes;
    }

    public static string Traduire(IdentityError e) => e.Code switch
    {
        "DuplicateUserName" or "DuplicateEmail" => "Un compte existe déjà avec cette adresse.",
        "PasswordTooShort" => "Le mot de passe doit faire 10 caractères au moins.",
        "PasswordRequiresDigit" => "Le mot de passe doit contenir un chiffre.",
        "PasswordRequiresLower" => "Le mot de passe doit contenir une minuscule.",
        "PasswordRequiresUpper" => "Le mot de passe doit contenir une majuscule.",
        "PasswordMismatch" => "Mot de passe actuel incorrect.",
        _ => e.Description
    };
}

/// <summary>Connexion, déconnexion et mot de passe (pages Identity en français, adaptées au style).</summary>
public class CompteController(SignInManager<ApplicationUser> connexion, UserManager<ApplicationUser> utilisateurs) : Controller
{
    [AllowAnonymous]
    public IActionResult Connexion(string? retour)
    {
        ViewData["Title"] = "Connexion";
        return View(new ConnexionVM { Retour = retour });
    }

    [AllowAnonymous]
    [HttpPost, ValidateAntiForgeryToken, ValiderModele]
    public async Task<IActionResult> Connexion(ConnexionVM vm)
    {
        var r = await connexion.PasswordSignInAsync(vm.Email, vm.MotDePasse, vm.Memoriser, lockoutOnFailure: true);
        if (r.Succeeded)
        {
            TempData["Message"] = "Tu es connecté.";
            return Url.IsLocalUrl(vm.Retour) ? LocalRedirect(vm.Retour) : RedirectToAction("Index", "Home");
        }
        ViewBag.Erreurs = new List<string> { r.IsLockedOut ? "Compte verrouillé après 5 essais : réessaie dans 10 minutes." : "Adresse ou mot de passe incorrect." };
        ViewData["Title"] = "Connexion";
        return View(vm);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deconnexion()
    {
        await connexion.SignOutAsync();
        TempData["Message"] = "Tu es déconnecté.";
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccesRefuse()
    {
        ViewData["Title"] = "Accès refusé";
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [Authorize]
    public IActionResult MotDePasse()
    {
        ViewData["Title"] = "Changer de mot de passe";
        return View(new ChangerMotDePasseVM());
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken, ValiderModele]
    public async Task<IActionResult> MotDePasse(ChangerMotDePasseVM vm)
    {
        var compte = await utilisateurs.GetUserAsync(User);
        if (compte is null) return Challenge();
        var r = await utilisateurs.ChangePasswordAsync(compte, vm.Actuel, vm.Nouveau);
        if (!r.Succeeded)
        {
            ViewBag.Erreurs = r.Errors.Select(AdminController.Traduire).ToList();
            ViewData["Title"] = "Changer de mot de passe";
            return View(vm);
        }
        await connexion.RefreshSignInAsync(compte);
        TempData["Message"] = "Mot de passe changé.";
        return RedirectToAction("Index", "Home");
    }
}
