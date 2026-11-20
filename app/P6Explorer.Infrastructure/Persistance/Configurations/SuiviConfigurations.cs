using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using P6Explorer.Domain.Suivi;
using P6Explorer.Domain.Topologie;
using P6Explorer.Infrastructure.Identite;

namespace P6Explorer.Infrastructure.Persistance.Configurations;

public class RevisionConfiguration : IEntityTypeConfiguration<Revision>
{
    public void Configure(EntityTypeBuilder<Revision> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Entite).HasMaxLength(40).IsRequired();
        b.Property(r => r.Champ).HasMaxLength(60).IsRequired();
        b.HasIndex(r => new { r.EtapeId, r.Date });
        b.HasIndex(r => new { r.Entite, r.EntiteId });
        // Pas de clé étrangère vers l'étape : l'historique survit à la suppression d'une étape.
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.AuteurId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> b)
    {
        b.HasKey(q => q.Id);
        b.HasIndex(q => q.EtapeId).IsUnique();
        b.HasMany(q => q.Questions).WithOne(x => x.Quiz).HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(q => q.Tentatives).WithOne(t => t.Quiz).HasForeignKey(t => t.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> b)
    {
        b.HasKey(q => q.Id);
        b.Property(q => q.Texte).HasMaxLength(1000).IsRequired();
        b.Property(q => q.Statut).HasConversion<string>().HasMaxLength(12);
        b.Property(q => q.Source).HasMaxLength(40);
        b.HasMany(q => q.Choix).WithOne(c => c.Question).HasForeignKey(c => c.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChoixConfiguration : IEntityTypeConfiguration<Choix>
{
    public void Configure(EntityTypeBuilder<Choix> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Texte).HasMaxLength(500).IsRequired();
        b.Property(c => c.Explication).HasMaxLength(1000);
    }
}

public class TentativeConfiguration : IEntityTypeConfiguration<Tentative>
{
    public void Configure(EntityTypeBuilder<Tentative> b)
    {
        b.HasKey(t => t.Id);
        b.HasIndex(t => new { t.UtilisateurId, t.QuizId });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.UtilisateurId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(t => t.Reponses).WithOne(r => r.Tentative).HasForeignKey(r => r.TentativeId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Jointure n-n Tentative ↔ Choix (clé composite).</summary>
public class ReponseConfiguration : IEntityTypeConfiguration<Reponse>
{
    public void Configure(EntityTypeBuilder<Reponse> b)
    {
        b.HasKey(r => new { r.TentativeId, r.ChoixId });
        // Pas de cascade depuis Choix : SQL Server refuse deux chemins de cascade depuis Quiz
        // (Quiz → Tentative → Reponse et Quiz → Question → Choix → Reponse).
        b.HasOne(r => r.Choix).WithMany().HasForeignKey(r => r.ChoixId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProgressionConfiguration : IEntityTypeConfiguration<Progression>
{
    public void Configure(EntityTypeBuilder<Progression> b)
    {
        b.HasKey(p => p.Id);
        b.HasIndex(p => new { p.UtilisateurId, p.CommandeId }).IsUnique();
        b.HasOne(p => p.Commande).WithMany().HasForeignKey(p => p.CommandeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UtilisateurId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FilConfiguration : IEntityTypeConfiguration<Fil>
{
    public void Configure(EntityTypeBuilder<Fil> b)
    {
        b.HasKey(f => f.Id);
        b.Property(f => f.Titre).HasMaxLength(200).IsRequired();
        b.Property(f => f.Statut).HasConversion<string>().HasMaxLength(8);
        b.HasIndex(f => new { f.CommandeId, f.Statut });
        b.HasOne(f => f.Commande).WithMany().HasForeignKey(f => f.CommandeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(f => f.AuteurId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(f => f.Messages).WithOne(m => m.Fil).HasForeignKey(m => m.FilId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.Texte).HasMaxLength(4000).IsRequired();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.AuteurId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SimulationConfiguration : IEntityTypeConfiguration<Simulation>
{
    public void Configure(EntityTypeBuilder<Simulation> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Titre).HasMaxLength(120);
        b.Property(s => s.Source).HasMaxLength(40).IsRequired();
        b.Property(s => s.Destination).HasMaxLength(60).IsRequired();
        b.Property(s => s.Protocole).HasConversion<string>().HasMaxLength(12);
        b.Property(s => s.Verdict).HasConversion<string>().HasMaxLength(12);
        b.Property(s => s.Regle).HasMaxLength(60);
        b.Property(s => s.Chemin).HasMaxLength(500);
        b.Property(s => s.EtatReseauCode).HasMaxLength(1);
        b.HasOne<EtatReseau>().WithMany().HasForeignKey(s => s.EtatReseauCode).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UtilisateurId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(s => new { s.PourDemo, s.OrdreDemo });
    }
}
