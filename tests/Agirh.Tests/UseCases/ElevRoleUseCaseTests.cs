using System.Threading;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Agirh.Tests.UseCases;

public class ElevRoleUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    [Fact]
    public async Task ExecuterAsync_ActeurAdminQualite_EleveLeRoleDeLaCible()
    {
        var admin = new CompteUtilisateur(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.AdminQualite, null, Maintenant);
        var cible = new CompteUtilisateur(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);
        var nouveauPole = Guid.NewGuid();
        var comptes = new Mock<ICompteUtilisateurRepository>();
        comptes.Setup(r => r.ObtenirParIdAsync(cible.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cible);
        var useCase = new ElevRoleUseCase(comptes.Object);

        var resultat = await useCase.ExecuterAsync(admin, cible.Id, RoleType.RH, nouveauPole);

        resultat.Role.Should().Be(RoleType.RH);
        resultat.PoleId.Should().Be(nouveauPole);
        comptes.Verify(r => r.MettreAJourAsync(cible, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuterAsync_ActeurRH_LeveAccesRefuseException()
    {
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);
        var comptes = new Mock<ICompteUtilisateurRepository>();
        var useCase = new ElevRoleUseCase(comptes.Object);

        var act = () => useCase.ExecuterAsync(rh, Guid.NewGuid(), RoleType.RH, Guid.NewGuid());

        await act.Should().ThrowAsync<AccesRefuseException>();
        comptes.Verify(r => r.ObtenirParIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_CibleInexistante_LeveInvalidOperationException()
    {
        var admin = new CompteUtilisateur(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.AdminQualite, null, Maintenant);
        var comptes = new Mock<ICompteUtilisateurRepository>();
        comptes.Setup(r => r.ObtenirParIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompteUtilisateur?)null);
        var useCase = new ElevRoleUseCase(comptes.Object);

        var act = () => useCase.ExecuterAsync(admin, Guid.NewGuid(), RoleType.RH, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuterAsync_VersRHSansPole_LeveArgumentException()
    {
        var admin = new CompteUtilisateur(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.AdminQualite, null, Maintenant);
        var cible = new CompteUtilisateur(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);
        var comptes = new Mock<ICompteUtilisateurRepository>();
        comptes.Setup(r => r.ObtenirParIdAsync(cible.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cible);
        var useCase = new ElevRoleUseCase(comptes.Object);

        var act = () => useCase.ExecuterAsync(admin, cible.Id, RoleType.RH, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
