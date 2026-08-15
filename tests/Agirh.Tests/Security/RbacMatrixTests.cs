using Agirh.Core.Security;
using Agirh.Domain;
using FluentAssertions;

namespace Agirh.Tests.Security;

public class RbacMatrixTests
{
    [Theory]
    [InlineData(RoleType.RH, ResourceAction.CollaborateurCreer, true)]
    [InlineData(RoleType.Collaborateur, ResourceAction.CollaborateurCreer, false)]
    [InlineData(RoleType.AdminQualite, ResourceAction.CollaborateurCreer, false)]
    [InlineData(RoleType.RH, ResourceAction.TemplateVerifier, false)]
    [InlineData(RoleType.AdminQualite, ResourceAction.TemplateVerifier, true)]
    [InlineData(RoleType.AdminQualite, ResourceAction.TemplateApprouver, true)]
    [InlineData(RoleType.RH, ResourceAction.TemplateApprouver, false)]
    [InlineData(RoleType.Collaborateur, ResourceAction.WorkflowInstanceLire, true)]
    [InlineData(RoleType.RH, ResourceAction.WorkflowInstanceLire, true)]
    [InlineData(RoleType.AdminQualite, ResourceAction.WorkflowInstanceLire, true)]
    [InlineData(RoleType.Collaborateur, ResourceAction.WorkflowInstanceCocher, false)]
    [InlineData(RoleType.RH, ResourceAction.WorkflowInstanceArchiver, true)]
    [InlineData(RoleType.Collaborateur, ResourceAction.WorkflowInstanceArchiver, false)]
    [InlineData(RoleType.RH, ResourceAction.CompteElevRole, false)]
    [InlineData(RoleType.AdminQualite, ResourceAction.CompteElevRole, true)]
    public void EstAutorise_RetourneLeResultatAttendu(RoleType role, ResourceAction action, bool attendu)
    {
        RbacMatrix.EstAutorise(role, action).Should().Be(attendu);
    }

    [Fact]
    public void EstAutorise_ActionNonMappee_EstRefuseeParDefaut()
    {
        var actionInconnue = (ResourceAction)999;

        RbacMatrix.EstAutorise(RoleType.AdminQualite, actionInconnue).Should().BeFalse();
    }
}
