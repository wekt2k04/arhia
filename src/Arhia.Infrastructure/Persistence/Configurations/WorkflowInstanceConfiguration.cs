using Arhia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arhia.Infrastructure.Persistence.Configurations;

public class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("WorkflowInstances");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.TemplateVersion).IsRequired().HasMaxLength(20);
        builder.Property(i => i.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.ClosureDate);
        builder.HasOne<Employee>().WithMany().HasForeignKey(i => i.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowTemplate>().WithMany().HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(i => i.Items, item =>
        {
            item.ToTable("ChecklistItemStatuses");
            item.WithOwner().HasForeignKey("WorkflowInstanceId");
            item.HasKey(x => x.Id);
            item.Property(x => x.TemplateItemId).IsRequired();
            item.Property(x => x.Label).IsRequired().HasMaxLength(300);
            item.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            item.Property(x => x.Comment).HasMaxLength(1000);
            item.Property(x => x.CheckedBy);
            item.Property(x => x.CheckedDate);
        });

        builder.Navigation(i => i.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
