using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using FluentAssertions;

namespace Agirh.Tests.Domain;

public class EmployeeTests
{
    private static readonly DateTime StartDate = new(2026, 1, 15);

    private static Employee CreateEmployee() =>
        new(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Développeur", Guid.NewGuid(), ContractType.CDI, StartDate);

    [Fact]
    public void Constructor_EmptyDepartmentId_ThrowsArgumentException()
    {
        var act = () => new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Développeur", Guid.Empty, ContractType.CDI, StartDate);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyLastName_ThrowsArgumentException(string lastName)
    {
        var act = () => new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), lastName, "Jean", "Développeur", Guid.NewGuid(), ContractType.CDI, StartDate);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordDeparture_DateAfterStart_IsAccepted()
    {
        var employee = CreateEmployee();

        employee.RecordDeparture(StartDate.AddYears(1));

        employee.DepartureDate.Should().Be(StartDate.AddYears(1));
    }

    [Fact]
    public void RecordDeparture_DateBeforeStart_ThrowsInvalidOperationException()
    {
        var employee = CreateEmployee();

        var act = () => employee.RecordDeparture(StartDate.AddDays(-1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RecordDeparture_SameDateAsStart_IsAccepted()
    {
        var employee = CreateEmployee();

        var act = () => employee.RecordDeparture(StartDate);

        act.Should().NotThrow();
    }

    [Fact]
    public void ChangeDepartment_NewValidDepartment_UpdatesDepartmentId()
    {
        var employee = CreateEmployee();
        var newDepartmentId = Guid.NewGuid();

        employee.ChangeDepartment(newDepartmentId);

        employee.DepartmentId.Should().Be(newDepartmentId);
    }

    [Fact]
    public void ChangeDepartment_EmptyDepartment_ThrowsArgumentException()
    {
        var employee = CreateEmployee();

        var act = () => employee.ChangeDepartment(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkUserAccount_ValidId_UpdatesUserAccountId()
    {
        var employee = CreateEmployee();
        var accountId = Guid.NewGuid();

        employee.LinkUserAccount(accountId);

        employee.UserAccountId.Should().Be(accountId);
    }
}
