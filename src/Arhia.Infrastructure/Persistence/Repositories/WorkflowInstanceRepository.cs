using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Domain;
using Arhia.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Arhia.Infrastructure.Persistence.Repositories;

public class WorkflowInstanceRepository : IWorkflowInstanceRepository
{
    private readonly ArhiaDbContext _db;

    public WorkflowInstanceRepository(ArhiaDbContext db)
    {
        _db = db;
    }

    public async Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.WorkflowInstances.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<WorkflowInstance?> GetByEmployeeAsync(Guid employeeId, WorkflowType type, CancellationToken ct = default) =>
        await _db.WorkflowInstances
            .Include(i => i.Items)
            .Where(i => i.EmployeeId == employeeId && i.Type == type)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<WorkflowInstance>> ListByEmployeeAsync(Guid employeeId, CancellationToken ct = default) =>
        await _db.WorkflowInstances
            .Include(i => i.Items)
            .Where(i => i.EmployeeId == employeeId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WorkflowInstance>> ListByDepartmentAsync(Guid departmentId, CancellationToken ct = default) =>
        await _db.WorkflowInstances
            .Include(i => i.Items)
            .Join(_db.Employees.Where(e => e.DepartmentId == departmentId),
                  instance => instance.EmployeeId, employee => employee.Id,
                  (instance, employee) => instance)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WorkflowInstance>> ListAllAsync(CancellationToken ct = default) =>
        await _db.WorkflowInstances
            .Include(i => i.Items)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(WorkflowInstance instance, CancellationToken ct = default)
    {
        await _db.WorkflowInstances.AddAsync(instance, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WorkflowInstance instance, CancellationToken ct = default)
    {
        _db.WorkflowInstances.Update(instance);
        await _db.SaveChangesAsync(ct);
    }
}
