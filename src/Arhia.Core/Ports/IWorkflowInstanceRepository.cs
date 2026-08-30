using System.Threading;
using System.Threading.Tasks;
using Arhia.Domain;
using Arhia.Domain.Entities;

namespace Arhia.Core.Ports;

public interface IWorkflowInstanceRepository
{
    Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByEmployeeAsync(Guid employeeId, WorkflowType type, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> ListByEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> ListByDepartmentAsync(Guid departmentId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(WorkflowInstance instance, CancellationToken ct = default);
    Task UpdateAsync(WorkflowInstance instance, CancellationToken ct = default);
}
