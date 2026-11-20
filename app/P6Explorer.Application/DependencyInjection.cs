using Microsoft.Extensions.DependencyInjection;
using P6Explorer.Application.Mappings;
using P6Explorer.Application.Services;

namespace P6Explorer.Application;

public static class DependencyInjection
{
    /// <summary>Services applicatifs (une interface, une implémentation, durée de vie Scoped) et AutoMapper.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(ProfilP6).Assembly);
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ITopologieService, TopologieService>();
        return services;
    }
}
