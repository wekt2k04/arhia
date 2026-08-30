using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;

namespace Arhia.Core.UseCases;

public sealed class GetWorkflowInstanceUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IEmployeeRepository _employees;
    private readonly IWorkflowTemplateRepository _templates;

    public GetWorkflowInstanceUseCase(
        IWorkflowInstanceRepository instances, IEmployeeRepository employees, IWorkflowTemplateRepository templates)
    {
        _instances = instances;
        _employees = employees;
        _templates = templates;
    }

    public async Task<WorkflowInstanceDetail> ExecuteAsync(Arhia.Domain.Entities.UserAccount actor, Guid workflowInstanceId, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.WorkflowInstanceRead))
            throw new AccessDeniedException("Vous n'avez pas accès à la lecture des dossiers.");

        var instance = await _instances.GetByIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowInstanceId} introuvable.");

        var employee = await _employees.GetByIdAsync(instance.EmployeeId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {instance.EmployeeId} introuvable.");

        if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
            throw new AccessDeniedException("Vous n'avez pas accès à ce dossier.");

        var template = await _templates.GetByIdAsync(instance.TemplateId, ct);
        return new WorkflowInstanceDetail(instance, employee, template);
    }
}
