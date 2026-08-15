using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agirh.Infrastructure.Persistence.Configurations;

public class CompteUtilisateurConfiguration : IEntityTypeConfiguration<CompteUtilisateur>
{
    public void Configure(EntityTypeBuilder<CompteUtilisateur> builder)
    {
        builder.ToTable("ComptesUtilisateurs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(c => c.Email).IsUnique();
        builder.Property(c => c.PasswordHash).IsRequired();
        builder.Property(c => c.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.EstActif).IsRequired();
        builder.Property(c => c.DateCreation).IsRequired();
        builder.HasOne<Pole>().WithMany().HasForeignKey(c => c.PoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
