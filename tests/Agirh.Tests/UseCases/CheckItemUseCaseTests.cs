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

public class CheckItemUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (WorkflowInstance instance, Guid itemId) CreateInstance(Guid employeeId)
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), employeeId, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        return (instance, item.Id);
    }

    [Fact]
    public async Task ExecuteAsync_HROnOwnDepartment_ChecksTheItem()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var (instance, itemId) = CreateInstance(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CheckItemUseCase(instances.Object, employees.Object);

        await useCase.ExecuteAsync(rh, instance.Id, itemId, ItemStatus.Done, "RAS", Maintenant);

        instance.Items.Single(i => i.Id == itemId).Status.Should().Be(ItemStatus.Done);
        instances.Verify(r => r.UpdateAsync(instance, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HROnAnotherDepartment_ThrowsAccessDeniedException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);
        var (instance, itemId) = CreateInstance(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CheckItemUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, instance.Id, itemId, ItemStatus.Done, null, Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
        instances.Verify(r => r.UpdateAsync(It.IsAny<WorkflowInstance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeActor_ThrowsAccessDeniedException()
    {
        var employeeActor = new UserAccount(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var instances = new Mock<IWorkflowInstanceRepository>();
        var employees = new Mock<IEmployeeRepository>();
        var useCase = new CheckItemUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(employeeActor, Guid.NewGuid(), Guid.NewGuid(), ItemStatus.Done, null, Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
        instances.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedWorkflow_ThrowsInvalidOperationException()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var (instance, itemId) = CreateInstance(employee.Id);
        instance.Check(itemId, ItemStatus.Done, Guid.NewGuid(), Maintenant, null);
        instance.Close(Maintenant);
        instance.Archive();

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CheckItemUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, instance.Id, itemId, ItemStatus.Failed, null, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
