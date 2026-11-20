using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using P6Explorer.Domain.Commandes;

namespace P6Explorer.Infrastructure.Persistance.Configurations;

/// <summary>
/// Héritage TPH : tous les effets dans une seule table « Effets », avec une colonne discriminante « Type »
/// (mêmes valeurs que le discriminateur JSON). Les propriétés de même nom et de même type des types
/// dérivés partagent une colonne (Noeud, De, Vers, Nom…), ce qui garde la table compacte.
/// </summary>
public class EffetConfiguration : IEntityTypeConfiguration<Effet>
{
    public void Configure(EntityTypeBuilder<Effet> b)
    {
        b.ToTable("Effets");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.CommandeId, e.Ordre });
        b.HasDiscriminator<string>("Type")
            .HasValue<EtatNoeud>("etatNoeud")
            .HasValue<EtatLien>("etatLien")
            .HasValue<AttribuerIp>("attribuerIp")
            .HasValue<Paquet>("paquet")
            .HasValue<ChangementRegle>("changementRegle")
            .HasValue<Service>("service")
            .HasValue<Fichier>("fichier")
            .HasValue<Instantane>("instantane")
            .HasValue<Journal>("journal")
            .HasValue<AlerteIds>("alerteIds")
            .HasValue<ResultatTest>("resultatTest")
            .HasValue<Surbrillance>("surbrillance")
            .HasValue<ChangementEtat>("changementEtat")
            .HasValue<ConteneurReseau>("conteneurReseau");
        b.Property<string>("Type").HasMaxLength(20);
    }
}

public class EtatNoeudConfiguration : IEntityTypeConfiguration<EtatNoeud>
{
    public void Configure(EntityTypeBuilder<EtatNoeud> b)
    {
        b.Property(e => e.Noeud).HasConversion(Convertisseurs.Noeud).HasColumnName("Noeud").HasMaxLength(40);
        b.Property(e => e.Etat).HasConversion<string>().HasColumnName("EtatVm").HasMaxLength(12);
    }
}

public class EtatLienConfiguration : IEntityTypeConfiguration<EtatLien>
{
    public void Configure(EntityTypeBuilder<EtatLien> b)
    {
        b.Property(e => e.Lien).HasConversion(Convertisseurs.Lien).HasColumnName("Lien").HasMaxLength(40);
        b.Property(e => e.Branche).HasColumnName("Branche");
    }
}

public class AttribuerIpConfiguration : IEntityTypeConfiguration<AttribuerIp>
{
    public void Configure(EntityTypeBuilder<AttribuerIp> b)
    {
        b.Property(e => e.Noeud).HasConversion(Convertisseurs.Noeud).HasColumnName("Noeud").HasMaxLength(40);
        b.Property(e => e.Carte).HasColumnName("Carte").HasMaxLength(30);
        b.Property(e => e.Ip).HasColumnName("Ip").HasMaxLength(45);
    }
}

public class PaquetConfiguration : IEntityTypeConfiguration<Paquet>
{
    public void Configure(EntityTypeBuilder<Paquet> b)
    {
        b.Property(e => e.De).HasConversion(Convertisseurs.Noeud).HasColumnName("De").HasMaxLength(40);
        b.Property(e => e.Vers).HasConversion(Convertisseurs.Noeud).HasColumnName("Vers").HasMaxLength(40);
        b.Property(e => e.Proto).HasConversion<string>().HasColumnName("Proto").HasMaxLength(12);
        b.Property(e => e.Port).HasColumnName("Port");
        b.Property(e => e.AdresseVisee).HasColumnName("AdresseVisee").HasMaxLength(60);
        b.Property(e => e.Chemin).HasConversion(Convertisseurs.Chemin, Convertisseurs.CheminComparateur).HasColumnName("Chemin").HasMaxLength(500);
        b.Property(e => e.Verdict).HasConversion<string>().HasColumnName("Verdict").HasMaxLength(12);
        b.Property(e => e.Regle).HasColumnName("Regle").HasMaxLength(60);
        b.Property(e => e.Vlan).HasColumnName("Vlan");
        // Type owned : la traduction NAT est rangée dans la même ligne (colonnes Nat_…).
        b.OwnsOne(e => e.Nat, n =>
        {
            n.Property(x => x.Regle).HasColumnName("Nat_Regle").HasMaxLength(20);
            n.Property(x => x.AdresseAvant).HasColumnName("Nat_AdresseAvant").HasMaxLength(45);
            n.Property(x => x.PortAvant).HasColumnName("Nat_PortAvant");
            n.Property(x => x.AdresseApres).HasColumnName("Nat_AdresseApres").HasMaxLength(45);
            n.Property(x => x.PortApres).HasColumnName("Nat_PortApres");
        });
    }
}

