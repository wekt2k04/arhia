using System.Threading;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Arhia.Domain;
using Arhia.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Arhia.Tests.UseCases;

public class AuthenticateUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    [Fact]
    public async Task ExecuteAsync_ValidCredentials_ReturnsTheAccount()
    {
        var account = new UserAccount(Guid.NewGuid(), "user@agirh.test", "hash-stocke", RoleType.HR, Guid.NewGuid(), Maintenant);
        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(r => r.GetByEmailAsync("user@agirh.test", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.VerifyPassword("motdepasse", "hash-stocke")).Returns(true);
        var useCase = new AuthenticateUseCase(accounts.Object, hasher.Object);

        var resultat = await useCase.ExecuteAsync("user@agirh.test", "motdepasse");

        resultat.Should().Be(account);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAccount_ThrowsAccessDeniedException()
    {
        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserAccount?)null);
        var hasher = new Mock<IPasswordHasher>();
        var useCase = new AuthenticateUseCase(accounts.Object, hasher.Object);

        var act = () => useCase.ExecuteAsync("inconnu@agirh.test", "motdepasse");

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuteAsync_WrongPassword_ThrowsAccessDeniedException()
    {
        var account = new UserAccount(Guid.NewGuid(), "user@agirh.test", "hash-stocke", RoleType.HR, Guid.NewGuid(), Maintenant);
        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(r => r.GetByEmailAsync("user@agirh.test", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var useCase = new AuthenticateUseCase(accounts.Object, hasher.Object);

        var act = () => useCase.ExecuteAsync("user@agirh.test", "mauvais-mot-de-passe");

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task ExecuteAsync_DeactivatedAccount_ThrowsAccessDeniedException()
    {
        var account = new UserAccount(Guid.NewGuid(), "user@agirh.test", "hash-stocke", RoleType.HR, Guid.NewGuid(), Maintenant);
        account.Deactivate();
        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(r => r.GetByEmailAsync("user@agirh.test", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var useCase = new AuthenticateUseCase(accounts.Object, hasher.Object);

        var act = () => useCase.ExecuteAsync("user@agirh.test", "motdepasse");

        await act.Should().ThrowAsync<AccessDeniedException>();
    }
}
