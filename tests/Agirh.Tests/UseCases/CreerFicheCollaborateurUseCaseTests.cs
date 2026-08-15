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

public class CreerFicheCollaborateurUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    [Fact]
    public async Task ExecuterAsync_RHSurSonPole_CreeLeCollaborateur()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var repo = new Mock<ICollaborateurRepository>();
        repo.Setup(r => r.ObtenirParMatriculeAsync(It.IsAny<Matricule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collaborateur?)null);
        var useCase = new CreerFicheCollaborateurUseCase(repo.Object);

        var resultat = await useCase.ExecuterAsync(
            rh, new Matricule("MAT001"), "Dupont", "Jean", "Développeur", poleId, TypeContrat.CDI, Maintenant);

        resultat.PoleId.Should().Be(poleId);
        repo.Verify(r => r.AjouterAsync(It.IsAny<Collaborateur>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurAutrePole_LeveAccesRefuseException()
    {
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);
        var repo = new Mock<ICollaborateurRepository>();
        var useCase = new CreerFicheCollaborateurUseCase(repo.Object);

        var act = () => useCase.ExecuterAsync(
            rh, new Matricule("MAT001"), "Dupont", "Jean", "Développeur", Guid.NewGuid(), TypeContrat.CDI, Maintenant);

        await act.Should().ThrowAsync<AccesRefuseException>();
        repo.Verify(r => r.AjouterAsync(It.IsAny<Collaborateur>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_ActeurCollaborateur_LeveAccesRefuseException()
    {
        var collaborateurActeur = new CompteUtilisateur(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);
        var repo = new Mock<ICollaborateurRepository>();
        var useCase = new CreerFicheCollaborateurUseCase(repo.Object);

        var act = () => useCase.ExecuterAsync(
            collaborateurActeur, new Matricule("MAT001"), "Dupont", "Jean", "Développeur", Guid.NewGuid(), TypeContrat.CDI, Maintenant);

        await act.Should().ThrowAsync<AccesRefuseException>();
    }

    [Fact]
    public async Task ExecuterAsync_MatriculeDejaUtilise_LeveInvalidOperationException()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var matricule = new Matricule("MAT001");
        var existant = new Collaborateur(Guid.NewGuid(), matricule, "Autre", "Personne", "Poste", poleId, TypeContrat.CDI, Maintenant);
        var repo = new Mock<ICollaborateurRepository>();
        repo.Setup(r => r.ObtenirParMatriculeAsync(matricule, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existant);
        var useCase = new CreerFicheCollaborateurUseCase(repo.Object);

        var act = () => useCase.ExecuterAsync(
            rh, matricule, "Dupont", "Jean", "Développeur", poleId, TypeContrat.CDI, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
