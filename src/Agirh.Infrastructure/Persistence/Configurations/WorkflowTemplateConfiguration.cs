using System.Linq;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agirh.Infrastructure.Persistence.Configurations;

public class WorkflowTemplateConfiguration : IEntityTypeConfiguration<WorkflowTemplate>
{
    public void Configure(EntityTypeBuilder<WorkflowTemplate> builder)
    {
        builder.ToTable("WorkflowTemplates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.Version).IsRequired().HasMaxLength(20);
        builder.Property(t => t.Statut).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.RedacteurId).IsRequired();
        builder.Property(t => t.VerificateurId);
        builder.Property(t => t.ApprobateurId);
        builder.Property(t => t.MotifRejet).HasMaxLength(500);
        builder.Property(t => t.DateCreation).IsRequired();
        builder.HasIndex(t => new { t.Type, t.Version }).IsUnique();

        builder.OwnsMany(t => t.Sections, section =>
        {
            section.ToTable("TemplateSections");
            section.WithOwner().HasForeignKey("WorkflowTemplateId");
            section.HasKey(s => s.Id);
            section.Property(s => s.Nom).IsRequired().HasMaxLength(100);
            section.Property(s => s.Ordre).IsRequired();

            section.OwnsMany(s => s.Items, item =>
            {
                item.ToTable("TemplateItems");
                item.WithOwner().HasForeignKey("TemplateSectionId");
                item.HasKey(i => i.Id);
                item.Property(i => i.Libelle).IsRequired().HasMaxLength(300);
                item.Property(i => i.Ordre).IsRequired();

                var conditionsComparer = new ValueComparer<IReadOnlyCollection<TypeContrat>>(
                    (a, b) => a!.SequenceEqual(b!),
                    c => c.Aggregate(0, (hash, v) => HashCode.Combine(hash, v)),
                    c => c.ToList());

                item.Property(i => i.ConditionsTypeContrat)
                    .HasConversion(
                        list => string.Join(',', list.Select(c => c.ToString())),
                        text => string.IsNullOrEmpty(text)
                            ? new List<TypeContrat>()
                            : text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<TypeContrat>).ToList())
                    .Metadata.SetValueComparer(conditionsComparer);
            });

            section.Navigation(s => s.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Navigation(t => t.Sections).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
