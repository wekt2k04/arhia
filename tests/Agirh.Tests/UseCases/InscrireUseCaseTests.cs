using System.Threading;
using Agirh.Core.Ports;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Agirh.Tests.UseCases;

public class InscrireUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    [Fact]
    public async Task ExecuterAsync_EmailDisponible_CreeUnCompteCollaborateur()
    {
        var comptes = new Mock<ICompteUtilisateurRepository>();
        comptes.Setup(r => r.ObtenirParEmailAsync("nouveau@agirh.test", It.IsAny<CancellationToken>())).ReturnsAsync((CompteUtilisateur?)null);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HacherMotDePasse("motdepasse123")).Returns("hash-genere");
        var useCase = new InscrireUseCase(comptes.Object, hasher.Object);

        var compte = await useCase.ExecuterAsync("nouveau@agirh.test", "motdepasse123", Maintenant);

        compte.Role.Should().Be(RoleType.Collaborateur);
        compte.PoleId.Should().BeNull();
        comptes.Verify(r => r.AjouterAsync(It.Is<CompteUtilisateur>(c => c.PasswordHash == "hash-genere"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuterAsync_EmailDejaUtilise_LeveInvalidOperationException()
    {
        var existant = new CompteUtilisateur(Guid.NewGuid(), "existant@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);
        var comptes = new Mock<ICompteUtilisateurRepository>();
        comptes.Setup(r => r.ObtenirParEmailAsync("existant@agirh.test", It.IsAny<CancellationToken>())).ReturnsAsync(existant);
        var hasher = new Mock<IPasswordHasher>();
        var useCase = new InscrireUseCase(comptes.Object, hasher.Object);

        var act = () => useCase.ExecuterAsync("existant@agirh.test", "motdepasse123", Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("court")]
    public async Task ExecuterAsync_MotDePasseTropCourt_LeveArgumentException(string motDePasse)
    {
        var comptes = new Mock<ICompteUtilisateurRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var useCase = new InscrireUseCase(comptes.Object, hasher.Object);

        var act = () => useCase.ExecuterAsync("test@agirh.test", motDePasse, Maintenant);

        await act.Should().ThrowAsync<ArgumentException>();
        comptes.Verify(r => r.ObtenirParEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
