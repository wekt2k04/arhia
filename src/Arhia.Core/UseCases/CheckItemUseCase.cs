using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Domain;
using Arhia.Domain.Entities;

namespace Arhia.Core.UseCases;

public sealed class CheckItemUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IEmployeeRepository _employees;

    public CheckItemUseCase(IWorkflowInstanceRepository instances, IEmployeeRepository employees)
    {
        _instances = instances;
        _employees = employees;
    }

    public async Task ExecuteAsync(
        UserAccount actor,
        Guid workflowInstanceId,
        Guid itemId,
        ItemStatus status,
        string? comment,
        DateTime checkedDate,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.WorkflowInstanceCheck))
            throw new AccessDeniedException("Seul un RH peut cocher un item de checklist.");

        var instance = await _instances.GetByIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowInstanceId} introuvable.");

        var employee = await _employees.GetByIdAsync(instance.EmployeeId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {instance.EmployeeId} introuvable.");

        if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
            throw new AccessDeniedException("Un RH ne peut cocher un item que pour un collaborateur de son pôle.");

        instance.Check(itemId, status, actor.Id, checkedDate, comment);
        await _instances.UpdateAsync(instance, ct);
    }
}
