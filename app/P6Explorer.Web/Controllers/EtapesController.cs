using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P6Explorer.Application.Services;
using P6Explorer.Application.ViewModels;
using P6Explorer.Domain.Exceptions;
using P6Explorer.Web.Filtres;
using P6Explorer.Web.Models;
using P6Explorer.Web.Securite;

namespace P6Explorer.Web.Controllers;

/// <summary>
/// Édition du contenu (CRUD étapes, commandes, effets ; preuves ; historique et retour arrière).
/// Chaque action vérifie la politique ModifierSonRole sur le rôle de l'étape concernée.
/// </summary>
[Authorize]
public class EtapesController(IEditionService edition, IAuthorizationService autorisation, IWebHostEnvironment environnement) : Controller
{
    private static readonly Dictionary<string, (string Mime, byte[] Signature)> ImagesAcceptees = new()
    {
        [".png"] = ("image/png", [0x89, 0x50, 0x4e, 0x47]),
        [".jpg"] = ("image/jpeg", [0xff, 0xd8, 0xff]),
        [".jpeg"] = ("image/jpeg", [0xff, 0xd8, 0xff]),
    };

    private async Task<bool> PeutModifier(int? role) => role is int r && (await autorisation.AuthorizeAsync(User, r, Politiques.ModifierSonRole)).Succeeded;

    private IActionResult VersEtape(int role, int etape, int? commande = null)
        => RedirectToAction("Etape", "Roles", new { numero = role, etape, commande });

    // ---------------------------------------------------------------- étape

    [HttpGet]
    public async Task<IActionResult> Modifier(int id)
    {
        var f = await edition.EtapeAsync(id);
        if (f is null) return NotFound();
        if (!await PeutModifier(f.RoleNumero)) return Forbid();
        ViewData["Title"] = $"Modifier l'étape {f.Code}";
        return View(f);
    }

    [HttpPost, ValidateAntiForgeryToken, ValiderModele]
    public async Task<IActionResult> Modifier(EtapeFormVM formulaire)
    {
        if (!await PeutModifier(await edition.RoleDeEtapeAsync(formulaire.Id))) return Forbid();
        await edition.ModifierEtapeAsync(formulaire);
        TempData["Message"] = "Étape enregistrée.";
        return VersEtape(formulaire.RoleNumero, formulaire.Numero);
    }

    // ---------------------------------------------------------------- commandes

    [HttpGet]
    public async Task<IActionResult> NouvelleCommande(int etapeId)
    {
        if (!await PeutModifier(await edition.RoleDeEtapeAsync(etapeId))) return Forbid();
        ViewData["Title"] = "Nouvelle commande";
        return View("Commande", await edition.NouvelleCommandeAsync(etapeId));
    }

    [HttpGet]
    public async Task<IActionResult> Commande(int id)
    {
        var f = await edition.CommandeAsync(id);
        if (f is null) return NotFound();
        if (!await PeutModifier(f.RoleNumero)) return Forbid();
        ViewData["Title"] = $"Modifier la commande {f.Code}";
        return View(f);
    }

