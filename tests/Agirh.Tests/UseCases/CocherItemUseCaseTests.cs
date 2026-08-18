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

public class CocherItemUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (WorkflowInstance instance, Guid itemId) CreerInstance(Guid collaborateurId)
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), collaborateurId, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        return (instance, item.Id);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurSonPole_CocheLItem()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var (instance, itemId) = CreerInstance(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CocherItemUseCase(instances.Object, employees.Object);

        await useCase.ExecuteAsync(rh, instance.Id, itemId, ItemEtat.Ok, "RAS", Maintenant);

        instance.Items.Single(i => i.Id == itemId).Etat.Should().Be(ItemEtat.Ok);
        instances.Verify(r => r.MettreAJourAsync(instance, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurAutrePole_LeveAccesRefuseException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);
        var (instance, itemId) = CreerInstance(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CocherItemUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, instance.Id, itemId, ItemEtat.Ok, null, Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
        instances.Verify(r => r.MettreAJourAsync(It.IsAny<WorkflowInstance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_ActeurCollaborateur_LeveAccesRefuseException()
    {
        var employeeActor = new UserAccount(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var instances = new Mock<IWorkflowInstanceRepository>();
        var employees = new Mock<IEmployeeRepository>();
        var useCase = new CocherItemUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(employeeActor, Guid.NewGuid(), Guid.NewGuid(), ItemEtat.Ok, null, Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
        instances.Verify(r => r.ObtenirParIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_WorkflowArchive_LeveInvalidOperationException()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var (instance, itemId) = CreerInstance(employee.Id);
        instance.Cocher(itemId, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);
        instance.Cloturer(Maintenant);
        instance.Archiver();

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CocherItemUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, instance.Id, itemId, ItemEtat.Ko, null, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
