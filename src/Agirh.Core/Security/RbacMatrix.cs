using Agirh.Domain;

namespace Agirh.Core.Security;

public static class RbacMatrix
{
    private static readonly IReadOnlyDictionary<ResourceAction, IReadOnlySet<RoleType>> Default =
        new Dictionary<ResourceAction, IReadOnlySet<RoleType>>
        {
            [ResourceAction.EmployeeCreate] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstantiate] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstanceRead] = Roles(RoleType.Employee, RoleType.HR, RoleType.QualityAdmin),
            [ResourceAction.WorkflowInstanceCheck] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstanceClose] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstanceArchive] = Roles(RoleType.HR, RoleType.QualityAdmin),
            [ResourceAction.TemplatePropose] = Roles(RoleType.HR),
            [ResourceAction.TemplateVerify] = Roles(RoleType.QualityAdmin),
            [ResourceAction.TemplateApprove] = Roles(RoleType.QualityAdmin),
            [ResourceAction.TemplateReject] = Roles(RoleType.QualityAdmin),
            [ResourceAction.UserAccountElevateRole] = Roles(RoleType.QualityAdmin),
            [ResourceAction.CorpusIngest] = Roles(RoleType.QualityAdmin)
        };

    public static bool IsAuthorized(RoleType role, ResourceAction action) =>
        Default.TryGetValue(action, out var authorizedRoles) && authorizedRoles.Contains(role);

    private static IReadOnlySet<RoleType> Roles(params RoleType[] roles) => roles.ToHashSet();
}
