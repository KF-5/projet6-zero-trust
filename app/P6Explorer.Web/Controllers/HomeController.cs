using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P6Explorer.Application.Services;
using P6Explorer.Application.ViewModels;

namespace P6Explorer.Web.Controllers;

[AllowAnonymous]
public class HomeController(IRoleService roles, ITopologieService topologie) : Controller
{
    /// <summary>Accueil : la carte complète de l'état A et les six cartes de rôle.</summary>
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Accueil";
        var vm = new AccueilVM
        {
            Roles = await roles.ListerCartesAsync(),
            Schema = await topologie.ChargerSchemaAsync("A", User.Identity?.IsAuthenticated == true),
        };
        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Erreur()
    {
        ViewData["Title"] = "Erreur";
        ViewBag.Identifiant = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
