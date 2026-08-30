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

public class InstantiateWorkflowUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static WorkflowTemplate CreateApprovedTemplate(params TemplateItem[] items)
    {
        var section = new TemplateSection(Guid.NewGuid(), "RH", 0, items);
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), new[] { section }, Maintenant);
        template.Submit();
        template.Verify(Guid.NewGuid());
        template.Approve(Guid.NewGuid());
        return template;
    }

    [Fact]
    public async Task ExecuteAsync_HROnOwnDepartment_InstantiatesTheWorkflowWithFilteredItems()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.Stage, Maintenant);
        var itemCommun = new TemplateItem(Guid.NewGuid(), "Bitlocker activé", 0);
        var itemCdiSeulement = new TemplateItem(Guid.NewGuid(), "Processus disciplinaire signé", 1, new[] { ContractType.CDI });
        var template = CreateApprovedTemplate(itemCommun, itemCdiSeulement);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var templates = new Mock<IWorkflowTemplateRepository>();
        templates.Setup(r => r.GetLastApprovedAsync(WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var instances = new Mock<IWorkflowInstanceRepository>();

        var useCase = new InstantiateWorkflowUseCase(employees.Object, templates.Object, instances.Object);

        var resultat = await useCase.ExecuteAsync(rh, employee.Id, WorkflowType.Onboarding, Maintenant);

        resultat.Items.Should().ContainSingle(i => i.Label == itemCommun.Label);
        instances.Verify(r => r.AddAsync(It.IsAny<WorkflowInstance>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HROnAnotherDepartment_ThrowsAccessDeniedException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var templates = new Mock<IWorkflowTemplateRepository>();
        var instances = new Mock<IWorkflowInstanceRepository>();
        var useCase = new InstantiateWorkflowUseCase(employees.Object, templates.Object, instances.Object);

        var act = () => useCase.ExecuteAsync(rh, employee.Id, WorkflowType.Onboarding, Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
        instances.Verify(r => r.AddAsync(It.IsAny<WorkflowInstance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoApprovedTemplate_ThrowsInvalidOperationException()
    {
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var templates = new Mock<IWorkflowTemplateRepository>();
        templates.Setup(r => r.GetLastApprovedAsync(WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowTemplate?)null);
        var instances = new Mock<IWorkflowInstanceRepository>();
        var useCase = new InstantiateWorkflowUseCase(employees.Object, templates.Object, instances.Object);

        var act = () => useCase.ExecuteAsync(rh, employee.Id, WorkflowType.Onboarding, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_UnknownEmployee_ThrowsInvalidOperationException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);
        var templates = new Mock<IWorkflowTemplateRepository>();
        var instances = new Mock<IWorkflowInstanceRepository>();
        var useCase = new InstantiateWorkflowUseCase(employees.Object, templates.Object, instances.Object);

        var act = () => useCase.ExecuteAsync(rh, Guid.NewGuid(), WorkflowType.Onboarding, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
