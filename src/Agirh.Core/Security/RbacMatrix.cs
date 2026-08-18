using Agirh.Domain;

namespace Agirh.Core.Security;

public static class RbacMatrix
{
    private static readonly IReadOnlyDictionary<ResourceAction, IReadOnlySet<RoleType>> Default =
        new Dictionary<ResourceAction, IReadOnlySet<RoleType>>
        {
            [ResourceAction.EmployeeCreate] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstancier] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstanceLire] = Roles(RoleType.Employee, RoleType.HR, RoleType.QualityAdmin),
            [ResourceAction.WorkflowInstanceCocher] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstanceCloturer] = Roles(RoleType.HR),
            [ResourceAction.WorkflowInstanceArchiver] = Roles(RoleType.HR, RoleType.QualityAdmin),
            [ResourceAction.TemplateProposer] = Roles(RoleType.HR),
            [ResourceAction.TemplateVerifier] = Roles(RoleType.QualityAdmin),
            [ResourceAction.TemplateApprouver] = Roles(RoleType.QualityAdmin),
            [ResourceAction.TemplateRejeter] = Roles(RoleType.QualityAdmin),
            [ResourceAction.UserAccountElevateRole] = Roles(RoleType.QualityAdmin),
            [ResourceAction.CorpusIngerer] = Roles(RoleType.QualityAdmin)
        };

    public static bool IsAuthorized(RoleType role, ResourceAction action) =>
        Default.TryGetValue(action, out var authorizedRoles) && authorizedRoles.Contains(role);

    private static IReadOnlySet<RoleType> Roles(params RoleType[] roles) => roles.ToHashSet();
}
