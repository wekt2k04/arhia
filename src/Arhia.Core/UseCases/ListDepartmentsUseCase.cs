using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Domain.Entities;

namespace Arhia.Core.UseCases;

public sealed class ListDepartmentsUseCase
{
    private readonly IDepartmentRepository _departments;

    public ListDepartmentsUseCase(IDepartmentRepository departments)
    {
        _departments = departments;
    }

    public async Task<IReadOnlyList<Department>> ExecuteAsync(Arhia.Domain.Entities.UserAccount actor, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.DepartmentRead))
            throw new AccessDeniedException("Vous n'avez pas accès à la liste des pôles.");

        return await _departments.ListAllAsync(ct);
    }
}
