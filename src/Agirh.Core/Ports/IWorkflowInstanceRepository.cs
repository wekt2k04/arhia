using System.Threading;
using System.Threading.Tasks;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.Ports;

public interface IWorkflowInstanceRepository
{
    Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByEmployeeAsync(Guid employeeId, WorkflowType type, CancellationToken ct = default);
    Task AddAsync(WorkflowInstance instance, CancellationToken ct = default);
    Task UpdateAsync(WorkflowInstance instance, CancellationToken ct = default);
}
