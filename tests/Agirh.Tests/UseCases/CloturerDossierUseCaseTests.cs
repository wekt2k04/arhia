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

public class CloturerDossierUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static WorkflowInstance CreerInstanceTousItemsTraites(Guid collaborateurId)
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), collaborateurId, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        instance.Cocher(item.Id, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);
        return instance;
    }

    [Fact]
    public async Task ExecuterAsync_RHSurSonPole_TousItemsTraites_ClotureLeDossier()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant);
        var instance = CreerInstanceTousItemsTraites(collaborateur.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var useCase = new CloturerDossierUseCase(instances.Object, collaborateurs.Object);

        await useCase.ExecuterAsync(rh, instance.Id, Maintenant);

        instance.Statut.Should().Be(WorkflowStatus.Cloture);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurAutrePole_LeveAccesRefuseException()
    {
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), TypeContrat.CDI, Maintenant);
        var instance = CreerInstanceTousItemsTraites(collaborateur.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var useCase = new CloturerDossierUseCase(instances.Object, collaborateurs.Object);

        var act = () => useCase.ExecuterAsync(rh, instance.Id, Maintenant);

        await act.Should().ThrowAsync<AccesRefuseException>();
    }

    [Fact]
    public async Task ExecuterAsync_ItemEncoreEnAttente_LeveInvalidOperationException()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant);
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item jamais coché");
        var instance = new WorkflowInstance(Guid.NewGuid(), collaborateur.Id, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var useCase = new CloturerDossierUseCase(instances.Object, collaborateurs.Object);

        var act = () => useCase.ExecuterAsync(rh, instance.Id, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuterAsync_WorkflowInexistant_LeveInvalidOperationException()
    {
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);
        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        var useCase = new CloturerDossierUseCase(instances.Object, collaborateurs.Object);

        var act = () => useCase.ExecuterAsync(rh, Guid.NewGuid(), Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
