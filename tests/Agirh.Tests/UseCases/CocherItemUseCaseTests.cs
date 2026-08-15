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

public class CocherItemUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (WorkflowInstance instance, Guid itemId) CreerInstance(Guid collaborateurId)
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), collaborateurId, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        return (instance, item.Id);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurSonPole_CocheLItem()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant);
        var (instance, itemId) = CreerInstance(collaborateur.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var useCase = new CocherItemUseCase(instances.Object, collaborateurs.Object);

        await useCase.ExecuterAsync(rh, instance.Id, itemId, ItemEtat.Ok, "RAS", Maintenant);

        instance.Items.Single(i => i.Id == itemId).Etat.Should().Be(ItemEtat.Ok);
        instances.Verify(r => r.MettreAJourAsync(instance, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurAutrePole_LeveAccesRefuseException()
    {
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), TypeContrat.CDI, Maintenant);
        var (instance, itemId) = CreerInstance(collaborateur.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var useCase = new CocherItemUseCase(instances.Object, collaborateurs.Object);

        var act = () => useCase.ExecuterAsync(rh, instance.Id, itemId, ItemEtat.Ok, null, Maintenant);

        await act.Should().ThrowAsync<AccesRefuseException>();
        instances.Verify(r => r.MettreAJourAsync(It.IsAny<WorkflowInstance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_ActeurCollaborateur_LeveAccesRefuseException()
    {
        var collaborateurActeur = new CompteUtilisateur(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);
        var instances = new Mock<IWorkflowInstanceRepository>();
        var collaborateurs = new Mock<ICollaborateurRepository>();
        var useCase = new CocherItemUseCase(instances.Object, collaborateurs.Object);

        var act = () => useCase.ExecuterAsync(collaborateurActeur, Guid.NewGuid(), Guid.NewGuid(), ItemEtat.Ok, null, Maintenant);

        await act.Should().ThrowAsync<AccesRefuseException>();
        instances.Verify(r => r.ObtenirParIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_WorkflowArchive_LeveInvalidOperationException()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant);
        var (instance, itemId) = CreerInstance(collaborateur.Id);
        instance.Cocher(itemId, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);
        instance.Cloturer(Maintenant);
        instance.Archiver();

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var useCase = new CocherItemUseCase(instances.Object, collaborateurs.Object);

        var act = () => useCase.ExecuterAsync(rh, instance.Id, itemId, ItemEtat.Ko, null, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