public class ChangementRegleConfiguration : IEntityTypeConfiguration<ChangementRegle>
{
    public void Configure(EntityTypeBuilder<ChangementRegle> b)
    {
        b.Property(e => e.Action).HasConversion<string>().HasColumnName("ActionRegle").HasMaxLength(12);
        b.Property(e => e.IdRegle).HasColumnName("IdRegle").HasMaxLength(60);
        b.Property(e => e.Resume).HasColumnName("Resume").HasMaxLength(500);
    }
}

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> b)
    {
        b.Property(e => e.Noeud).HasConversion(Convertisseurs.Noeud).HasColumnName("Noeud").HasMaxLength(40);
        b.Property(e => e.Nom).HasColumnName("Nom").HasMaxLength(100);
        b.Property(e => e.Demarre).HasColumnName("Demarre");
        b.Property(e => e.Port).HasColumnName("Port");
    }
}

public class FichierConfiguration : IEntityTypeConfiguration<Fichier>
{
    public void Configure(EntityTypeBuilder<Fichier> b)
    {
        b.Property(e => e.Noeud).HasConversion(Convertisseurs.Noeud).HasColumnName("Noeud").HasMaxLength(40);
        b.Property(e => e.Chemin).HasColumnName("CheminFichier").HasMaxLength(300);
        b.Property(e => e.Cree).HasColumnName("Cree");
    }
}

public class InstantaneConfiguration : IEntityTypeConfiguration<Instantane>
{
    public void Configure(EntityTypeBuilder<Instantane> b)
    {
        b.Property(e => e.Vm).HasConversion(Convertisseurs.Noeud).HasColumnName("Noeud").HasMaxLength(40);
        b.Property(e => e.Nom).HasColumnName("Nom").HasMaxLength(100);
        b.Property(e => e.Restauration).HasColumnName("Restauration");
    }
}

public class JournalConfiguration : IEntityTypeConfiguration<Journal>
{
    public void Configure(EntityTypeBuilder<Journal> b)
    {
        b.Property(e => e.De).HasConversion(Convertisseurs.Noeud).HasColumnName("De").HasMaxLength(40);
        b.Property(e => e.Ligne).HasColumnName("Ligne").HasMaxLength(500);
    }
}

public class AlerteIdsConfiguration : IEntityTypeConfiguration<AlerteIds>
{
    public void Configure(EntityTypeBuilder<AlerteIds> b)
    {
        b.Property(e => e.Signature).HasColumnName("Signature").HasMaxLength(200);
        b.Property(e => e.Blocage).HasColumnName("Blocage");
    }
}

public class ResultatTestConfiguration : IEntityTypeConfiguration<ResultatTest>
{
    public void Configure(EntityTypeBuilder<ResultatTest> b)
    {
        b.Property(e => e.Nom).HasColumnName("Nom").HasMaxLength(100);
        b.Property(e => e.Ok).HasColumnName("Ok");
        b.Property(e => e.Compteur).HasColumnName("Compteur").HasMaxLength(60);
    }
}

public class SurbrillanceConfiguration : IEntityTypeConfiguration<Surbrillance>
{
    public void Configure(EntityTypeBuilder<Surbrillance> b)
    {
        b.Property(e => e.Noeud).HasConversion(Convertisseurs.Noeud).HasColumnName("Noeud").HasMaxLength(40);
        b.Property(e => e.Fichier).HasColumnName("CheminFichier").HasMaxLength(300);
        b.Property(e => e.Texte).HasColumnName("Texte").HasMaxLength(1000);
    }
}

public class ChangementEtatConfiguration : IEntityTypeConfiguration<ChangementEtat>
{
    public void Configure(EntityTypeBuilder<ChangementEtat> b)
        => b.Property(e => e.Etat).HasColumnName("EtatReseau").HasMaxLength(1);
}

public class ConteneurReseauConfiguration : IEntityTypeConfiguration<ConteneurReseau>
{
    public void Configure(EntityTypeBuilder<ConteneurReseau> b)
    {
        b.Property(e => e.Conteneur).HasConversion(Convertisseurs.Noeud).HasColumnName("Noeud").HasMaxLength(40);
        b.Property(e => e.Reseau).HasColumnName("ReseauDocker").HasMaxLength(40);
        b.Property(e => e.Ip).HasColumnName("Ip").HasMaxLength(45);
    }
}
