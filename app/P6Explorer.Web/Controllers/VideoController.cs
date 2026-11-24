using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P6Explorer.Application.Services;

namespace P6Explorer.Web.Controllers;

/// <summary>
/// Section « Vidéo » : les plans P-1 à P-8 à filmer. Pages publiques et en lecture seule ; aucune adresse réservée,
/// aucun nom de compte ni bouton de connexion à l'image (le service force MembreConnecte = false).
/// </summary>
[AllowAnonymous]
[Route("Video")]
public class VideoController(IVideoService video) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Vidéo";
        ViewData["MasquerCompte"] = true;
        return View(await video.ListerAsync());
    }

    /// <summary>Une scène en mode tournage. ?propre=1 : sans le coin « P-n » ; ?lecture=auto : démarre seule (vérifications).</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Scene(string id, string? propre = null, string? lecture = null)
    {
        var vm = await video.ChargerSceneAsync(id);
        if (vm is null) return NotFound();
        vm.Propre = propre is "1" or "true";                 // ?propre=1 (le binder de bool n'accepte pas « 1 »)
        vm.LectureAuto = lecture == "auto";
        ViewData["Title"] = $"{vm.Id} · {vm.Titre}";
        return View(vm);
    }
}

/// <summary>La politique comme donnée (flows.csv → c2.py → règles et tests), pour l'équipe, hors tournage.</summary>
[AllowAnonymous]
[Route("Politique")]
public class PolitiqueController(IPolitiqueService politique) : Controller
{
    [HttpGet("")]
    public IActionResult Index(string? etat)
    {
        var e = etat is "B" ? "B" : "A";
        ViewData["Title"] = $"Politique · état {e}";
        ViewData["MasquerCompte"] = true;
        return View(politique.Matrice(e));
    }
}
