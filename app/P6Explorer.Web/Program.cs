using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using P6Explorer.Application;
using P6Explorer.Application.Services;
using P6Explorer.Infrastructure;
using P6Explorer.Infrastructure.Identite;
using P6Explorer.Infrastructure.Persistance;

var builder = WebApplication.CreateBuilder(args);

// ---------- Injection de dépendances (toutes les couches sont branchées ici) ----------
builder.Services.AddControllersWithViews();
builder.Services.AddApplication();                                   // services applicatifs + AutoMapper
builder.Services.AddInfrastructure(builder.Configuration);           // EF Core, repositories, amorçage

// Identity : comptes des membres, rôles Admin / Membre (pages de connexion au jalon 7).
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireUppercase = true;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;                       // verrouillage après 5 échecs (TP5)
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<P6DbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "P6 Explorer — API",
    Version = "v1",
    Description = "Données JSON utilisées par le lecteur de commandes (animation du schéma)."
}));

var app = builder.Build();

// ---------- Base : migrations puis amorçage depuis donnees/ (une seule fois) ----------
using (var scope = app.Services.CreateScope())
{
    var amorcage = scope.ServiceProvider.GetRequiredService<IServiceAmorcage>();
    await amorcage.AmorcerAsync(app.Environment.ContentRootPath);
}

// ---------- Pipeline HTTP ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.DocumentTitle = "P6 Explorer — API");
}
else
{
    app.UseExceptionHandler("/Home/Erreur");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Minimal API : état de l'application et de la base (utile avant une démonstration).
app.MapGet("/api/sante", async (P6DbContext db) =>
{
    var baseOk = await db.Database.CanConnectAsync();
    return Results.Ok(new
    {
        application = "ok",
        @base = baseOk ? "ok" : "injoignable",
        roles = baseOk ? await db.RolesProjet.CountAsync() : 0,
        etapes = baseOk ? await db.Etapes.CountAsync() : 0,
        commandes = baseOk ? await db.Commandes.CountAsync() : 0,
    });
}).WithName("Sante").WithTags("Santé");

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>Rendu visible pour les tests d'intégration (WebApplicationFactory).</summary>
public partial class Program;
