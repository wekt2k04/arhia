using System.Security.Claims;
using System.Threading;
using Arhia.Api.Auth;
using Arhia.Core.Ports;
using Arhia.Domain;
using Arhia.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Arhia.Tests.Auth;

public class CurrentUserAccessorTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (CurrentUserAccessor accessor, Mock<IUserAccountRepository> accounts) CreateAccessor(UserAccount account)
    {
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()) }));
        var httpContext = new DefaultHttpContext { User = claims };
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        return (new CurrentUserAccessor(httpContextAccessor.Object, accounts.Object), accounts);
    }

    [Fact]
    public async Task GetActorAsync_ActiveAccount_ReturnsIt()
    {
        var account = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var (accessor, _) = CreateAccessor(account);

        var resultat = await accessor.GetActorAsync();

        resultat.Should().Be(account);
    }

    [Fact]
    public async Task GetActorAsync_DeactivatedAccount_ThrowsUnauthorizedAccessException()
    {
        var account = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        account.Deactivate();
        var (accessor, _) = CreateAccessor(account);

        var act = () => accessor.GetActorAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
