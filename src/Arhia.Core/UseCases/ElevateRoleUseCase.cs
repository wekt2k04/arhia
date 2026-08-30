using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Domain;
using Arhia.Domain.Entities;

namespace Arhia.Core.UseCases;

public sealed class ElevateRoleUseCase
{
    private readonly IUserAccountRepository _accounts;

    public ElevateRoleUseCase(IUserAccountRepository accounts)
    {
        _accounts = accounts;
    }

    public async Task<UserAccount> ExecuteAsync(
        UserAccount actor,
        Guid targetAccountId,
        RoleType newRole,
        Guid? newDepartmentId,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.UserAccountElevateRole))
            throw new AccessDeniedException("Seul un compte Admin/Qualité peut élever un rôle.");

        var target = await _accounts.GetByIdAsync(targetAccountId, ct)
            ?? throw new InvalidOperationException($"Compte {targetAccountId} introuvable.");

        target.ElevateRole(newRole, newDepartmentId);
        await _accounts.UpdateAsync(target, ct);
        return target;
    }
}
