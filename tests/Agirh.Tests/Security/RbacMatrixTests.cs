using Agirh.Core.Security;
using Agirh.Domain;
using FluentAssertions;

namespace Agirh.Tests.Security;

public class RbacMatrixTests
{
    [Theory]
    [InlineData(RoleType.HR, ResourceAction.EmployeeCreate, true)]
    [InlineData(RoleType.Employee, ResourceAction.EmployeeCreate, false)]
    [InlineData(RoleType.QualityAdmin, ResourceAction.EmployeeCreate, false)]
    [InlineData(RoleType.HR, ResourceAction.TemplateVerifier, false)]
    [InlineData(RoleType.QualityAdmin, ResourceAction.TemplateVerifier, true)]
    [InlineData(RoleType.QualityAdmin, ResourceAction.TemplateApprouver, true)]
    [InlineData(RoleType.HR, ResourceAction.TemplateApprouver, false)]
    [InlineData(RoleType.Employee, ResourceAction.WorkflowInstanceLire, true)]
    [InlineData(RoleType.HR, ResourceAction.WorkflowInstanceLire, true)]
    [InlineData(RoleType.QualityAdmin, ResourceAction.WorkflowInstanceLire, true)]
    [InlineData(RoleType.Employee, ResourceAction.WorkflowInstanceCocher, false)]
    [InlineData(RoleType.HR, ResourceAction.WorkflowInstanceArchiver, true)]
    [InlineData(RoleType.Employee, ResourceAction.WorkflowInstanceArchiver, false)]
    [InlineData(RoleType.HR, ResourceAction.UserAccountElevateRole, false)]
    [InlineData(RoleType.QualityAdmin, ResourceAction.UserAccountElevateRole, true)]
    public void IsAuthorized_ReturnsExpectedResult(RoleType role, ResourceAction action, bool attendu)
    {
        RbacMatrix.IsAuthorized(role, action).Should().Be(attendu);
    }

    [Fact]
    public void IsAuthorized_UnmappedAction_IsDeniedByDefault()
    {
        var actionInconnue = (ResourceAction)999;

        RbacMatrix.IsAuthorized(RoleType.QualityAdmin, actionInconnue).Should().BeFalse();
    }
}
