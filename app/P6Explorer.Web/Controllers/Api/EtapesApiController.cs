using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P6Explorer.Application.Dto;
using P6Explorer.Application.Services;
using P6Explorer.Domain.Interfaces;
using P6Explorer.Web.Models;
using P6Explorer.Web.Securite;

namespace P6Explorer.Web.Controllers.Api;

/// <summary>Web API lue par lecteur.js : les commandes d'une étape et leurs effets, calculés pour l'état A ou B.</summary>
[ApiController]
[Route("api/etapes")]
[AllowAnonymous]
public class EtapesApiController(IEtapeService etapes, IEtapeRepository depot) : ControllerBase
{
    /// <summary>Commandes à rejouer (sous-commandes dépliées) ; 404 si l'étape n'existe pas.</summary>
    [HttpGet("{id:int}/commandes")]
    [ProducesResponseType<List<CommandeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CommandeDto>>> Commandes(int id, string? etat)
    {
        if (await depot.TrouverAsync(id) is null) return NotFound();
        var e = etat is "A" or "B" ? etat : HttpContext.Session.Etat();
        return Ok(await etapes.ListerCommandesAsync(id, e, User.Identifiant()));
    }
}

/// <summary>Coche ou décoche « j'ai compris » sur une commande (membres connectés).</summary>
[ApiController]
[Route("api/progression")]
[Authorize]
public class ProgressionApiController(IProgressionService progression) : ControllerBase
{
    [HttpPost("{commandeId:int}")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Basculer(int commandeId)
    {
        await progression.BasculerAsync(User.Identifiant()!, commandeId);
        return NoContent();
    }
}

/// <summary>Mode « Et si ? » : simule un paquet et renvoie le chemin, le verdict, la règle et la trame décodée.</summary>
[ApiController]
[Route("api/simulation")]
[AllowAnonymous]
public class SimulationApiController(ISimulationService simulations) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ResultatPaquetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultatPaquetDto>> Simuler(DemandeSimulationDto demande)
    {
        if (string.IsNullOrWhiteSpace(demande.Source) || string.IsNullOrWhiteSpace(demande.Destination))
            return BadRequest("Source et destination sont obligatoires.");
        return Ok(await simulations.SimulerAsync(demande));
    }

    /// <summary>Les simulations choisies pour la démonstration, dans l'ordre, recalculées.</summary>
    [HttpGet("demo")]
    public async Task<ActionResult<List<object>>> Demo()
    {
        var liste = new List<object>();
        foreach (var s in await simulations.DemoAsync())
        {
            var r = await simulations.SimulerAsync(new DemandeSimulationDto { Source = s.Source, Destination = s.Destination, Protocole = s.Protocole, Port = s.Port, Etat = s.Etat });
            liste.Add(new { s.Id, s.Titre, s.Etat, Resultat = r });
        }
        return Ok(liste);
    }
}
