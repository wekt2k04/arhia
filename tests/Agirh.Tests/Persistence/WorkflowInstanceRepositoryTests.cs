using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Persistence;
using Agirh.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Tests.Persistence;

public class WorkflowInstanceRepositoryTests
{
    private static DbContextOptions<AgirhDbContext> CreerOptions() =>
        new DbContextOptionsBuilder<AgirhDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task AjouterCocherPuisRecharger_PersisteLetatDesItems()
    {
        var options = CreerOptions();
        var collaborateurId = Guid.NewGuid();
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Compte SELFRH créé");
        var instance = new WorkflowInstance(Guid.NewGuid(), collaborateurId, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, new DateTime(2026, 8, 15));
        var cochePar = Guid.NewGuid();
        instance.Cocher(item.Id, ItemEtat.Ok, cochePar, new DateTime(2026, 8, 16), "RAS");

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new WorkflowInstanceRepository(dbEcriture).AjouterAsync(instance);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new WorkflowInstanceRepository(dbLecture).ObtenirParIdAsync(instance.Id);

        recharge.Should().NotBeNull();
        recharge!.Items.Should().ContainSingle();
        var itemRecharge = recharge.Items[0];
        itemRecharge.Etat.Should().Be(ItemEtat.Ok);
        itemRecharge.CochePar.Should().Be(cochePar);
        itemRecharge.Commentaire.Should().Be("RAS");
    }

    [Fact]
    public async Task Cloturer_PuisRecharger_EstEnLectureSeule()
    {
        var options = CreerOptions();
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item unique");
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item }, new DateTime(2026, 8, 15));
        instance.Cocher(item.Id, ItemEtat.Ok, Guid.NewGuid(), new DateTime(2026, 8, 15), null);
        instance.Cloturer(new DateTime(2026, 8, 20));
        instance.Archiver();

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new WorkflowInstanceRepository(dbEcriture).AjouterAsync(instance);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new WorkflowInstanceRepository(dbLecture).ObtenirParIdAsync(instance.Id);

        recharge!.Statut.Should().Be(WorkflowStatus.Archive);
        var act = () => recharge.Cocher(item.Id, ItemEtat.Ko, Guid.NewGuid(), DateTime.UtcNow, null);
        act.Should().Throw<InvalidOperationException>();
    }
}
