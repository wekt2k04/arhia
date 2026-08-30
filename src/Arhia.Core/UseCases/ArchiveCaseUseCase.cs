using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;

namespace Arhia.Core.UseCases;

public sealed class ArchiveCaseUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IEmployeeRepository _employees;

    public ArchiveCaseUseCase(IWorkflowInstanceRepository instances, IEmployeeRepository employees)
    {
        _instances = instances;
        _employees = employees;
    }

    public async Task ExecuteAsync(Domain.Entities.UserAccount actor, Guid workflowInstanceId, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.WorkflowInstanceArchive))
            throw new AccessDeniedException("Seuls RH et Admin/Qualité peuvent archiver un dossier.");

        var instance = await _instances.GetByIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowInstanceId} introuvable.");

        var employee = await _employees.GetByIdAsync(instance.EmployeeId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {instance.EmployeeId} introuvable.");

        if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
            throw new AccessDeniedException("Un RH ne peut archiver un dossier que pour un collaborateur de son pôle.");

        instance.Archive();
        await _instances.UpdateAsync(instance, ct);
    }
}
