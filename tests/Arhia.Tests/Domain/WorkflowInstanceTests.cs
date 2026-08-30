using Arhia.Domain;
using Arhia.Domain.Entities;
using FluentAssertions;

namespace Arhia.Tests.Domain;

public class WorkflowInstanceTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (WorkflowInstance instance, Guid itemId) CreateInstanceWithOneItem()
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        return (instance, item.Id);
    }

    [Fact]
    public void Constructor_NoItems_ThrowsArgumentException()
    {
        var act = () => new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T0", WorkflowType.Onboarding, Array.Empty<ChecklistItemStatus>(), Maintenant);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_InitialStatus_IsInProgress()
    {
        var (instance, _) = CreateInstanceWithOneItem();

        instance.Status.Should().Be(WorkflowStatus.InProgress);
    }

    [Fact]
    public void Check_ExistingItem_UpdatesStatus()
    {
        var (instance, itemId) = CreateInstanceWithOneItem();
        var checkedBy = Guid.NewGuid();

        instance.Check(itemId, ItemStatus.Done, checkedBy, Maintenant, "RAS");

        instance.Items.Single(i => i.Id == itemId).Status.Should().Be(ItemStatus.Done);
    }

    [Fact]
    public void Check_UnknownItem_ThrowsInvalidOperationException()
    {
        var (instance, _) = CreateInstanceWithOneItem();

        var act = () => instance.Check(Guid.NewGuid(), ItemStatus.Done, Guid.NewGuid(), Maintenant, null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Close_WithPendingItem_ThrowsInvalidOperationException()
    {
        var (instance, _) = CreateInstanceWithOneItem();

        var act = () => instance.Close(Maintenant);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Close_AllItemsChecked_MovesToClosedStatus()
    {
        var (instance, itemId) = CreateInstanceWithOneItem();
        instance.Check(itemId, ItemStatus.Done, Guid.NewGuid(), Maintenant, null);

        instance.Close(Maintenant);

        instance.Status.Should().Be(WorkflowStatus.Closed);
        instance.ClosureDate.Should().Be(Maintenant);
    }

    [Fact]
    public void Close_WithFailedItem_IsAccepted()
    {
        var (instance, itemId) = CreateInstanceWithOneItem();
        instance.Check(itemId, ItemStatus.Failed, Guid.NewGuid(), Maintenant, "Non applicable à ce poste");

        var act = () => instance.Close(Maintenant);

        act.Should().NotThrow();
    }

    [Fact]
    public void Archive_OnNonClosedWorkflow_ThrowsInvalidOperationException()
    {
        var (instance, _) = CreateInstanceWithOneItem();

        var act = () => instance.Archive();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Archive_OnClosedWorkflow_MovesToArchivedStatus()
    {
        var (instance, itemId) = CreateInstanceWithOneItem();
        instance.Check(itemId, ItemStatus.Done, Guid.NewGuid(), Maintenant, null);
        instance.Close(Maintenant);

        instance.Archive();

        instance.Status.Should().Be(WorkflowStatus.Archived);
    }

    [Fact]
    public void Check_OnArchivedWorkflow_ThrowsInvalidOperationException()
    {
        var (instance, itemId) = CreateInstanceWithOneItem();
        instance.Check(itemId, ItemStatus.Done, Guid.NewGuid(), Maintenant, null);
        instance.Close(Maintenant);
        instance.Archive();

        var act = () => instance.Check(itemId, ItemStatus.Failed, Guid.NewGuid(), Maintenant, null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_OnArchivedWorkflow_ThrowsInvalidOperationException()
    {
        var (instance, itemId) = CreateInstanceWithOneItem();
        instance.Check(itemId, ItemStatus.Done, Guid.NewGuid(), Maintenant, null);
        instance.Close(Maintenant);
        instance.Archive();

        var act = () => instance.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Suspend_ThenResume_ReturnsToInProgress()
    {
        var (instance, _) = CreateInstanceWithOneItem();

        instance.Suspend();
        instance.Status.Should().Be(WorkflowStatus.Suspended);

        instance.Resume();
        instance.Status.Should().Be(WorkflowStatus.InProgress);
    }
}
