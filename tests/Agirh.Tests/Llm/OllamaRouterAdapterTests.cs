using Agirh.Core.Ports;
using Agirh.Infrastructure.Llm;
using FluentAssertions;

namespace Agirh.Tests.Llm;

/// <summary>
/// Tests d'integration reels contre une instance Ollama locale (localhost:11434, modele
/// phi4-mini:3.8b). Se termine sans assertion si Ollama n'est pas accessible ou si le modele
/// n'est pas installe, plutot que d'echouer bloquant la suite sur une machine fraiche.
/// </summary>
public class OllamaRouterAdapterTests
{
    private static async Task<bool> OllamaAvailableAsync()
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(3) };
            var response = await http.GetAsync("/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static OllamaRouterAdapter CreateAdapter() =>
        new(new OllamaClient(new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(60) }));

    [Theory]
    [InlineData("Quelle est la politique de mot de passe de l'entreprise ?", ConversationIntent.DocumentaryQuestion)]
    [InlineData("Où en est mon onboarding ?", ConversationIntent.CaseStatus)]
    [InlineData("Mon dossier est-il clôturé ?", ConversationIntent.CaseStatus)]
    [InlineData("Quel temps fait-il ?", ConversationIntent.OutOfScope)]
    public async Task ClassifyAsync_RepresentativeQuestions_ClassifiesCorrectly(string question, ConversationIntent expected)
    {
        if (!await OllamaAvailableAsync()) return;

        var adapter = CreateAdapter();

        var intent = await adapter.ClassifyAsync(question);

        intent.Should().Be(expected);
    }

    [Fact]
    public async Task ClassifyAsync_ModelUnavailable_ReturnsOutOfScopeByDefault()
    {
        // Pointe vers un port inutilise : simule une panne du service Ollama.
        var adapter = new OllamaRouterAdapter(new OllamaClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:1"), Timeout = TimeSpan.FromSeconds(2) }));

        var intent = await adapter.ClassifyAsync("Quelle est la politique de mot de passe ?");

        intent.Should().Be(ConversationIntent.OutOfScope, "un echec de communication ne doit jamais faire croire a tort a une intention exploitable");
    }
}
