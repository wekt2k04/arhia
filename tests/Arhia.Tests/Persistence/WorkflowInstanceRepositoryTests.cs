using Arhia.Domain;
using Arhia.Domain.Entities;
using Arhia.Domain.ValueObjects;
using Arhia.Infrastructure.Persistence;
using Arhia.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Arhia.Tests.Persistence;

public class WorkflowInstanceRepositoryTests
{
    private static DbContextOptions<ArhiaDbContext> CreerOptions() =>
        new DbContextOptionsBuilder<ArhiaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task AddCheckThenReload_PersistsItemStatuses()
    {
        var options = CreerOptions();
        var employeeId = Guid.NewGuid();
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), employeeId, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, new DateTime(2026, 8, 15));
        var checkedBy = Guid.NewGuid();
        instance.Check(item.Id, ItemStatus.Done, checkedBy, new DateTime(2026, 8, 16), "RAS");

        await using (var dbEcriture = new ArhiaDbContext(options))
        {
            await new WorkflowInstanceRepository(dbEcriture).AddAsync(instance);
        }

        await using var dbLecture = new ArhiaDbContext(options);
        var recharge = await new WorkflowInstanceRepository(dbLecture).GetByIdAsync(instance.Id);

        recharge.Should().NotBeNull();
        recharge!.Items.Should().ContainSingle();
        var itemRecharge = recharge.Items[0];
        itemRecharge.Status.Should().Be(ItemStatus.Done);
        itemRecharge.CheckedBy.Should().Be(checkedBy);
        itemRecharge.Comment.Should().Be("RAS");
    }

    [Fact]
    public async Task Close_ThenReload_IsReadOnly()
    {
        var options = CreerOptions();
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item unique");
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, new DateTime(2026, 8, 15));
        instance.Check(item.Id, ItemStatus.Done, Guid.NewGuid(), new DateTime(2026, 8, 15), null);
        instance.Close(new DateTime(2026, 8, 20));
        instance.Archive();

        await using (var dbEcriture = new ArhiaDbContext(options))
        {
            await new WorkflowInstanceRepository(dbEcriture).AddAsync(instance);
        }

        await using var dbLecture = new ArhiaDbContext(options);
        var recharge = await new WorkflowInstanceRepository(dbLecture).GetByIdAsync(instance.Id);

        recharge!.Status.Should().Be(WorkflowStatus.Archived);
        var act = () => recharge.Check(item.Id, ItemStatus.Failed, Guid.NewGuid(), DateTime.UtcNow, null);
        act.Should().Throw<InvalidOperationException>();
    }

    private static WorkflowInstance CreateInstanceFor(Guid employeeId) =>
        new(Guid.NewGuid(), employeeId, Guid.NewGuid(), "T0", WorkflowType.Onboarding,
            new[] { new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item") }, new DateTime(2026, 8, 15));

    [Fact]
    public async Task ListByEmployeeAsync_ReturnsOnlyThatEmployeesInstances()
    {
        var options = CreerOptions();
        var employeeId = Guid.NewGuid();
        var instanceA = CreateInstanceFor(employeeId);
        var instanceB = CreateInstanceFor(employeeId);
        var instanceAutrui = CreateInstanceFor(Guid.NewGuid());

        await using (var db = new ArhiaDbContext(options))
        {
            var repo = new WorkflowInstanceRepository(db);
            await repo.AddAsync(instanceA);
            await repo.AddAsync(instanceB);
            await repo.AddAsync(instanceAutrui);
        }

        await using var dbLecture = new ArhiaDbContext(options);
        var resultat = await new WorkflowInstanceRepository(dbLecture).ListByEmployeeAsync(employeeId);

        resultat.Should().HaveCount(2);
        resultat.Should().OnlyContain(i => i.EmployeeId == employeeId);
    }

    [Fact]
    public async Task ListByDepartmentAsync_ReturnsOnlyInstancesOfEmployeesInThatDepartment()
    {
        var options = CreerOptions();
        var departmentA = Guid.NewGuid();
        var departmentB = Guid.NewGuid();
        var employeeA1 = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "A1", "A1", "Dev", departmentA, ContractType.CDI, new DateTime(2026, 8, 1));
        var employeeA2 = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT002"), "A2", "A2", "Dev", departmentA, ContractType.CDI, new DateTime(2026, 8, 1));
        var employeeB1 = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT003"), "B1", "B1", "Dev", departmentB, ContractType.CDI, new DateTime(2026, 8, 1));

        await using (var db = new ArhiaDbContext(options))
        {
            var employeeRepo = new EmployeeRepository(db);
            await employeeRepo.AddAsync(employeeA1);
            await employeeRepo.AddAsync(employeeA2);
            await employeeRepo.AddAsync(employeeB1);
            var instanceRepo = new WorkflowInstanceRepository(db);
            await instanceRepo.AddAsync(CreateInstanceFor(employeeA1.Id));
            await instanceRepo.AddAsync(CreateInstanceFor(employeeA2.Id));
            await instanceRepo.AddAsync(CreateInstanceFor(employeeB1.Id));
        }

        await using var dbLecture = new ArhiaDbContext(options);
        var resultat = await new WorkflowInstanceRepository(dbLecture).ListByDepartmentAsync(departmentA);

        resultat.Should().HaveCount(2);
        resultat.Should().OnlyContain(i => i.EmployeeId == employeeA1.Id || i.EmployeeId == employeeA2.Id);
    }

    [Fact]
    public async Task ListAllAsync_ReturnsInstancesAcrossDepartments()
    {
        var options = CreerOptions();
        await using (var db = new ArhiaDbContext(options))
        {
            var repo = new WorkflowInstanceRepository(db);
            await repo.AddAsync(CreateInstanceFor(Guid.NewGuid()));
            await repo.AddAsync(CreateInstanceFor(Guid.NewGuid()));
        }

        await using var dbLecture = new ArhiaDbContext(options);
        var resultat = await new WorkflowInstanceRepository(dbLecture).ListAllAsync();

        resultat.Should().HaveCount(2);
    }
}
