using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Infrastructure.Persistence.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly AgirhDbContext _db;

    public DepartmentRepository(AgirhDbContext db)
    {
        _db = db;
    }

    public async Task<Department?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task AddAsync(Department department, CancellationToken ct = default)
    {
        await _db.Departments.AddAsync(department, ct);
        await _db.SaveChangesAsync(ct);
    }
}
