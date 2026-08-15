using Agirh.Domain;

namespace Agirh.Core.Security;

public static class RbacMatrix
{
    private static readonly IReadOnlyDictionary<ResourceAction, IReadOnlySet<RoleType>> Default =
        new Dictionary<ResourceAction, IReadOnlySet<RoleType>>
        {
            [ResourceAction.CollaborateurCreer] = Roles(RoleType.RH),
            [ResourceAction.WorkflowInstancier] = Roles(RoleType.RH),
            [ResourceAction.WorkflowInstanceLire] = Roles(RoleType.Collaborateur, RoleType.RH, RoleType.AdminQualite),
            [ResourceAction.WorkflowInstanceCocher] = Roles(RoleType.RH),
            [ResourceAction.WorkflowInstanceCloturer] = Roles(RoleType.RH),
            [ResourceAction.WorkflowInstanceArchiver] = Roles(RoleType.RH, RoleType.AdminQualite),
            [ResourceAction.TemplateProposer] = Roles(RoleType.RH),
            [ResourceAction.TemplateVerifier] = Roles(RoleType.AdminQualite),
            [ResourceAction.TemplateApprouver] = Roles(RoleType.AdminQualite),
            [ResourceAction.TemplateRejeter] = Roles(RoleType.AdminQualite),
            [ResourceAction.CompteElevRole] = Roles(RoleType.AdminQualite)
        };

    public static bool EstAutorise(RoleType role, ResourceAction action) =>
        Default.TryGetValue(action, out var rolesAutorises) && rolesAutorises.Contains(role);

    private static IReadOnlySet<RoleType> Roles(params RoleType[] roles) => roles.ToHashSet();
}
