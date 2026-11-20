using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using P6Explorer.Application.Services;
using P6Explorer.Infrastructure.Identite;

namespace P6Explorer.Web.Securite;

public static class Politiques
{
    /// <summary>Un membre ne modifie que les étapes de son rôle ; l'admin modifie tout.</summary>
    public const string ModifierSonRole = nameof(ModifierSonRole);
    public const string TypeNumeroRole = "NumeroRole";
}

/// <summary>Exigence de la politique ModifierSonRole ; la ressource vérifiée est le numéro du rôle de l'étape.</summary>
public sealed class ExigenceModifierSonRole : IAuthorizationRequirement;

/// <summary>
/// Lit la claim NumeroRole (ajoutée à la connexion par FabriqueClaimsP6) et la compare au rôle de l'étape :
/// ApplicationUser.NumeroRole == Etape.RoleNumero, ou rôle Identity « Admin ».
/// </summary>
public sealed class GestionnaireModifierSonRole : AuthorizationHandler<ExigenceModifierSonRole, int>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ExigenceModifierSonRole requirement, int roleNumero)
    {
        if (context.User.IsInRole(RolesIdentite.Admin)
            || context.User.FindFirst(Politiques.TypeNumeroRole)?.Value == roleNumero.ToString())
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>Ajoute la claim NumeroRole (et le nom affiché) au cookie de connexion.</summary>
public sealed class FabriqueClaimsP6(UserManager<ApplicationUser> utilisateurs, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(utilisateurs, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identite = await base.GenerateClaimsAsync(user);
        if (user.NumeroRole is int n) identite.AddClaim(new Claim(Politiques.TypeNumeroRole, n.ToString()));
        if (!string.IsNullOrWhiteSpace(user.NomAffiche)) identite.AddClaim(new Claim("NomAffiche", user.NomAffiche));
        return identite;
    }
}

/// <summary>Auteur des modifications pour l'historique : le compte de la requête en cours.</summary>
public sealed class UtilisateurCourantWeb(IHttpContextAccessor accesseur) : IUtilisateurCourant
{
    public string? Identifiant => accesseur.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public static class ExtensionsUtilisateur
{
    public static string? Identifiant(this ClaimsPrincipal u) => u.Identity?.IsAuthenticated == true ? u.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    public static string NomAffiche(this ClaimsPrincipal u) => u.FindFirstValue("NomAffiche") ?? u.Identity?.Name ?? "";
}
