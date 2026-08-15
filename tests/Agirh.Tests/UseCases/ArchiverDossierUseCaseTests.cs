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

public class ArchiverDossierUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static WorkflowInstance CreerInstanceCloturee(Guid collaborateurId)
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), collaborateurId, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);
        instance.Cocher(item.Id, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);
        instance.Cloturer(Maintenant);
        return instance;
    }

    [Fact]
    public async Task ExecuterAsync_RHSurSonPole_ArchiveLeDossier()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant);
        var instance = CreerInstanceCloturee(collaborateur.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var useCase = new ArchiverDossierUseCase(instances.Object, collaborateurs.Object);

        await useCase.ExecuterAsync(rh, instance.Id);

        instance.Statut.Should().Be(WorkflowStatus.Archive);
    }

    [Fact]
    public async Task ExecuterAsync_AdminQualite_PeutArchiverNimporteQuelPole()
    {
        var admin = new CompteUtilisateur(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.AdminQualite, null, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), TypeContrat.CDI, Maintenant);
        var instance = CreerInstanceCloturee(collaborateur.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var useCase = new ArchiverDossierUseCase(instances.Object, collaborateurs.Object);

        await useCase.ExecuterAsync(admin, instance.Id);

        instance.Statut.Should().Be(WorkflowStatus.Archive);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurAutrePole_LeveAccesRefuseException()
    {
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), TypeContrat.CDI, Maintenant);
        var instance = CreerInstanceCloturee(collaborateur.Id);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var useCase = new ArchiverDossierUseCase(instances.Object, collaborateurs.Object);

        var act = () => useCase.ExecuterAsync(rh, instance.Id);

        await act.Should().ThrowAsync<AccesRefuseException>();
    }

    [Fact]
    public async Task ExecuterAsync_WorkflowNonCloture_LeveInvalidOperationException()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant);
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item");
        var instance = new WorkflowInstance(Guid.NewGuid(), collaborateur.Id, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, Maintenant);

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(r => r.ObtenirParIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var useCase = new ArchiverDossierUseCase(instances.Object, collaborateurs.Object);

        var act = () => useCase.ExecuterAsync(rh, instance.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
