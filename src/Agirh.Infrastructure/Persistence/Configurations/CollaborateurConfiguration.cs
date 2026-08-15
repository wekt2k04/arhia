using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agirh.Infrastructure.Persistence.Configurations;

public class CollaborateurConfiguration : IEntityTypeConfiguration<Collaborateur>
{
    public void Configure(EntityTypeBuilder<Collaborateur> builder)
    {
        builder.ToTable("Collaborateurs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Matricule)
            .HasConversion(m => m.Valeur, v => new Matricule(v))
            .HasMaxLength(20)
            .IsRequired();
        builder.HasIndex(c => c.Matricule).IsUnique();
        builder.Property(c => c.Nom).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Prenom).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Poste).IsRequired().HasMaxLength(150);
        builder.Property(c => c.TypeContrat).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.DateIntegration).IsRequired();
        builder.Property(c => c.DateDepart);
        builder.HasOne<Pole>().WithMany().HasForeignKey(c => c.PoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompteUtilisateur>().WithMany().HasForeignKey(c => c.CompteUtilisateurId).OnDelete(DeleteBehavior.SetNull);
    }
}
