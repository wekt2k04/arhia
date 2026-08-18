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

public class CloturerDossierUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static WorkflowInstance CreerInstanceTousItemsTraites(Guid collaborateurId)
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), collaborateurId, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        instance.Cocher(item.Id, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);
        return instance;
    }

    [Fact]
    public async Task ExecuterAsync_RHSurSonPole_TousItemsTraites_ClotureLeDossier()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var instance = CreerInstanceTousItemsTraites(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CloturerDossierUseCase(instances.Object, employees.Object);

        await useCase.ExecuteAsync(rh, instance.Id, Maintenant);

        instance.Statut.Should().Be(WorkflowStatus.Cloture);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurAutrePole_LeveAccesRefuseException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);
        var instance = CreerInstanceTousItemsTraites(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CloturerDossierUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, instance.Id, Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuterAsync_ItemEncoreEnAttente_LeveInvalidOperationException()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item jamais coché");
        var instance = new WorkflowInstance(Guid.NewGuid(), employee.Id, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new CloturerDossierUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, instance.Id, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuterAsync_WorkflowInexistant_LeveInvalidOperationException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);
        var employees = new Mock<IEmployeeRepository>();
        var useCase = new CloturerDossierUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, Guid.NewGuid(), Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
