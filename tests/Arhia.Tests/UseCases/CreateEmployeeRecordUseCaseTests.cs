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

public class CreateEmployeeRecordUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    [Fact]
    public async Task ExecuteAsync_HROnOwnDepartment_CreatesTheEmployee()
    {
        var departmentId = Guid.NewGuid();
        var hr = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.GetByEmployeeNumberAsync(It.IsAny<EmployeeNumber>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);
        var useCase = new CreateEmployeeRecordUseCase(repo.Object);

        var resultat = await useCase.ExecuteAsync(
            hr, new EmployeeNumber("MAT001"), "Dupont", "Jean", "Développeur", departmentId, ContractType.CDI, Maintenant);

        resultat.DepartmentId.Should().Be(departmentId);
        repo.Verify(r => r.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HROnAnotherDepartment_ThrowsAccessDeniedException()
    {
        var hr = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var repo = new Mock<IEmployeeRepository>();
        var useCase = new CreateEmployeeRecordUseCase(repo.Object);

        var act = () => useCase.ExecuteAsync(
            hr, new EmployeeNumber("MAT001"), "Dupont", "Jean", "Développeur", Guid.NewGuid(), ContractType.CDI, Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
        repo.Verify(r => r.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeActor_ThrowsAccessDeniedException()
    {
        var employeeActor = new UserAccount(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var repo = new Mock<IEmployeeRepository>();
        var useCase = new CreateEmployeeRecordUseCase(repo.Object);

        var act = () => useCase.ExecuteAsync(
            employeeActor, new EmployeeNumber("MAT001"), "Dupont", "Jean", "Développeur", Guid.NewGuid(), ContractType.CDI, Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeNumberAlreadyUsed_ThrowsInvalidOperationException()
    {
        var departmentId = Guid.NewGuid();
        var hr = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employeeNumber = new EmployeeNumber("MAT001");
        var existant = new Employee(Guid.NewGuid(), employeeNumber, "Autre", "Personne", "Poste", departmentId, ContractType.CDI, Maintenant);
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.GetByEmployeeNumberAsync(employeeNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existant);
        var useCase = new CreateEmployeeRecordUseCase(repo.Object);

        var act = () => useCase.ExecuteAsync(
            hr, employeeNumber, "Dupont", "Jean", "Développeur", departmentId, ContractType.CDI, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
