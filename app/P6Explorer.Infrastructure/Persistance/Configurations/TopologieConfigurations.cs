using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using P6Explorer.Domain.Topologie;

namespace P6Explorer.Infrastructure.Persistance.Configurations;

public class EtatReseauConfiguration : IEntityTypeConfiguration<EtatReseau>
{
    public void Configure(EntityTypeBuilder<EtatReseau> b)
    {
        b.HasKey(e => e.Code);
        b.Property(e => e.Code).HasMaxLength(1);
        b.Property(e => e.Libelle).HasMaxLength(100).IsRequired();
        b.Property(e => e.Description).HasMaxLength(1000);
        b.HasMany(e => e.Reseaux).WithOne(r => r.EtatReseau).HasForeignKey(r => r.EtatReseauCode).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.Regles).WithOne().HasForeignKey(r => r.EtatReseauCode).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.ReglesNat).WithOne().HasForeignKey(r => r.EtatReseauCode).OnDelete(DeleteBehavior.Cascade);
    }
}

public class NoeudConfiguration : IEntityTypeConfiguration<Noeud>
{
    public void Configure(EntityTypeBuilder<Noeud> b)
    {
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).HasConversion(Convertisseurs.Noeud).HasMaxLength(40);
        b.Property(n => n.Hote).HasConversion(Convertisseurs.Noeud).HasMaxLength(40);
        b.Property(n => n.Nom).HasMaxLength(60).IsRequired();
        b.Property(n => n.SousTitre).HasMaxLength(120);
        b.Property(n => n.Type).HasConversion<string>().HasMaxLength(12);
        b.Property(n => n.Zone).HasConversion<string>().HasMaxLength(12);
        b.Property(n => n.Description).HasMaxLength(1000);
        b.Property(n => n.Passerelle).HasMaxLength(45);
        b.HasMany(n => n.Interfaces).WithOne(i => i.Noeud).HasForeignKey(i => i.NoeudId).OnDelete(DeleteBehavior.Cascade);
        // Ports en écoute : petite liste lue d'un bloc par le moteur réseau, rangée en JSON dans la colonne Services.
        b.OwnsMany(n => n.Services, s =>
        {
            s.ToJson();
            s.Property(x => x.Nom).HasMaxLength(80);
            s.Property(x => x.Proto).HasMaxLength(3);
            s.Property(x => x.Adresse).HasMaxLength(45);
            s.Property(x => x.Description).HasMaxLength(200);
        });
    }
}

public class InterfaceConfiguration : IEntityTypeConfiguration<Interface>
{
    public void Configure(EntityTypeBuilder<Interface> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.NoeudId).HasConversion(Convertisseurs.Noeud).HasMaxLength(40);
        b.Property(i => i.Nom).HasMaxLength(30).IsRequired();
        b.Property(i => i.Description).HasMaxLength(200);
        b.Property(i => i.ReseauCode).HasMaxLength(30);
        b.Property(i => i.Affectation).HasMaxLength(20);
        b.Property(i => i.EtatReseauCode).HasMaxLength(1);
        // Type owned : adresse + préfixe, rangés dans la table Interfaces (Adresse_Adresse, Adresse_Prefixe).
        b.OwnsOne(i => i.Adresse, a =>
        {
            a.Property(x => x.Adresse).HasColumnName("Adresse").HasMaxLength(45);
            a.Property(x => x.Prefixe).HasColumnName("Prefixe");
        });
        b.HasOne(i => i.Reseau).WithMany(r => r.Interfaces).HasForeignKey(i => i.ReseauId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(i => new { i.NoeudId, i.Nom, i.EtatReseauCode });
    }
}

public class ReseauConfiguration : IEntityTypeConfiguration<Reseau>
{
    public void Configure(EntityTypeBuilder<Reseau> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(r => r.Code).IsUnique();
        b.Property(r => r.Nom).HasMaxLength(120).IsRequired();
        b.Property(r => r.Zone).HasConversion<string>().HasMaxLength(12);
        b.Property(r => r.Passerelle).HasMaxLength(45);
        b.Property(r => r.EtatReseauCode).HasMaxLength(1);
        b.OwnsOne(r => r.Prefixe, a =>
        {
            a.Property(x => x.Adresse).HasColumnName("Prefixe_Adresse").HasMaxLength(45);
            a.Property(x => x.Prefixe).HasColumnName("Prefixe_Longueur");
        });
    }
}

public class LienConfiguration : IEntityTypeConfiguration<Lien>
{
    public void Configure(EntityTypeBuilder<Lien> b)
    {
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasConversion(Convertisseurs.Lien).HasMaxLength(40);
        b.Property(l => l.De).HasConversion(Convertisseurs.Noeud).HasMaxLength(40);
        b.Property(l => l.Vers).HasConversion(Convertisseurs.Noeud).HasMaxLength(40);
        b.Property(l => l.Zone).HasConversion<string>().HasMaxLength(12);
        b.Property(l => l.Libelle).HasMaxLength(100);
        b.Property(l => l.EtatReseauCode).HasMaxLength(1);
        b.Property(l => l.Points).HasConversion(Convertisseurs.Points, Convertisseurs.PointsComparateur).HasMaxLength(1000);
    }
}

public class RegleConfiguration : IEntityTypeConfiguration<Regle>
{
    public void Configure(EntityTypeBuilder<Regle> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.EtatReseauCode).HasMaxLength(1);
        b.Property(r => r.Equipement).HasConversion(Convertisseurs.Noeud).HasMaxLength(40);
        b.Property(r => r.Code).HasMaxLength(40).IsRequired();
        b.Property(r => r.Interface).HasMaxLength(20).IsRequired();
        b.Property(r => r.Action).HasConversion<string>().HasMaxLength(6);
        b.Property(r => r.Proto).HasMaxLength(10);
        b.Property(r => r.Source).HasMaxLength(100);
        b.Property(r => r.Destination).HasMaxLength(100);
        b.Property(r => r.Ports).HasMaxLength(60);
        b.Property(r => r.Description).HasMaxLength(300);
        // Première correspondance : l'ordre des règles d'une interface compte (index utilisé par le moteur).
        b.HasIndex(r => new { r.EtatReseauCode, r.Equipement, r.Interface, r.Ordre });
    }
}

public class RegleNatConfiguration : IEntityTypeConfiguration<RegleNat>
{
    public void Configure(EntityTypeBuilder<RegleNat> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.EtatReseauCode).HasMaxLength(1);
        b.Property(r => r.Code).HasMaxLength(20).IsRequired();
        b.Property(r => r.Interface).HasMaxLength(20);
        b.Property(r => r.Proto).HasMaxLength(10);
        b.Property(r => r.AdressePublique).HasMaxLength(45);
        b.Property(r => r.PortPublic).HasMaxLength(20);
        b.Property(r => r.Cible).HasMaxLength(45);
        b.Property(r => r.PortCible).HasMaxLength(20);
        b.Property(r => r.SourceAutorisee).HasMaxLength(45);
        b.Property(r => r.Filtrage).HasConversion<string>().HasMaxLength(8);
        b.Property(r => r.Description).HasMaxLength(300);
    }
}
