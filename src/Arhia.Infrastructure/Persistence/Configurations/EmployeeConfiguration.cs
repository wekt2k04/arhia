using Arhia.Domain.Entities;
using Arhia.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arhia.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EmployeeNumber)
            .HasConversion(n => n.Value, v => new EmployeeNumber(v))
            .HasMaxLength(20)
            .IsRequired();
        builder.HasIndex(e => e.EmployeeNumber).IsUnique();
        builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.JobTitle).IsRequired().HasMaxLength(150);
        builder.Property(e => e.ContractType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.DepartureDate);
        builder.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.SetNull);
    }
}
