using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agirh.Infrastructure.Persistence.Configurations;

public class PoleConfiguration : IEntityTypeConfiguration<Pole>
{
    public void Configure(EntityTypeBuilder<Pole> builder)
    {
        builder.ToTable("Poles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nom).IsRequired().HasMaxLength(100);
        builder.HasIndex(p => p.Nom).IsUnique();
    }
}
