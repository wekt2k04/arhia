using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Domain.Entities;

namespace Arhia.Core.UseCases;

public sealed class CloseCaseUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IEmployeeRepository _employees;

    public CloseCaseUseCase(IWorkflowInstanceRepository instances, IEmployeeRepository employees)
    {
        _instances = instances;
        _employees = employees;
    }

    public async Task ExecuteAsync(UserAccount actor, Guid workflowInstanceId, DateTime closureDate, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.WorkflowInstanceClose))
            throw new AccessDeniedException("Seul un RH peut clôturer un dossier.");

        var instance = await _instances.GetByIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowInstanceId} introuvable.");

        var employee = await _employees.GetByIdAsync(instance.EmployeeId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {instance.EmployeeId} introuvable.");

        if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
            throw new AccessDeniedException("Un RH ne peut clôturer un dossier que pour un collaborateur de son pôle.");

        instance.Close(closureDate);
        await _instances.UpdateAsync(instance, ct);
    }
}
