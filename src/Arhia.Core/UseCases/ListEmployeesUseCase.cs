using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Domain;
using Arhia.Domain.Entities;

namespace Arhia.Core.UseCases;

public sealed class ListEmployeesUseCase
{
    private readonly IEmployeeRepository _employees;

    public ListEmployeesUseCase(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<IReadOnlyList<Employee>> ExecuteAsync(Arhia.Domain.Entities.UserAccount actor, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.EmployeeRead))
            throw new AccessDeniedException("Vous n'avez pas accès à la lecture des fiches collaborateur.");

        return actor.Role switch
        {
            RoleType.QualityAdmin => await _employees.ListAllAsync(ct),
            RoleType.HR => await _employees.ListByDepartmentAsync(actor.DepartmentId!.Value, ct),
            RoleType.Employee => await ResolveOwnRecordAsync(actor, ct),
            _ => Array.Empty<Employee>()
        };
    }

    private async Task<IReadOnlyList<Employee>> ResolveOwnRecordAsync(Arhia.Domain.Entities.UserAccount actor, CancellationToken ct)
    {
        var own = await _employees.GetByUserAccountIdAsync(actor.Id, ct);
        return own is null ? Array.Empty<Employee>() : new[] { own };
    }
}
