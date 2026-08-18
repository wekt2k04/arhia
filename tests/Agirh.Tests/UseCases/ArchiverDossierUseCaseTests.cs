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

public class ArchiverDossierUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static WorkflowInstance CreerInstanceCloturee(Guid collaborateurId)
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), collaborateurId, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        instance.Cocher(item.Id, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);
        instance.Cloturer(Maintenant);
        return instance;
    }

    [Fact]
    public async Task ExecuterAsync_RHSurSonPole_ArchiveLeDossier()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var instance = CreerInstanceCloturee(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new ArchiverDossierUseCase(instances.Object, employees.Object);

        await useCase.ExecuteAsync(rh, instance.Id);

        instance.Statut.Should().Be(WorkflowStatus.Archive);
    }

    [Fact]
    public async Task ExecuterAsync_AdminQualite_PeutArchiverNimporteQuelPole()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);
        var instance = CreerInstanceCloturee(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new ArchiverDossierUseCase(instances.Object, employees.Object);

        await useCase.ExecuteAsync(admin, instance.Id);

        instance.Statut.Should().Be(WorkflowStatus.Archive);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurAutrePole_LeveAccesRefuseException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);
        var instance = CreerInstanceCloturee(employee.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new ArchiverDossierUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, instance.Id);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuterAsync_WorkflowNonCloture_LeveInvalidOperationException()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item");
        var instance = new WorkflowInstance(Guid.NewGuid(), employee.Id, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var useCase = new ArchiverDossierUseCase(instances.Object, employees.Object);

        var act = () => useCase.ExecuteAsync(rh, instance.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
