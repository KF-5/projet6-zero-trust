using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P6Explorer.Application.Services;
using P6Explorer.Web.Models;
using P6Explorer.Web.Securite;

namespace P6Explorer.Web.Controllers;

/// <summary>Vue rôle : la liste des étapes, et pour l'étape choisie le schéma, le terminal et l'explication.</summary>
[AllowAnonymous]
[Route("Roles")]
public class RolesController(IEtapeService etapes, IRoleService roles, IAuthorizationService autorisation, P6Explorer.Web.Services.SchemasGuide schemasGuide) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => RedirectToAction("Index", "Home", null, "titre-roles");

    [HttpGet("{numero:int:range(1,6)}")]
    public async Task<IActionResult> Role(int numero)
    {
        var carte = (await roles.ListerCartesAsync()).FirstOrDefault(r => r.Numero == numero);
        if (carte is null) return NotFound();
        var premiere = carte.PremiereEtape ?? 1;
        return RedirectToAction(nameof(Etape), new { numero, etape = premiere });
    }

    /// <summary>/Roles/2/Etapes/4?commande=3 : l'URL désigne une commande précise (partageable).</summary>
    [HttpGet("{numero:int:range(1,6)}/Etapes/{etape:int:range(0,20)}")]
    public async Task<IActionResult> Etape(int numero, int etape, int? commande, string? code)
    {
        var vm = await etapes.ChargerAsync(numero, etape, HttpContext.Session.Etat(), User.Identifiant());
        if (vm is null) return NotFound();
        vm.CommandeInitiale = Math.Max(1, commande ?? 1);
        vm.Vitesse = HttpContext.Session.Vitesse();
        vm.PeutModifier = (await autorisation.AuthorizeAsync(User, numero, Politiques.ModifierSonRole)).Succeeded;
        vm.SchemaGuide = schemasGuide.Trouver(vm.Etape.Schema);
        ViewData["Title"] = $"Rôle {numero} · {vm.Etape.Titre}";
        ViewBag.CodeInitial = code;
        return View(vm);
    }
}
