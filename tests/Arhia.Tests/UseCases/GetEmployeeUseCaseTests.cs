using System.Threading;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Arhia.Domain;
using Arhia.Domain.Entities;
using Arhia.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Arhia.Tests.UseCases;

public class GetEmployeeUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static Employee CreateEmployee(Guid departmentId) =>
        new(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);

    [Fact]
    public async Task ExecuteAsync_HROnOwnDepartment_ReturnsTheEmployee()
    {
        var departmentId = Guid.NewGuid();
        var employee = CreateEmployee(departmentId);
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new GetEmployeeUseCase(employees.Object);

        var resultat = await useCase.ExecuteAsync(rh, employee.Id);

        resultat.Should().Be(employee);
    }

    [Fact]
    public async Task ExecuteAsync_HROnAnotherDepartment_ThrowsAccessDeniedException()
    {
        var employee = CreateEmployee(Guid.NewGuid());
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new GetEmployeeUseCase(employees.Object);

        var act = () => useCase.ExecuteAsync(rh, employee.Id);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeOnOwnRecord_ReturnsIt()
    {
        var employee = CreateEmployee(Guid.NewGuid());
        var actor = new UserAccount(Guid.NewGuid(), "moi@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        employee.LinkUserAccount(actor.Id);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new GetEmployeeUseCase(employees.Object);

        var resultat = await useCase.ExecuteAsync(actor, employee.Id);

        resultat.Should().Be(employee);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeOnAnotherRecord_ThrowsAccessDeniedException()
    {
        var employee = CreateEmployee(Guid.NewGuid());
        var actor = new UserAccount(Guid.NewGuid(), "moi@agirh.test", "hash", RoleType.Employee, null, Maintenant);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new GetEmployeeUseCase(employees.Object);

        var act = () => useCase.ExecuteAsync(actor, employee.Id);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuteAsync_QualityAdmin_ReturnsAnyEmployee()
    {
        var employee = CreateEmployee(Guid.NewGuid());
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new GetEmployeeUseCase(employees.Object);

        var resultat = await useCase.ExecuteAsync(admin, employee.Id);

        resultat.Should().Be(employee);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownEmployee_ThrowsInvalidOperationException()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);
        var useCase = new GetEmployeeUseCase(employees.Object);

        var act = () => useCase.ExecuteAsync(admin, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
