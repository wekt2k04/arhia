using System.Threading;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Agirh.Tests.UseCases;

public class CloseCaseUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static WorkflowInstance CreateInstanceWithAllItemsChecked(Guid employeeId)
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), employeeId, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        instance.Check(item.Id, ItemStatus.Done, Guid.NewGuid(), Maintenant, null);
        return instance;
    }

    [Fact]
    public async Task ExecuteAsync_HROnOwnDepartment_AllItemsChecked_ClosesTheCase()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var instance = CreateInstanceWithAllItemsChecked(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CloseCaseUseCase(instances.Object, employees.Object);

        await useCase.ExecuteAsync(rh, instance.Id, Maintenant);

        instance.Status.Should().Be(WorkflowStatus.Closed);
    }

    [Fact]
    public async Task ExecuteAsync_HROnAnotherDepartment_ThrowsAccessDeniedException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);
        var instance = CreateInstanceWithAllItemsChecked(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CloseCaseUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, instance.Id, Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuteAsync_ItemStillPending_ThrowsInvalidOperationException()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item jamais coché");
        var instance = new WorkflowInstance(Guid.NewGuid(), employee.Id, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CloseCaseUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, instance.Id, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_UnknownWorkflow_ThrowsInvalidOperationException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);
        var employees = new Mock<IEmployeeRepository>();
        var useCase = new CloseCaseUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, Guid.NewGuid(), Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
