using System.Threading;
using Arhia.Core.Ports;
using Arhia.Core.UseCases;
using Arhia.Domain;
using Arhia.Domain.Entities;
using Arhia.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Arhia.Tests.UseCases;

public class ListEmployeesUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static Employee CreateEmployee(Guid departmentId) =>
        new(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);

    [Fact]
    public async Task ExecuteAsync_QualityAdmin_ListsAllEmployees()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { CreateEmployee(Guid.NewGuid()) });
        var useCase = new ListEmployeesUseCase(employees.Object);

        var resultat = await useCase.ExecuteAsync(admin);

        resultat.Should().HaveCount(1);
        employees.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HR_ListsOwnDepartmentOnly()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.ListByDepartmentAsync(departmentId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { CreateEmployee(departmentId) });
        var useCase = new ListEmployeesUseCase(employees.Object);

        var resultat = await useCase.ExecuteAsync(rh);

        resultat.Should().HaveCount(1);
        employees.Verify(r => r.ListByDepartmentAsync(departmentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeLinked_ReturnsOwnRecordOnly()
    {
        var actor = new UserAccount(Guid.NewGuid(), "moi@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var ownRecord = CreateEmployee(Guid.NewGuid());
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByUserAccountIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ownRecord);
        var useCase = new ListEmployeesUseCase(employees.Object);

        var resultat = await useCase.ExecuteAsync(actor);

        resultat.Should().ContainSingle().Which.Should().Be(ownRecord);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeNotLinked_ReturnsEmptyList()
    {
        var actor = new UserAccount(Guid.NewGuid(), "moi@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByUserAccountIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);
        var useCase = new ListEmployeesUseCase(employees.Object);

        var resultat = await useCase.ExecuteAsync(actor);

        resultat.Should().BeEmpty();
    }
}
