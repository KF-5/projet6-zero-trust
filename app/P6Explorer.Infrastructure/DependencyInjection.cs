using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using P6Explorer.Infrastructure.Identite;
using P6Explorer.Application.Services;
using P6Explorer.Domain.Interfaces;
using P6Explorer.Infrastructure.Amorcage;
using P6Explorer.Infrastructure.Persistance;
using P6Explorer.Infrastructure.Repositories;

namespace P6Explorer.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Base, repositories et amorçage. <paramref name="configurerBase"/> permet aux tests de remplacer
    /// SQL Server LocalDB par SQLite en mémoire.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configurerBase = null)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IntercepteurHorodatage>();
        services.AddDbContext<P6DbContext>((fournisseur, o) =>
        {
            if (configurerBase is not null) configurerBase(o);
            else o.UseSqlServer(configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("Chaîne de connexion « DefaultConnection » absente."));
            o.AddInterceptors(fournisseur.GetRequiredService<IntercepteurHorodatage>());
            // Pas de lazy loading : chaque accès à une navigation déclencherait une requête (N+1).
        });

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ITopologieRepository, TopologieRepository>();
        services.AddScoped<IEtapeRepository, EtapeRepository>();
        services.AddScoped<IRevisionRepository, RevisionRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<IProgressionRepository, ProgressionRepository>();
        services.AddScoped<IDiscussionRepository, DiscussionRepository>();
        services.AddScoped<ISimulationRepository, SimulationRepository>();
        services.AddScoped<IGlossaireRepository, GlossaireRepository>();
        services.AddScoped<IAnnuaire, Annuaire>();
        // Auteur des modifications : remplacé par la version web (compte connecté) dans Program.cs.
        services.TryAddSingleton<IUtilisateurCourant, AucunUtilisateur>();

        services.Configure<OptionsAmorcage>(configuration.GetSection(OptionsAmorcage.Section));
        services.AddScoped<IServiceAmorcage, ServiceAmorcage>();
        return services;
    }
}
