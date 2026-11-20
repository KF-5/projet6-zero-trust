using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using P6Explorer.Domain.Commandes;
using P6Explorer.Domain.Suivi;
using P6Explorer.Domain.Topologie;
using P6Explorer.Infrastructure.Identite;

namespace P6Explorer.Infrastructure.Persistance;

/// <summary>
/// Contexte EF Core 8 (Code First). Hérite d'IdentityDbContext : les tables des comptes (AspNetUsers…)
/// vivent dans la même base que le contenu. Toute la configuration est en API Fluent, une classe
/// IEntityTypeConfiguration&lt;T&gt; par entité (dossier Configurations).
/// </summary>
public class P6DbContext(DbContextOptions<P6DbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public const string DateCreation = nameof(DateCreation);
    public const string DateModification = nameof(DateModification);

    /// <summary>Faux pendant l'amorçage : le chargement initial du guide n'est pas une modification de l'équipe.</summary>
    public bool JournaliserRevisions { get; set; } = true;

    // Contenu du guide
    public DbSet<Role> RolesProjet => Set<Role>();
    public DbSet<Etape> Etapes => Set<Etape>();
    public DbSet<Commande> Commandes => Set<Commande>();
    public DbSet<Effet> Effets => Set<Effet>();
    public DbSet<Panne> Pannes => Set<Panne>();
    public DbSet<CritereFin> Criteres => Set<CritereFin>();
    public DbSet<Preuve> Preuves => Set<Preuve>();
    public DbSet<TermeGlossaire> Glossaire => Set<TermeGlossaire>();

    // Topologie du laboratoire
    public DbSet<EtatReseau> EtatsReseau => Set<EtatReseau>();
    public DbSet<Noeud> Noeuds => Set<Noeud>();
    public DbSet<Interface> Interfaces => Set<Interface>();
    public DbSet<Reseau> Reseaux => Set<Reseau>();
    public DbSet<Lien> Liens => Set<Lien>();
    public DbSet<Regle> Regles => Set<Regle>();
    public DbSet<RegleNat> ReglesNat => Set<RegleNat>();

    // Données vivantes de l'équipe
    public DbSet<Revision> Revisions => Set<Revision>();
    public DbSet<Quiz> Quiz => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Choix> Choix => Set<Choix>();
    public DbSet<Tentative> Tentatives => Set<Tentative>();
    public DbSet<Reponse> Reponses => Set<Reponse>();
    public DbSet<Progression> Progressions => Set<Progression>();
    public DbSet<Fil> Fils => Set<Fil>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Simulation> Simulations => Set<Simulation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);   // tables d'Identity
        builder.ApplyConfigurationsFromAssembly(typeof(P6DbContext).Assembly);

        // Propriétés shadow : DateCreation et DateModification sur chaque entité éditable (racine de hiérarchie),
        // remplies par IntercepteurHorodatage. Elles n'existent pas dans les classes du domaine.
        foreach (var type in builder.Model.GetEntityTypes()
                     .Where(t => typeof(IHorodate).IsAssignableFrom(t.ClrType) && t.BaseType is null && !t.IsOwned()))
        {
            builder.Entity(type.ClrType).Property<DateTime>(DateCreation);
            builder.Entity(type.ClrType).Property<DateTime>(DateModification);
        }
    }
}
