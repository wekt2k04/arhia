using Agirh.Domain;
using Agirh.Domain.Entities;
using FluentAssertions;

namespace Agirh.Tests.Domain;

public class UserAccountTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_HRRole_WithoutDepartment_ThrowsArgumentException()
    {
        var act = () => new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_HRRole_WithDepartment_IsAccepted()
    {
        var departmentId = Guid.NewGuid();

        var account = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Now);

        account.DepartmentId.Should().Be(departmentId);
    }

    [Fact]
    public void Constructor_QualityAdminRole_WithDepartment_ThrowsArgumentException()
    {
        var act = () => new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, Guid.NewGuid(), Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmployeeRole_WithDepartment_ThrowsArgumentException()
    {
        var act = () => new UserAccount(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Employee, Guid.NewGuid(), Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("pas-un-email")]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_InvalidEmail_ThrowsArgumentException(string email)
    {
        var act = () => new UserAccount(Guid.NewGuid(), email, "hash", RoleType.QualityAdmin, null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ElevateRole_FromEmployeeToHR_UpdatesRoleAndDepartment()
    {
        var account = new UserAccount(Guid.NewGuid(), "user@agirh.test", "hash", RoleType.Employee, null, Now);
        var newDepartmentId = Guid.NewGuid();

        account.ElevateRole(RoleType.HR, newDepartmentId);

        account.Role.Should().Be(RoleType.HR);
        account.DepartmentId.Should().Be(newDepartmentId);
    }

    [Fact]
    public void ElevateRole_ToHRWithoutDepartment_ThrowsArgumentException()
    {
        var account = new UserAccount(Guid.NewGuid(), "user@agirh.test", "hash", RoleType.Employee, null, Now);

        var act = () => account.ElevateRole(RoleType.HR, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_ThenReactivate_TogglesIsActive()
    {
        var account = new UserAccount(Guid.NewGuid(), "user@agirh.test", "hash", RoleType.QualityAdmin, null, Now);

        account.Deactivate();
        account.IsActive.Should().BeFalse();

        account.Reactivate();
        account.IsActive.Should().BeTrue();
    }
}
