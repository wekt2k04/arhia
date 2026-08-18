using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using FluentAssertions;

namespace Agirh.Tests.Security;

public class DepartmentScopeGuardTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    [Fact]
    public void CanAccessDepartment_QualityAdmin_AlwaysAllowed()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);

        DepartmentScopeGuard.CanAccessDepartment(admin, Guid.NewGuid()).Should().BeTrue();
    }

    [Fact]
    public void CanAccessDepartment_HROnOwnDepartment_IsAllowed()
    {
        var departmentId = Guid.NewGuid();
        var hr = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);

        DepartmentScopeGuard.CanAccessDepartment(hr, departmentId).Should().BeTrue();
    }

    [Fact]
    public void CanAccessDepartment_HROnAnotherDepartment_IsDenied()
    {
        var hr = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);

        DepartmentScopeGuard.CanAccessDepartment(hr, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void CanAccessEmployee_HRSameDepartment_IsAllowed()
    {
        var departmentId = Guid.NewGuid();
        var hr = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);

        DepartmentScopeGuard.CanAccessEmployee(hr, employee).Should().BeTrue();
    }

    [Fact]
    public void CanAccessEmployee_HRAnotherDepartment_IsDenied()
    {
        var hr = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);

        DepartmentScopeGuard.CanAccessEmployee(hr, employee).Should().BeFalse();
    }

    [Fact]
    public void CanAccessEmployee_EmployeeOnOwnRecord_IsAllowed()
    {
        var accountId = Guid.NewGuid();
        var actor = new UserAccount(accountId, "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);
        employee.LinkUserAccount(accountId);

        DepartmentScopeGuard.CanAccessEmployee(actor, employee).Should().BeTrue();
    }

    [Fact]
    public void CanAccessEmployee_EmployeeOnAnothersRecord_IsDenied()
    {
        var actor = new UserAccount(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);
        employee.LinkUserAccount(Guid.NewGuid());

        DepartmentScopeGuard.CanAccessEmployee(actor, employee).Should().BeFalse();
    }

    [Fact]
    public void CanAccessEmployee_EmployeeWithoutLinkedAccount_IsDenied()
    {
        var actor = new UserAccount(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);

        DepartmentScopeGuard.CanAccessEmployee(actor, employee).Should().BeFalse();
    }
}
