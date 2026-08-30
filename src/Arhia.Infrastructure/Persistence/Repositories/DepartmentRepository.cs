using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Arhia.Infrastructure.Persistence.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly ArhiaDbContext _db;

    public DepartmentRepository(ArhiaDbContext db)
    {
        _db = db;
    }

    public async Task<Department?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Department>> ListAllAsync(CancellationToken ct = default) =>
        await _db.Departments.ToListAsync(ct);

    public async Task AddAsync(Department department, CancellationToken ct = default)
    {
        await _db.Departments.AddAsync(department, ct);
        await _db.SaveChangesAsync(ct);
    }
}
