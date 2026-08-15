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
    private static async Task<bool> OllamaDisponibleAsync()
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(3) };
            var reponse = await http.GetAsync("/api/tags");
            return reponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static OllamaRouterAdapter CreerAdapter() =>
        new(new OllamaClient(new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(60) }));

    [Theory]
    [InlineData("Quelle est la politique de mot de passe de l'entreprise ?", IntentionConversation.QuestionDocumentaire)]
    [InlineData("Où en est mon onboarding ?", IntentionConversation.StatutDossier)]
    [InlineData("Mon dossier est-il clôturé ?", IntentionConversation.StatutDossier)]
    [InlineData("Quel temps fait-il ?", IntentionConversation.HorsPerimetre)]
    public async Task ClassifierAsync_QuestionsRepresentatives_ClassifieCorrectement(string question, IntentionConversation attendu)
    {
        if (!await OllamaDisponibleAsync()) return;

        var adapter = CreerAdapter();

        var intention = await adapter.ClassifierAsync(question);

        intention.Should().Be(attendu);
    }

    [Fact]
    public async Task ClassifierAsync_ModeleIndisponible_RetourneHorsPerimetreParDefaut()
    {
        // Pointe vers un port inutilise : simule une panne du service Ollama.
        var adapter = new OllamaRouterAdapter(new OllamaClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:1"), Timeout = TimeSpan.FromSeconds(2) }));

        var intention = await adapter.ClassifierAsync("Quelle est la politique de mot de passe ?");

        intention.Should().Be(IntentionConversation.HorsPerimetre, "un echec de communication ne doit jamais faire croire a tort a une intention exploitable");
    }
}
