using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Infrastructure.Persistence;

public class AgirhDbContext : DbContext
{
    public AgirhDbContext(DbContextOptions<AgirhDbContext> options) : base(options)
    {
    }

    public DbSet<Pole> Poles => Set<Pole>();
    public DbSet<CompteUtilisateur> ComptesUtilisateurs => Set<CompteUtilisateur>();
    public DbSet<Collaborateur> Collaborateurs => Set<Collaborateur>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgirhDbContext).Assembly);
    }
}
