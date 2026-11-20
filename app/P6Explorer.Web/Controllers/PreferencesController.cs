using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P6Explorer.Web.Models;

namespace P6Explorer.Web.Controllers;

/// <summary>État du laboratoire affiché (A ou B) et vitesse du lecteur, mémorisés en session.</summary>
[AllowAnonymous]
public class PreferencesController : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Etat(string etat, string? retour)
    {
        HttpContext.Session.ChoisirEtat(etat);
        TempData["Message"] = etat == "B" ? "État B affiché : microsegmentation et règles F01 à F99." : "État A affiché : pare-feu périmétrique.";
        return Url.IsLocalUrl(retour) ? LocalRedirect(retour) : RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Vitesse(double vitesse)
    {
        HttpContext.Session.ChoisirVitesse(vitesse);
        return NoContent();
    }
}
