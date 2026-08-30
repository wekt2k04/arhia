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

public class GetWorkflowInstanceUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (WorkflowInstance instance, Employee employee) CreateInstance(Guid departmentId)
    {
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Bitlocker activé");
        var instance = new WorkflowInstance(Guid.NewGuid(), employee.Id, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        return (instance, employee);
    }

    private static Mock<IWorkflowInstanceRepository> MockInstances(WorkflowInstance instance)
    {
        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        return instances;
    }

    [Fact]
    public async Task ExecuteAsync_HROnOwnDepartment_ReturnsDetailWithTemplate()
    {
        var departmentId = Guid.NewGuid();
        var (instance, employee) = CreateInstance(departmentId);
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var section = new TemplateSection(Guid.NewGuid(), "RH", 0, new[] { new TemplateItem(Guid.NewGuid(), "X", 0) });
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), new[] { section }, Maintenant);

        var instances = MockInstances(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var templates = new Mock<IWorkflowTemplateRepository>();
        templates.Setup(r => r.GetByIdAsync(instance.TemplateId, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        var useCase = new GetWorkflowInstanceUseCase(instances.Object, employees.Object, templates.Object);
        var resultat = await useCase.ExecuteAsync(rh, instance.Id);

        resultat.Template.Should().Be(template);
        resultat.Employee.Should().Be(employee);
    }

    [Fact]
    public async Task ExecuteAsync_HROnAnotherDepartment_ThrowsAccessDeniedException()
    {
        var (instance, employee) = CreateInstance(Guid.NewGuid());
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);

        var instances = MockInstances(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var templates = new Mock<IWorkflowTemplateRepository>();

        var useCase = new GetWorkflowInstanceUseCase(instances.Object, employees.Object, templates.Object);
        var act = () => useCase.ExecuteAsync(rh, instance.Id);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeOnOwnCase_ReturnsDetail()
    {
        var (instance, employee) = CreateInstance(Guid.NewGuid());
        var actor = new UserAccount(Guid.NewGuid(), "moi@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        employee.LinkUserAccount(actor.Id);

        var instances = MockInstances(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var templates = new Mock<IWorkflowTemplateRepository>();
        templates.Setup(r => r.GetByIdAsync(instance.TemplateId, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowTemplate?)null);

        var useCase = new GetWorkflowInstanceUseCase(instances.Object, employees.Object, templates.Object);
        var resultat = await useCase.ExecuteAsync(actor, instance.Id);

        resultat.Instance.Should().Be(instance);
        resultat.Template.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeOnAnotherCase_ThrowsAccessDeniedException()
    {
        var (instance, employee) = CreateInstance(Guid.NewGuid());
        var actor = new UserAccount(Guid.NewGuid(), "moi@agirh.test", "hash", RoleType.Employee, null, Maintenant);

        var instances = MockInstances(instance);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var templates = new Mock<IWorkflowTemplateRepository>();

        var useCase = new GetWorkflowInstanceUseCase(instances.Object, employees.Object, templates.Object);
        var act = () => useCase.ExecuteAsync(actor, instance.Id);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuteAsync_UnknownWorkflow_ThrowsInvalidOperationException()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);
        var employees = new Mock<IEmployeeRepository>();
        var templates = new Mock<IWorkflowTemplateRepository>();

        var useCase = new GetWorkflowInstanceUseCase(instances.Object, employees.Object, templates.Object);
        var act = () => useCase.ExecuteAsync(admin, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
