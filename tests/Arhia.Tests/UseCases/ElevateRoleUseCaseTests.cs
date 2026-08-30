using System.Threading;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Arhia.Domain;
using Arhia.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Arhia.Tests.UseCases;

public class ElevateRoleUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    [Fact]
    public async Task ExecuteAsync_QualityAdminActor_ElevatesTargetRole()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var cible = new UserAccount(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var newDepartmentId = Guid.NewGuid();
        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(r => r.GetByIdAsync(cible.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cible);
        var useCase = new ElevateRoleUseCase(accounts.Object);

        var resultat = await useCase.ExecuteAsync(admin, cible.Id, RoleType.HR, newDepartmentId);

        resultat.Role.Should().Be(RoleType.HR);
        resultat.DepartmentId.Should().Be(newDepartmentId);
        accounts.Verify(r => r.UpdateAsync(cible, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HRActor_ThrowsAccessDeniedException()
    {
        var hr = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var accounts = new Mock<IUserAccountRepository>();
        var useCase = new ElevateRoleUseCase(accounts.Object);

        var act = () => useCase.ExecuteAsync(hr, Guid.NewGuid(), RoleType.HR, Guid.NewGuid());

        await act.Should().ThrowAsync<AccessDeniedException>();
        accounts.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTarget_ThrowsInvalidOperationException()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserAccount?)null);
        var useCase = new ElevateRoleUseCase(accounts.Object);

        var act = () => useCase.ExecuteAsync(admin, Guid.NewGuid(), RoleType.HR, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_ToHRWithoutDepartment_ThrowsArgumentException()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var cible = new UserAccount(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);
        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(r => r.GetByIdAsync(cible.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cible);
        var useCase = new ElevateRoleUseCase(accounts.Object);

        var act = () => useCase.ExecuteAsync(admin, cible.Id, RoleType.HR, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
