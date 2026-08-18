using System.Threading;
using Agirh.Core.Ports;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Agirh.Tests.UseCases;

public class ObtenirNotificationsUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (
        Mock<IEmployeeRepository> Employees,
        Mock<IWorkflowInstanceRepository> Instances,
        Mock<IWorkflowTemplateRepository> Templates,
        ObtenirNotificationsUseCase UseCase) CreerUseCase()
    {
        var employees = new Mock<IEmployeeRepository>();
        var instances = new Mock<IWorkflowInstanceRepository>();
        var templates = new Mock<IWorkflowTemplateRepository>();
        var useCase = new ObtenirNotificationsUseCase(employees.Object, instances.Object, templates.Object);
        return (employees, instances, templates, useCase);
    }

    private static WorkflowInstance CreerInstanceEnCours(Guid collaborateurId, WorkflowType type, DateTime dateCreation)
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item");
        return new WorkflowInstance(Guid.NewGuid(), collaborateurId, Guid.NewGuid(), "T0", type, new[] { item }, dateCreation);
    }

    [Fact]
    public async Task ExecuterAsync_RH_ItemEnAttenteDepuisPlusDe3Jours_ProduitUneNotification()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant.AddDays(-10));
        var instance = CreerInstanceEnCours(employee.Id, WorkflowType.Onboarding, Maintenant.AddDays(-4));

        var (employees, instances, _, useCase) = CreerUseCase();
        employees.Setup(c => c.ListByDepartmentAsync(departmentId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { employee });
        instances.Setup(i => i.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        instances.Setup(i => i.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);

        var resultat = await useCase.ExecuteAsync(rh, Maintenant);

        resultat.Should().ContainSingle(n => n.Type == TypeNotification.ItemEnAttenteDepuisLongtemps);
    }

    [Fact]
    public async Task ExecuterAsync_RH_ItemEnAttenteDepuisMoinsDe3Jours_NeProduitRien()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant.AddDays(-1));
        var instance = CreerInstanceEnCours(employee.Id, WorkflowType.Onboarding, Maintenant.AddDays(-1));

        var (employees, instances, _, useCase) = CreerUseCase();
        employees.Setup(c => c.ListByDepartmentAsync(departmentId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { employee });
        instances.Setup(i => i.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        instances.Setup(i => i.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);

        var resultat = await useCase.ExecuteAsync(rh, Maintenant);

        resultat.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuterAsync_RH_EcheanceDepartDansMoinsDe3JoursSansOffboardingDemarre_ProduitUneNotification()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant.AddYears(-1));
        employee.RecordDeparture(Maintenant.AddDays(2));

        var (employees, instances, _, useCase) = CreerUseCase();
        employees.Setup(c => c.ListByDepartmentAsync(departmentId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { employee });
        instances.Setup(i => i.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);
        instances.Setup(i => i.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);

        var resultat = await useCase.ExecuteAsync(rh, Maintenant);

        resultat.Should().ContainSingle(n => n.Type == TypeNotification.EcheanceDepartApprochante);
    }

    [Fact]
    public async Task ExecuterAsync_RH_EcheanceDepartApprochanteMaisOffboardingDejaDemarre_NeProduitRien()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant.AddYears(-1));
        employee.RecordDeparture(Maintenant.AddDays(2));
        var offboarding = CreerInstanceEnCours(employee.Id, WorkflowType.Offboarding, Maintenant.AddDays(-1));

        var (employees, instances, _, useCase) = CreerUseCase();
        employees.Setup(c => c.ListByDepartmentAsync(departmentId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { employee });
        instances.Setup(i => i.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);
        instances.Setup(i => i.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>())).ReturnsAsync(offboarding);

        var resultat = await useCase.ExecuteAsync(rh, Maintenant);

        resultat.Should().NotContain(n => n.Type == TypeNotification.EcheanceDepartApprochante);
    }

    [Fact]
    public async Task ExecuterAsync_Collaborateur_RetourneListeVide()
    {
        var employeeActor = new UserAccount(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var (_, _, _, useCase) = CreerUseCase();

        var resultat = await useCase.ExecuteAsync(employeeActor, Maintenant);

        resultat.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuterAsync_AdminQualite_TemplateEnValidation_ProduitUneNotification()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var section = new TemplateSection(Guid.NewGuid(), "Section", 0, new[] { new TemplateItem(Guid.NewGuid(), "Item", 0) });
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T1", Guid.NewGuid(), new[] { section }, Maintenant);
        template.Soumettre();

        var (_, _, templates, useCase) = CreerUseCase();
        templates.Setup(t => t.ListerParStatutAsync(TemplateStatut.EnValidation, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { template });

        var resultat = await useCase.ExecuteAsync(admin, Maintenant);

        resultat.Should().ContainSingle(n => n.Type == TypeNotification.TemplateEnAttenteValidation);
    }

    [Fact]
    public async Task ExecuterAsync_AdminQualite_AucunTemplateEnValidation_RetourneListeVide()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var (_, _, templates, useCase) = CreerUseCase();
        templates.Setup(t => t.ListerParStatutAsync(TemplateStatut.EnValidation, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<WorkflowTemplate>());

        var resultat = await useCase.ExecuteAsync(admin, Maintenant);

        resultat.Should().BeEmpty();
    }
}
