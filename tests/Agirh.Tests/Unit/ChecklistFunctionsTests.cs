using System.Text.Json;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Agirh.Infrastructure.MAF;
using FluentAssertions;
using Xunit;

namespace Agirh.Tests.Unit;

/// <summary>
/// Behavior tests for <see cref="ChecklistFunctions"/> — messages EXACTS quand
/// la base est vide ou la catégorie inconnue (R4) — et mise en forme des items.
/// </summary>
public class ChecklistFunctionsTests
{
    private sealed class FakeChecklistRepo : IChecklistRepository
    {
        private readonly List<ChecklistItem> _items;
        public FakeChecklistRepo(params ChecklistItem[] items) => _items = items.ToList();

        public Task<IEnumerable<ChecklistItem>> GetAllAsync() => Task.FromResult(_items.AsEnumerable());

        public Task<IEnumerable<ChecklistItem>> GetByCategoryAsync(string category)
            => Task.FromResult(_items
                .Where(i => i.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .AsEnumerable());
    }

    private static async Task<string> RunAsync(IChecklistRepository repo, GenererChecklistInput input)
    {
        var tool = new ChecklistFunctions(repo);
        return await tool.ExecuteAsync(JsonSerializer.SerializeToElement(input), "u-1", CancellationToken.None);
    }

    [Fact]
    public async Task Should_Return_Exact_Message_When_Repo_Is_Empty()
    {
        // Given une base de checklist vide
        // When on génère la checklist complète
        var result = await RunAsync(new FakeChecklistRepo(), new GenererChecklistInput(null));

        // Then le message EXACT est retourné (jamais reformulé en "aucune liste")
        result.Should().Be("Aucune tâche de checklist trouvée.");
    }

    [Fact]
    public async Task Should_List_Available_Categories_For_Unknown_Category()
    {
        // Given un repo vide pour la catégorie "stagiaire"
        var repo = new FakeChecklistRepo(
            new ChecklistItem { Id = Guid.NewGuid(), Title = "A", Category = "IT", IsRequired = true, Order = 1 });

        // When on filtre sur une catégorie inconnue
        var result = await RunAsync(repo, new GenererChecklistInput("stagiaire"));

        // Then le message liste les catégories disponibles
        result.Should().Contain("Catégories disponibles : Administratif, IT, RH, Management");
    }

    [Fact]
    public async Task Should_Format_Checklist_Sorted_By_Category_And_Order()
    {
        // Given des items en vrac
        var repo = new FakeChecklistRepo(
            new ChecklistItem { Id = Guid.NewGuid(), Title = "Badge", Category = "Administratif", IsRequired = true, Order = 2 },
            new ChecklistItem { Id = Guid.NewGuid(), Title = "Contrat", Category = "Administratif", IsRequired = true, Order = 1 },
            new ChecklistItem { Id = Guid.NewGuid(), Title = "Compte", Category = "IT", IsRequired = false, Order = 1 });

        // When on génère la checklist complète
        var result = await RunAsync(repo, new GenererChecklistInput(null));

        // Then le nombre de tâches est annoncé et les items sont triés catégorie puis ordre
        result.Should().Contain("3 tâches");
        result.IndexOf("Contrat", StringComparison.Ordinal).Should().BeLessThan(result.IndexOf("Badge", StringComparison.Ordinal));
        result.Should().Contain("(obligatoire)");
        result.Should().Contain("(optionnel)");
    }
}
