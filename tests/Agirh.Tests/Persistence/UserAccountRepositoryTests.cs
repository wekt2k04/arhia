using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Persistence;
using Agirh.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Tests.Persistence;

public class UserAccountRepositoryTests
{
    private static DbContextOptions<AgirhDbContext> CreerOptions() =>
        new DbContextOptionsBuilder<AgirhDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task AddThenGetByEmail_RebuildsTheAccount()
    {
        var options = CreerOptions();
        var departmentId = Guid.NewGuid();
        var account = new UserAccount(Guid.NewGuid(), "RH@Agirh.Test", "hash-secret", RoleType.HR, departmentId, new DateTime(2026, 8, 1));

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new UserAccountRepository(dbEcriture).AddAsync(account);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new UserAccountRepository(dbLecture).GetByEmailAsync("rh@agirh.test");

        recharge.Should().NotBeNull();
        recharge!.Role.Should().Be(RoleType.HR);
        recharge.DepartmentId.Should().Be(departmentId);
        recharge.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_ThenReload_PersistsIsActiveFalse()
    {
        var options = CreerOptions();
        var account = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, new DateTime(2026, 8, 1));
        account.Deactivate();

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new UserAccountRepository(dbEcriture).AddAsync(account);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new UserAccountRepository(dbLecture).GetByIdAsync(account.Id);

        recharge!.IsActive.Should().BeFalse();
    }
}
