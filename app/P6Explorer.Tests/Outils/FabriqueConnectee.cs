using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace P6Explorer.Tests.Outils;

/// <summary>
/// L'application complète avec un membre connecté (schéma d'authentification « Test »), pour vérifier que les pages
/// filmées n'affichent ni son nom, ni son adresse e-mail, ni les adresses réservées aux membres (Tailscale).
/// </summary>
public static class FabriqueConnectee
{
    public const string NomCompte = "camille.membre";
    public const string Courriel = "camille.membre@p6.lab";

    public static WebApplicationFactory<Program> Connecter(this WebApplicationFactory<Program> fabrique)
        => fabrique.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, AuthentificationTest>(AuthentificationTest.Schema, _ => { });
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = AuthentificationTest.Schema;
                o.DefaultAuthenticateScheme = AuthentificationTest.Schema;
                o.DefaultChallengeScheme = AuthentificationTest.Schema;
            });
        }));
}

/// <summary>Chaque requête est celle d'un membre connecté (rôle 2), avec nom et e-mail.</summary>
public sealed class AuthentificationTest(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory journal, UrlEncoder encodeur)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, journal, encodeur)
{
    public const string Schema = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identite = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "membre-test"),
            new Claim(ClaimTypes.Name, FabriqueConnectee.NomCompte),
            new Claim(ClaimTypes.Email, FabriqueConnectee.Courriel),
            new Claim(ClaimTypes.Role, "Membre"),
            new Claim("NumeroRole", "2"),
        ], Schema);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identite), Schema)));
    }
}