    [HttpPost, ValidateAntiForgeryToken, ValiderModele("Commande")]
    public async Task<IActionResult> Commande(CommandeFormVM formulaire)
    {
        var role = formulaire.Id == 0 ? await edition.RoleDeEtapeAsync(formulaire.EtapeId) : await edition.RoleDeCommandeAsync(formulaire.Id);
        if (!await PeutModifier(role)) return Forbid();
        var id = await edition.EnregistrerCommandeAsync(formulaire);
        TempData["Message"] = formulaire.Id == 0 ? "Commande ajoutée : précise maintenant ses effets." : "Commande enregistrée.";
        return RedirectToAction(nameof(Commande), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SupprimerCommande(int id)
    {
        if (!await PeutModifier(await edition.RoleDeCommandeAsync(id))) return Forbid();
        var (role, etape) = await edition.SupprimerCommandeAsync(id);
        TempData["Message"] = "Commande supprimée (annulable depuis l'historique).";
        return VersEtape(role, etape);
    }

    // ---------------------------------------------------------------- effets

    [HttpGet]
    public async Task<IActionResult> Effet(int commandeId, int id = 0)
    {
        if (!await PeutModifier(await edition.RoleDeCommandeAsync(commandeId))) return Forbid();
        var f = await edition.EffetAsync(commandeId, id);
        if (f is null) return NotFound();
        ViewData["Title"] = id == 0 ? "Nouvel effet" : "Modifier un effet";
        return View(f);
    }

    [HttpPost, ValidateAntiForgeryToken, ValiderModele]
    public async Task<IActionResult> Effet(EffetFormVM formulaire)
    {
        if (!await PeutModifier(await edition.RoleDeCommandeAsync(formulaire.CommandeId))) return Forbid();
        try
        {
            await edition.EnregistrerEffetAsync(formulaire);
        }
        catch (ExceptionMetier ex)
        {
            ModelState.AddModelError(nameof(formulaire.Json), ex.Message);
            ViewBag.Erreurs = new List<string> { ex.Message };
            return View(formulaire);
        }
        TempData["Message"] = "Effet enregistré.";
        return RedirectToAction(nameof(Commande), new { id = formulaire.CommandeId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SupprimerEffet(int commandeId, int id)
    {
        if (!await PeutModifier(await edition.RoleDeCommandeAsync(commandeId))) return Forbid();
        try
        {
            await edition.SupprimerEffetAsync(commandeId, id);
            TempData["Message"] = "Effet supprimé.";
        }
        catch (ExceptionMetier ex) { TempData["Message"] = ex.Message; }
        return RedirectToAction(nameof(Commande), new { id = commandeId });
    }

    // ---------------------------------------------------------------- preuves (IFormFile)

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(PreuveFormVM.TailleMax + 64 * 1024)]
    public async Task<IActionResult> Preuve(PreuveFormVM formulaire, int role, int etape)
    {
        if (!await PeutModifier(await edition.RoleDeEtapeAsync(formulaire.EtapeId))) return Forbid();
        var f = formulaire.Fichier;
        var extension = Path.GetExtension(f?.FileName ?? "").ToLowerInvariant();
        string? erreur = null;
        if (!ModelState.IsValid || f is null) erreur = "Choisis une image.";
        else if (f.Length == 0 || f.Length > PreuveFormVM.TailleMax) erreur = "L'image doit faire 5 Mo au plus.";
        else if (!ImagesAcceptees.TryGetValue(extension, out var attendu) || !await SignatureValide(f, attendu.Signature))
            erreur = "Seules les images PNG et JPEG sont acceptées.";
        if (erreur is not null)
        {
            TempData["Message"] = erreur;
            return VersEtape(role, etape);
        }

        // Nom généré : jamais le nom envoyé par le navigateur (ni chemin, ni écrasement possibles).
        var nom = $"{Guid.NewGuid():N}{extension}";
        var dossier = Path.Combine(environnement.WebRootPath, "preuves");
        Directory.CreateDirectory(dossier);
        await using (var flux = System.IO.File.Create(Path.Combine(dossier, nom)))
            await f!.CopyToAsync(flux);
        await edition.AjouterPreuveAsync(formulaire.EtapeId, nom, Path.GetFileName(f.FileName), ImagesAcceptees[extension].Mime,
            f.Length, formulaire.Legende, User.Identifiant());
        TempData["Message"] = "Preuve ajoutée à l'étape.";
        return VersEtape(role, etape);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SupprimerPreuve(int id, int etapeId, int role, int etape)
    {
        if (!await PeutModifier(await edition.RoleDeEtapeAsync(etapeId))) return Forbid();
        var nom = await edition.SupprimerPreuveAsync(id);
        if (nom is not null)
        {
            var chemin = Path.Combine(environnement.WebRootPath, "preuves", Path.GetFileName(nom));
            if (System.IO.File.Exists(chemin)) System.IO.File.Delete(chemin);
        }
        TempData["Message"] = "Preuve supprimée.";
        return VersEtape(role, etape);
    }

    private static async Task<bool> SignatureValide(IFormFile f, byte[] signature)
    {
        await using var flux = f.OpenReadStream();
        var debut = new byte[signature.Length];
        return await flux.ReadAsync(debut) == signature.Length && debut.SequenceEqual(signature);
    }

    // ---------------------------------------------------------------- historique

    [HttpGet]
    public async Task<IActionResult> Historique(int id, int page = 1)
    {
        var vm = await edition.HistoriqueAsync(id, page);
        if (vm is null) return NotFound();
        vm.PeutModifier = await PeutModifier(vm.RoleNumero);
        ViewData["Title"] = $"Historique · {vm.Titre}";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Annuler(Guid lot, int etapeId)
    {
        if (!await PeutModifier(await edition.RoleDeEtapeAsync(etapeId))) return Forbid();
        TempData["Message"] = await edition.AnnulerAsync(lot);
        return RedirectToAction(nameof(Historique), new { id = etapeId });
    }
}
