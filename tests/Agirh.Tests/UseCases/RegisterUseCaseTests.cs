using System.Threading;
using Agirh.Core.Ports;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Agirh.Tests.UseCases;

public class RegisterUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    [Fact]
    public async Task ExecuteAsync_EmailAvailable_CreatesAnEmployeeAccount()
    {
        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(r => r.GetByEmailAsync("nouveau@agirh.test", It.IsAny<CancellationToken>())).ReturnsAsync((UserAccount?)null);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword("motdepasse123")).Returns("hash-genere");
        var useCase = new RegisterUseCase(accounts.Object, hasher.Object);

        var account = await useCase.ExecuteAsync("nouveau@agirh.test", "motdepasse123", Maintenant);

        account.Role.Should().Be(RoleType.Employee);
        account.DepartmentId.Should().BeNull();
        accounts.Verify(r => r.AddAsync(It.Is<UserAccount>(c => c.PasswordHash == "hash-genere"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmailAlreadyUsed_ThrowsInvalidOperationException()
    {
        var existant = new UserAccount(Guid.NewGuid(), "existant@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(r => r.GetByEmailAsync("existant@agirh.test", It.IsAny<CancellationToken>())).ReturnsAsync(existant);
        var hasher = new Mock<IPasswordHasher>();
        var useCase = new RegisterUseCase(accounts.Object, hasher.Object);

        var act = () => useCase.ExecuteAsync("existant@agirh.test", "motdepasse123", Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("court")]
    public async Task ExecuteAsync_PasswordTooShort_ThrowsArgumentException(string motDePasse)
    {
        var accounts = new Mock<IUserAccountRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var useCase = new RegisterUseCase(accounts.Object, hasher.Object);

        var act = () => useCase.ExecuteAsync("test@agirh.test", motDePasse, Maintenant);

        await act.Should().ThrowAsync<ArgumentException>();
        accounts.Verify(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
