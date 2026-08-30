using System.Threading;
using System.Threading.Tasks;
using Arhia.Domain;
using Arhia.Domain.Entities;

namespace Arhia.Core.Ports;

public interface IWorkflowTemplateRepository
{
    Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowTemplate?> GetLastApprovedAsync(WorkflowType type, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTemplate>> ListByStatusAsync(TemplateStatus status, CancellationToken ct = default);
    Task AddAsync(WorkflowTemplate template, CancellationToken ct = default);
    Task UpdateAsync(WorkflowTemplate template, CancellationToken ct = default);
}
