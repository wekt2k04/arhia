using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class InstantiateWorkflowUseCase
{
    private readonly IEmployeeRepository _employees;
    private readonly IWorkflowTemplateRepository _templates;
    private readonly IWorkflowInstanceRepository _instances;

    public InstantiateWorkflowUseCase(
        IEmployeeRepository employees,
        IWorkflowTemplateRepository templates,
        IWorkflowInstanceRepository instances)
    {
        _employees = employees;
        _templates = templates;
        _instances = instances;
    }

    public async Task<WorkflowInstance> ExecuteAsync(
        UserAccount actor,
        Guid employeeId,
        WorkflowType type,
        DateTime createdAt,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.WorkflowInstantiate))
            throw new AccessDeniedException("Seul un RH peut instancier un workflow.");

        var employee = await _employees.GetByIdAsync(employeeId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {employeeId} introuvable.");

        if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
            throw new AccessDeniedException("Un RH ne peut instancier un workflow que pour un collaborateur de son pôle.");

        var template = await _templates.GetLastApprovedAsync(type, ct)
            ?? throw new InvalidOperationException($"Aucun template approuvé pour le type {type}.");

        var applicableItems = template.ResolveApplicableItems(employee.ContractType);
        var items = applicableItems
            .Select(i => new ChecklistItemStatus(Guid.NewGuid(), i.Id, i.Label))
            .ToList();

        var instance = new WorkflowInstance(
            Guid.NewGuid(), employee.Id, template.Id, template.Version, type, items, createdAt);

        await _instances.AddAsync(instance, ct);
        return instance;
    }
}
