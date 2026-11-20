using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using P6Explorer.Application;
using P6Explorer.Infrastructure;
using P6Explorer.Infrastructure.Identite;
using P6Explorer.Infrastructure.Persistance;

namespace P6Explorer.Tests.Outils;

/// <summary>
/// Conteneur de services complet (Application + Infrastructure + Identity) sur une base SQLite en mémoire :
/// les relations, contraintes et index sont vérifiés, contrairement au fournisseur InMemory.
/// La connexion reste ouverte pendant toute la durée de vie de l'objet (sinon la base disparaît).
/// </summary>
public sealed class BaseDeTest : IDisposable
{
    private readonly SqliteConnection _connexion = new("DataSource=:memory:");
    public ServiceProvider Services { get; }

    public BaseDeTest(string? motDePasseAdmin = null)
    {
        _connexion.Open();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Amorcage:AppliquerMigrations"] = "false",
            ["Amorcage:DossierDonnees"] = Depot.Chemin("donnees"),
            ["Amorcage:MotDePasseAdmin"] = motDePasseAdmin,
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration, o => o.UseSqlite(_connexion));
        services.AddIdentityCore<ApplicationUser>(o => { o.Password.RequiredLength = 10; o.User.RequireUniqueEmail = true; })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<P6DbContext>();
        Services = services.BuildServiceProvider(validateScopes: true);
    }

    public void Dispose()
    {
        Services.Dispose();
        _connexion.Dispose();
    }
}
