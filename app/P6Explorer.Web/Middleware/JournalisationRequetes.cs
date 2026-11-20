using System.Diagnostics;

namespace P6Explorer.Web.Middleware;

/// <summary>Middleware personnalisé : une ligne de journal par requête (méthode, chemin, utilisateur, statut, durée).</summary>
public sealed class JournalisationRequetes(RequestDelegate suivant, ILogger<JournalisationRequetes> journal)
{
    public async Task InvokeAsync(HttpContext contexte)
    {
        var chrono = Stopwatch.StartNew();
        try
        {
            await suivant(contexte);
        }
        finally
        {
            chrono.Stop();
            if (!contexte.Request.Path.StartsWithSegments("/css") && !contexte.Request.Path.StartsWithSegments("/js")
                && !contexte.Request.Path.StartsWithSegments("/fonts"))
                journal.LogInformation("{Methode} {Chemin} · {Utilisateur} · {Statut} · {Duree} ms",
                    contexte.Request.Method, contexte.Request.Path + contexte.Request.QueryString,
                    contexte.User.Identity?.IsAuthenticated == true ? contexte.User.Identity.Name : "visiteur",
                    contexte.Response.StatusCode, chrono.ElapsedMilliseconds);
        }
    }
}

public static class ExtensionsJournalisation
{
    public static IApplicationBuilder UseJournalisationRequetes(this IApplicationBuilder app) => app.UseMiddleware<JournalisationRequetes>();
}
