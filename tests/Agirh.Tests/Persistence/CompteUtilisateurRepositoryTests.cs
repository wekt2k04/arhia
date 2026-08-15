using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Persistence;
using Agirh.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Tests.Persistence;

public class CompteUtilisateurRepositoryTests
{
    private static DbContextOptions<AgirhDbContext> CreerOptions() =>
        new DbContextOptionsBuilder<AgirhDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task AjouterPuisObtenirParEmail_ReconstruitLeCompte()
    {
        var options = CreerOptions();
        var poleId = Guid.NewGuid();
        var compte = new CompteUtilisateur(Guid.NewGuid(), "RH@Agirh.Test", "hash-secret", RoleType.RH, poleId, new DateTime(2026, 8, 1));

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new CompteUtilisateurRepository(dbEcriture).AjouterAsync(compte);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new CompteUtilisateurRepository(dbLecture).ObtenirParEmailAsync("rh@agirh.test");

        recharge.Should().NotBeNull();
        recharge!.Role.Should().Be(RoleType.RH);
        recharge.PoleId.Should().Be(poleId);
        recharge.EstActif.Should().BeTrue();
    }

    [Fact]
    public async Task Desactiver_PuisRecharger_PersisteEstActifFalse()
    {
        var options = CreerOptions();
        var compte = new CompteUtilisateur(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.AdminQualite, null, new DateTime(2026, 8, 1));
        compte.Desactiver();

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new CompteUtilisateurRepository(dbEcriture).AjouterAsync(compte);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new CompteUtilisateurRepository(dbLecture).ObtenirParIdAsync(compte.Id);

        recharge!.EstActif.Should().BeFalse();
    }
}
