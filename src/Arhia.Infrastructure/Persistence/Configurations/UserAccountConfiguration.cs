using Arhia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arhia.Infrastructure.Persistence.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("UserAccounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(a => a.Email).IsUnique();
        builder.Property(a => a.PasswordHash).IsRequired();
        builder.Property(a => a.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.HasOne<Department>().WithMany().HasForeignKey(a => a.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
