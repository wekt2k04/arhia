using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Domain;
using Arhia.Domain.Entities;

namespace Arhia.Core.UseCases;

public sealed class ListWorkflowInstancesUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IEmployeeRepository _employees;

    public ListWorkflowInstancesUseCase(IWorkflowInstanceRepository instances, IEmployeeRepository employees)
    {
        _instances = instances;
        _employees = employees;
    }

    public async Task<IReadOnlyList<WorkflowInstanceWithEmployee>> ExecuteAsync(
        Arhia.Domain.Entities.UserAccount actor, Guid? employeeId, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.WorkflowInstanceRead))
            throw new AccessDeniedException("Vous n'avez pas accès à la lecture des dossiers.");

        if (employeeId is { } targetId)
        {
            var target = await _employees.GetByIdAsync(targetId, ct)
                ?? throw new InvalidOperationException($"Collaborateur {targetId} introuvable.");
            if (!DepartmentScopeGuard.CanAccessEmployee(actor, target))
                throw new AccessDeniedException("Vous n'avez pas accès aux dossiers de ce collaborateur.");

            var own = await _instances.ListByEmployeeAsync(targetId, ct);
            return own.Select(i => new WorkflowInstanceWithEmployee(i, target)).ToList();
        }

        return actor.Role switch
        {
            RoleType.QualityAdmin => Merge(await _instances.ListAllAsync(ct), await _employees.ListAllAsync(ct)),
            RoleType.HR => Merge(
                await _instances.ListByDepartmentAsync(actor.DepartmentId!.Value, ct),
                await _employees.ListByDepartmentAsync(actor.DepartmentId!.Value, ct)),
            RoleType.Employee => await ListOwnAsync(actor, ct),
            _ => Array.Empty<WorkflowInstanceWithEmployee>()
        };
    }

    private async Task<IReadOnlyList<WorkflowInstanceWithEmployee>> ListOwnAsync(Arhia.Domain.Entities.UserAccount actor, CancellationToken ct)
    {
        var own = await _employees.GetByUserAccountIdAsync(actor.Id, ct);
        if (own is null) return Array.Empty<WorkflowInstanceWithEmployee>();
        var instances = await _instances.ListByEmployeeAsync(own.Id, ct);
        return instances.Select(i => new WorkflowInstanceWithEmployee(i, own)).ToList();
    }

    private static IReadOnlyList<WorkflowInstanceWithEmployee> Merge(IReadOnlyList<WorkflowInstance> instances, IReadOnlyList<Employee> employees)
    {
        var byId = employees.ToDictionary(e => e.Id);
        return instances
            .Where(i => byId.ContainsKey(i.EmployeeId))
            .Select(i => new WorkflowInstanceWithEmployee(i, byId[i.EmployeeId]))
            .ToList();
    }
}
