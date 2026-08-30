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

public class ListWorkflowInstancesUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (WorkflowInstance instance, Employee employee) CreateInstance(Guid departmentId)
    {
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Bitlocker activé");
        var instance = new WorkflowInstance(Guid.NewGuid(), employee.Id, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        return (instance, employee);
    }

    [Fact]
    public async Task ExecuteAsync_QualityAdminNoFilter_ListsAllDepartments()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var (instance, employee) = CreateInstance(Guid.NewGuid());

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { instance });
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { employee });

        var useCase = new ListWorkflowInstancesUseCase(instances.Object, employees.Object);
        var resultat = await useCase.ExecuteAsync(admin, null);

        resultat.Should().ContainSingle().Which.Instance.Should().Be(instance);
    }

    [Fact]
    public async Task ExecuteAsync_HRNoFilter_ListsOwnDepartmentOnly()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var (instance, employee) = CreateInstance(departmentId);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ListByDepartmentAsync(departmentId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { instance });
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.ListByDepartmentAsync(departmentId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { employee });

        var useCase = new ListWorkflowInstancesUseCase(instances.Object, employees.Object);
        var resultat = await useCase.ExecuteAsync(rh, null);

        resultat.Should().ContainSingle();
        instances.Verify(r => r.ListByDepartmentAsync(departmentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeLinkedNoFilter_ListsOwnInstancesOnly()
    {
        var actor = new UserAccount(Guid.NewGuid(), "moi@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var (instance, employee) = CreateInstance(Guid.NewGuid());
        employee.LinkUserAccount(actor.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ListByEmployeeAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { instance });
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByUserAccountIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var useCase = new ListWorkflowInstancesUseCase(instances.Object, employees.Object);
        var resultat = await useCase.ExecuteAsync(actor, null);

        resultat.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeNotLinkedNoFilter_ReturnsEmptyList()
    {
        var actor = new UserAccount(Guid.NewGuid(), "moi@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var instances = new Mock<IWorkflowInstanceRepository>();
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByUserAccountIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);

        var useCase = new ListWorkflowInstancesUseCase(instances.Object, employees.Object);
        var resultat = await useCase.ExecuteAsync(actor, null);

        resultat.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeIdFilterWithinHRScope_UsesListByEmployee()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var (instance, employee) = CreateInstance(departmentId);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ListByEmployeeAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { instance });
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var useCase = new ListWorkflowInstancesUseCase(instances.Object, employees.Object);
        var resultat = await useCase.ExecuteAsync(rh, employee.Id);

        resultat.Should().ContainSingle();
        instances.Verify(r => r.ListByEmployeeAsync(employee.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeIdFilterOutsideHRScope_ThrowsAccessDeniedException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var (_, employee) = CreateInstance(Guid.NewGuid());

        var instances = new Mock<IWorkflowInstanceRepository>();
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var useCase = new ListWorkflowInstancesUseCase(instances.Object, employees.Object);
        var act = () => useCase.ExecuteAsync(rh, employee.Id);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeIdFilterUnknown_ThrowsInvalidOperationException()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var instances = new Mock<IWorkflowInstanceRepository>();
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);

        var useCase = new ListWorkflowInstancesUseCase(instances.Object, employees.Object);
        var act = () => useCase.ExecuteAsync(admin, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
