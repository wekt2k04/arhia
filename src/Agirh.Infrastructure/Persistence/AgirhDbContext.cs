using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Infrastructure.Persistence;

public class AgirhDbContext : DbContext
{
    public AgirhDbContext(DbContextOptions<AgirhDbContext> options) : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgirhDbContext).Assembly);
    }
}
