using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.Security;

public static class DepartmentScopeGuard
{
    public static bool CanAccessDepartment(UserAccount actor, Guid targetDepartmentId)
    {
        return actor.Role switch
        {
            RoleType.QualityAdmin => true,
            RoleType.HR => actor.DepartmentId == targetDepartmentId,
            _ => false
        };
    }

    public static bool CanAccessEmployee(UserAccount actor, Employee target)
    {
        return actor.Role switch
        {
            RoleType.QualityAdmin => true,
            RoleType.HR => actor.DepartmentId == target.DepartmentId,
            RoleType.Employee => actor.Id == target.UserAccountId,
            _ => false
        };
    }
}
