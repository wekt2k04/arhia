using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Persistence;
using Agirh.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Tests.Persistence;

public class WorkflowInstanceRepositoryTests
{
    private static DbContextOptions<AgirhDbContext> CreerOptions() =>
        new DbContextOptionsBuilder<AgirhDbContext>()
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

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new WorkflowInstanceRepository(dbEcriture).AddAsync(instance);
        }

        await using var dbLecture = new AgirhDbContext(options);
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

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new WorkflowInstanceRepository(dbEcriture).AddAsync(instance);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new WorkflowInstanceRepository(dbLecture).GetByIdAsync(instance.Id);

        recharge!.Status.Should().Be(WorkflowStatus.Archived);
        var act = () => recharge.Check(item.Id, ItemStatus.Failed, Guid.NewGuid(), DateTime.UtcNow, null);
        act.Should().Throw<InvalidOperationException>();
    }
}
