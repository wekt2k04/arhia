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
    [InlineData(RoleType.HR, ResourceAction.TemplateVerify, false)]
    [InlineData(RoleType.QualityAdmin, ResourceAction.TemplateVerify, true)]
    [InlineData(RoleType.QualityAdmin, ResourceAction.TemplateApprove, true)]
    [InlineData(RoleType.HR, ResourceAction.TemplateApprove, false)]
    [InlineData(RoleType.Employee, ResourceAction.WorkflowInstanceRead, true)]
    [InlineData(RoleType.HR, ResourceAction.WorkflowInstanceRead, true)]
    [InlineData(RoleType.QualityAdmin, ResourceAction.WorkflowInstanceRead, true)]
    [InlineData(RoleType.Employee, ResourceAction.WorkflowInstanceCheck, false)]
    [InlineData(RoleType.HR, ResourceAction.WorkflowInstanceArchive, true)]
    [InlineData(RoleType.Employee, ResourceAction.WorkflowInstanceArchive, false)]
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
