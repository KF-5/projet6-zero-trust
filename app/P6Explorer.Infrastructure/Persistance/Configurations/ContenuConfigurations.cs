using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using P6Explorer.Domain.Commandes;
using P6Explorer.Infrastructure.Identite;

namespace P6Explorer.Infrastructure.Persistance.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles6");                                  // « Roles » est déjà pris par Identity (AspNetRoles)
        b.HasKey(r => r.Numero);
        b.Property(r => r.Numero).ValueGeneratedNever();
        b.Property(r => r.Cle).HasMaxLength(20).IsRequired();
        b.HasIndex(r => r.Cle).IsUnique();
        b.Property(r => r.Titre).HasMaxLength(120).IsRequired();
        b.Property(r => r.CourtNom).HasMaxLength(40).IsRequired();
        b.Property(r => r.Couleur).HasMaxLength(10).IsRequired();
        b.Property(r => r.Mission).HasMaxLength(1000).IsRequired();

        // Relation 1-n : un rôle a ses étapes, supprimées avec lui.
        b.HasMany(r => r.Etapes).WithOne(e => e.Role).HasForeignKey(e => e.RoleNumero).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EtapeConfiguration : IEntityTypeConfiguration<Etape>
{
    public void Configure(EntityTypeBuilder<Etape> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Code).HasMaxLength(10).IsRequired();
        b.HasIndex(e => e.Code).IsUnique();
        b.HasIndex(e => new { e.RoleNumero, e.Numero }).IsUnique();
        b.Property(e => e.Titre).HasMaxLength(200).IsRequired();
        b.Property(e => e.Quand).HasMaxLength(60);
        b.Property(e => e.EtatTexte).HasMaxLength(40);
        b.Property(e => e.Statut).HasConversion<string>().HasMaxLength(12);
        b.Property(e => e.Schema).HasMaxLength(40);
        b.Property(e => e.But).IsRequired();
        b.Property(e => e.Pourquoi).IsRequired();

        b.HasMany(e => e.Commandes).WithOne(c => c.Etape).HasForeignKey(c => c.EtapeId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.Pannes).WithOne().HasForeignKey(p => p.EtapeId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.Criteres).WithOne().HasForeignKey(c => c.EtapeId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.Preuves).WithOne(p => p.Etape).HasForeignKey(p => p.EtapeId).OnDelete(DeleteBehavior.Cascade);

        // Relation n-n avec le glossaire : table de jointure EtapeTerme.
        b.HasMany(e => e.Termes).WithMany(t => t.Etapes).UsingEntity("EtapeTerme");

        // Relation 1-1 : une étape a au plus un quiz ; la clé étrangère est du côté du quiz.
        b.HasOne(e => e.Quiz).WithOne(q => q.Etape).HasForeignKey<Domain.Suivi.Quiz>(q => q.EtapeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CommandeConfiguration : IEntityTypeConfiguration<Commande>
{
    public void Configure(EntityTypeBuilder<Commande> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Code).HasMaxLength(16).IsRequired();
        b.HasIndex(c => c.Code).IsUnique();
        b.HasIndex(c => new { c.EtapeId, c.Ordre });
        b.Property(c => c.Ou).HasConversion(Convertisseurs.Noeud).HasMaxLength(40).IsRequired();
        b.Property(c => c.Shell).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.Compte).HasMaxLength(20);
        b.Property(c => c.Bloc).HasMaxLength(300);
        b.Property(c => c.Texte).IsRequired();
        b.Property(c => c.Explication).IsRequired();

        // Sous-commandes d'un script déplié (prompt, règle 4). Pas de cascade sur une auto-référence (SQL Server).
        b.HasMany(c => c.SousCommandes).WithOne(c => c.Parent).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(c => c.Effets).WithOne(e => e.Commande).HasForeignKey(e => e.CommandeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PanneConfiguration : IEntityTypeConfiguration<Panne>
{
    public void Configure(EntityTypeBuilder<Panne> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Symptome).HasMaxLength(500).IsRequired();
        b.Property(p => p.Cause).HasMaxLength(1000).IsRequired();
        b.Property(p => p.Solution).HasMaxLength(1500).IsRequired();
    }
}

public class CritereFinConfiguration : IEntityTypeConfiguration<CritereFin>
{
    public void Configure(EntityTypeBuilder<CritereFin> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Texte).HasMaxLength(500).IsRequired();
    }
}

public class PreuveConfiguration : IEntityTypeConfiguration<Preuve>
{
    public void Configure(EntityTypeBuilder<Preuve> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.NomFichier).HasMaxLength(100).IsRequired();
        b.HasIndex(p => p.NomFichier).IsUnique();
        b.Property(p => p.NomOriginal).HasMaxLength(255).IsRequired();
        b.Property(p => p.TypeMime).HasMaxLength(40).IsRequired();
        b.Property(p => p.Legende).HasMaxLength(300);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.AuteurId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class TermeGlossaireConfiguration : IEntityTypeConfiguration<TermeGlossaire>
{
    public void Configure(EntityTypeBuilder<TermeGlossaire> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Terme).HasMaxLength(60).IsRequired();
        b.HasIndex(t => t.Terme).IsUnique();
        b.Property(t => t.Definition).HasMaxLength(600).IsRequired();
        b.Property(t => t.Comparaison).HasMaxLength(600);
    }
}
