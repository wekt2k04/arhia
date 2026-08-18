using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Infrastructure.Persistence.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AgirhDbContext _db;

    public EmployeeRepository(AgirhDbContext db)
    {
        _db = db;
    }

    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<Employee?> GetByEmployeeNumberAsync(EmployeeNumber employeeNumber, CancellationToken ct = default) =>
        await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeNumber == employeeNumber, ct);

    public async Task<Employee?> GetByUserAccountIdAsync(Guid userAccountId, CancellationToken ct = default) =>
        await _db.Employees.FirstOrDefaultAsync(e => e.UserAccountId == userAccountId, ct);

    public async Task<IReadOnlyList<Employee>> ListByDepartmentAsync(Guid departmentId, CancellationToken ct = default) =>
        await _db.Employees.Where(e => e.DepartmentId == departmentId).ToListAsync(ct);

    public async Task AddAsync(Employee employee, CancellationToken ct = default)
    {
        await _db.Employees.AddAsync(employee, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Employee employee, CancellationToken ct = default)
    {
        _db.Employees.Update(employee);
        await _db.SaveChangesAsync(ct);
    }
}
