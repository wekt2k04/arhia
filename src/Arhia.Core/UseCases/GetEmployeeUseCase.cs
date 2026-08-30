using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Domain.Entities;

namespace Arhia.Core.UseCases;

public sealed class GetEmployeeUseCase
{
    private readonly IEmployeeRepository _employees;

    public GetEmployeeUseCase(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<Employee> ExecuteAsync(Arhia.Domain.Entities.UserAccount actor, Guid employeeId, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.EmployeeRead))
            throw new AccessDeniedException("Vous n'avez pas accès à la lecture des fiches collaborateur.");

        var employee = await _employees.GetByIdAsync(employeeId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {employeeId} introuvable.");

        if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
            throw new AccessDeniedException("Vous n'avez pas accès à la fiche de ce collaborateur.");

        return employee;
    }
}
