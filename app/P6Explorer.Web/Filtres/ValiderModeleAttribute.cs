using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace P6Explorer.Web.Filtres;

/// <summary>
/// Filtre d'action : si le ModelState est invalide, renvoie la vue avec le formulaire reçu et la liste des erreurs
/// (ViewBag.Erreurs, affichée par la partielle _ErreursModele), sans répéter ce test dans chaque action.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ValiderModeleAttribute(string? vue = null) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid || context.Controller is not Controller controleur) return;
        controleur.ViewBag.Erreurs = context.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct().ToList();
        var modele = context.ActionArguments.Values.FirstOrDefault(v => v is not null && v.GetType().Name.EndsWith("VM"));
        context.Result = controleur.View(vue, modele);
    }
}
