using Arhia.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Arhia.Infrastructure.Persistence;

public class ArhiaDbContext : DbContext
{
    public ArhiaDbContext(DbContextOptions<ArhiaDbContext> options) : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArhiaDbContext).Assembly);
    }
}
