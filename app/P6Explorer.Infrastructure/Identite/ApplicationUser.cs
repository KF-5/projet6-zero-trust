using Microsoft.AspNetCore.Identity;

namespace P6Explorer.Infrastructure.Identite;

/// <summary>Compte d'un membre de l'équipe. NumeroRole : 1 à 6 (null pour un compte sans rôle du projet).</summary>
public class ApplicationUser : IdentityUser
{
    public int? NumeroRole { get; set; }
    public string? NomAffiche { get; set; }
}

/// <summary>Noms des rôles Identity créés au démarrage.</summary>
public static class RolesIdentite
{
    public const string Admin = "Admin";     // rôle 1 : chef de projet
    public const string Membre = "Membre";   // rôles 2 à 6
}
