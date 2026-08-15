using Agirh.Domain;
using Agirh.Domain.Entities;
using FluentAssertions;

namespace Agirh.Tests.Domain;

public class WorkflowInstanceTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (WorkflowInstance instance, Guid itemId) CreerInstanceAvecUnItem()
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        return (instance, item.Id);
    }

    [Fact]
    public void Constructeur_SansItems_LeveArgumentException()
    {
        var act = () => new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T0", WorkflowType.Onboarding, Array.Empty<ChecklistItemStatus>(), Maintenant);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructeur_EtatInitial_EstEnCours()
    {
        var (instance, _) = CreerInstanceAvecUnItem();

        instance.Statut.Should().Be(WorkflowStatus.EnCours);
    }

    [Fact]
    public void Cocher_ItemExistant_MetAJourEtat()
    {
        var (instance, itemId) = CreerInstanceAvecUnItem();
        var cochePar = Guid.NewGuid();

        instance.Cocher(itemId, ItemEtat.Ok, cochePar, Maintenant, "RAS");

        instance.Items.Single(i => i.Id == itemId).Etat.Should().Be(ItemEtat.Ok);
    }

    [Fact]
    public void Cocher_ItemInexistant_LeveInvalidOperationException()
    {
        var (instance, _) = CreerInstanceAvecUnItem();

        var act = () => instance.Cocher(Guid.NewGuid(), ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cloturer_AvecItemEnAttente_LeveInvalidOperationException()
    {
        var (instance, _) = CreerInstanceAvecUnItem();

        var act = () => instance.Cloturer(Maintenant);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cloturer_TousItemsCoches_PasseAuStatutCloture()
    {
        var (instance, itemId) = CreerInstanceAvecUnItem();
        instance.Cocher(itemId, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);

        instance.Cloturer(Maintenant);

        instance.Statut.Should().Be(WorkflowStatus.Cloture);
        instance.DateCloture.Should().Be(Maintenant);
    }

    [Fact]
    public void Cloturer_AvecItemKo_EstAcceptee()
    {
        var (instance, itemId) = CreerInstanceAvecUnItem();
        instance.Cocher(itemId, ItemEtat.Ko, Guid.NewGuid(), Maintenant, "Non applicable à ce poste");

        var act = () => instance.Cloturer(Maintenant);

        act.Should().NotThrow();
    }

    [Fact]
    public void Archiver_SurWorkflowNonCloture_LeveInvalidOperationException()
    {
        var (instance, _) = CreerInstanceAvecUnItem();

        var act = () => instance.Archiver();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Archiver_SurWorkflowCloture_PasseAuStatutArchive()
    {
        var (instance, itemId) = CreerInstanceAvecUnItem();
        instance.Cocher(itemId, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);
        instance.Cloturer(Maintenant);

        instance.Archiver();

        instance.Statut.Should().Be(WorkflowStatus.Archive);
    }

    [Fact]
    public void Cocher_SurWorkflowArchive_LeveInvalidOperationException()
    {
        var (instance, itemId) = CreerInstanceAvecUnItem();
        instance.Cocher(itemId, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);
        instance.Cloturer(Maintenant);
        instance.Archiver();

        var act = () => instance.Cocher(itemId, ItemEtat.Ko, Guid.NewGuid(), Maintenant, null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Annuler_SurWorkflowArchive_LeveInvalidOperationException()
    {
        var (instance, itemId) = CreerInstanceAvecUnItem();
        instance.Cocher(itemId, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);
        instance.Cloturer(Maintenant);
        instance.Archiver();

        var act = () => instance.Annuler();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Suspendre_PuisReprendre_RepasseEnCours()
    {
        var (instance, _) = CreerInstanceAvecUnItem();

        instance.Suspendre();
        instance.Statut.Should().Be(WorkflowStatus.Suspendu);

        instance.Reprendre();
        instance.Statut.Should().Be(WorkflowStatus.EnCours);
    }
}
