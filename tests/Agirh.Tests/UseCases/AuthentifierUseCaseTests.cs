using System.Threading;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Agirh.Tests.UseCases;

public class AuthentifierUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    [Fact]
    public async Task ExecuterAsync_IdentifiantsValides_RetourneLeCompte()
    {
        var compte = new CompteUtilisateur(Guid.NewGuid(), "user@agirh.test", "hash-stocke", RoleType.RH, Guid.NewGuid(), Maintenant);
        var comptes = new Mock<ICompteUtilisateurRepository>();
        comptes.Setup(r => r.ObtenirParEmailAsync("user@agirh.test", It.IsAny<CancellationToken>())).ReturnsAsync(compte);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.VerifierMotDePasse("motdepasse", "hash-stocke")).Returns(true);
        var useCase = new AuthentifierUseCase(comptes.Object, hasher.Object);

        var resultat = await useCase.ExecuterAsync("user@agirh.test", "motdepasse");

        resultat.Should().Be(compte);
    }

    [Fact]
    public async Task ExecuterAsync_CompteInexistant_LeveAccesRefuseException()
    {
        var comptes = new Mock<ICompteUtilisateurRepository>();
        comptes.Setup(r => r.ObtenirParEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompteUtilisateur?)null);
        var hasher = new Mock<IPasswordHasher>();
        var useCase = new AuthentifierUseCase(comptes.Object, hasher.Object);

        var act = () => useCase.ExecuterAsync("inconnu@agirh.test", "motdepasse");

        await act.Should().ThrowAsync<AccesRefuseException>();
    }

    [Fact]
    public async Task ExecuterAsync_MotDePasseIncorrect_LeveAccesRefuseException()
    {
        var compte = new CompteUtilisateur(Guid.NewGuid(), "user@agirh.test", "hash-stocke", RoleType.RH, Guid.NewGuid(), Maintenant);
        var comptes = new Mock<ICompteUtilisateurRepository>();
        comptes.Setup(r => r.ObtenirParEmailAsync("user@agirh.test", It.IsAny<CancellationToken>())).ReturnsAsync(compte);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.VerifierMotDePasse(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var useCase = new AuthentifierUseCase(comptes.Object, hasher.Object);

        var act = () => useCase.ExecuterAsync("user@agirh.test", "mauvais-mot-de-passe");

        await act.Should().ThrowAsync<AccesRefuseException>();
    }

    [Fact]
    public async Task ExecuterAsync_CompteDesactive_LeveAccesRefuseException()
    {
        var compte = new CompteUtilisateur(Guid.NewGuid(), "user@agirh.test", "hash-stocke", RoleType.RH, Guid.NewGuid(), Maintenant);
        compte.Desactiver();
        var comptes = new Mock<ICompteUtilisateurRepository>();
        comptes.Setup(r => r.ObtenirParEmailAsync("user@agirh.test", It.IsAny<CancellationToken>())).ReturnsAsync(compte);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.VerifierMotDePasse(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var useCase = new AuthentifierUseCase(comptes.Object, hasher.Object);

        var act = () => useCase.ExecuterAsync("user@agirh.test", "motdepasse");

        await act.Should().ThrowAsync<AccesRefuseException>();
    }
}
