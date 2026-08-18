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

public class InstancierWorkflowUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static WorkflowTemplate CreerTemplateApprouve(params TemplateItem[] items)
    {
        var section = new TemplateSection(Guid.NewGuid(), "RH", 0, items);
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), new[] { section }, Maintenant);
        template.Soumettre();
        template.Verifier(Guid.NewGuid());
        template.Approuver(Guid.NewGuid());
        return template;
    }

    [Fact]
    public async Task ExecuterAsync_RHSurSonPole_InstancieLeWorkflowAvecItemsFiltres()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.Stage, Maintenant);
        var itemCommun = new TemplateItem(Guid.NewGuid(), "Bitlocker activé", 0);
        var itemCdiSeulement = new TemplateItem(Guid.NewGuid(), "Processus disciplinaire signé", 1, new[] { ContractType.CDI });
        var template = CreerTemplateApprouve(itemCommun, itemCdiSeulement);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var templates = new Mock<IWorkflowTemplateRepository>();
        templates.Setup(r => r.ObtenirDernierApprouveAsync(WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var instances = new Mock<IWorkflowInstanceRepository>();

        var useCase = new InstancierWorkflowUseCase(employees.Object, templates.Object, instances.Object);

        var resultat = await useCase.ExecuteAsync(rh, employee.Id, WorkflowType.Onboarding, Maintenant);

        resultat.Items.Should().ContainSingle(i => i.Libelle == itemCommun.Libelle);
        instances.Verify(r => r.AjouterAsync(It.IsAny<WorkflowInstance>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurAutrePole_LeveAccesRefuseException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var templates = new Mock<IWorkflowTemplateRepository>();
        var instances = new Mock<IWorkflowInstanceRepository>();
        var useCase = new InstancierWorkflowUseCase(employees.Object, templates.Object, instances.Object);

        var act = () => useCase.ExecuteAsync(rh, employee.Id, WorkflowType.Onboarding, Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
        instances.Verify(r => r.AjouterAsync(It.IsAny<WorkflowInstance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_AucunTemplateApprouve_LeveInvalidOperationException()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var templates = new Mock<IWorkflowTemplateRepository>();
        templates.Setup(r => r.ObtenirDernierApprouveAsync(WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowTemplate?)null);
        var instances = new Mock<IWorkflowInstanceRepository>();
        var useCase = new InstancierWorkflowUseCase(employees.Object, templates.Object, instances.Object);

        var act = () => useCase.ExecuteAsync(rh, employee.Id, WorkflowType.Onboarding, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuterAsync_CollaborateurInexistant_LeveInvalidOperationException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);
        var templates = new Mock<IWorkflowTemplateRepository>();
        var instances = new Mock<IWorkflowInstanceRepository>();
        var useCase = new InstancierWorkflowUseCase(employees.Object, templates.Object, instances.Object);

        var act = () => useCase.ExecuteAsync(rh, Guid.NewGuid(), WorkflowType.Onboarding, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
