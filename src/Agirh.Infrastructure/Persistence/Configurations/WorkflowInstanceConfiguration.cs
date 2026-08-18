using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agirh.Infrastructure.Persistence.Configurations;

public class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("WorkflowInstances");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.TemplateVersion).IsRequired().HasMaxLength(20);
        builder.Property(i => i.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Statut).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.DateCreation).IsRequired();
        builder.Property(i => i.DateCloture);
        builder.HasOne<Employee>().WithMany().HasForeignKey(i => i.CollaborateurId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowTemplate>().WithMany().HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(i => i.Items, item =>
        {
            item.ToTable("ChecklistItemStatuses");
            item.WithOwner().HasForeignKey("WorkflowInstanceId");
            item.HasKey(x => x.Id);
            item.Property(x => x.TemplateItemId).IsRequired();
            item.Property(x => x.Libelle).IsRequired().HasMaxLength(300);
            item.Property(x => x.Etat).HasConversion<string>().HasMaxLength(20).IsRequired();
            item.Property(x => x.Commentaire).HasMaxLength(1000);
            item.Property(x => x.CochePar);
            item.Property(x => x.DateCoche);
        });

        builder.Navigation(i => i.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
