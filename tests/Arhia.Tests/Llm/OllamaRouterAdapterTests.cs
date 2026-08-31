using Arhia.Core.Ports;
using Arhia.Infrastructure.Llm;
using FluentAssertions;

namespace Arhia.Tests.Llm;

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
    [InlineData("Bonjour", ConversationIntent.Greeting)]
    [InlineData("Quel temps fait-il ?", ConversationIntent.OutOfScope)]
    public async Task ClassifyAsync_RepresentativeQuestions_ClassifiesCorrectly(string question, ConversationIntent expected)
    {
        if (!await OllamaAvailableAsync()) return;

        var adapter = CreateAdapter();

        var intent = await adapter.ClassifyAsync(question);

        intent.Should().Be(expected);
    }

    [Fact]
    public async Task ClassifyAsync_AmbiguousMessage_NeverMisroutesToDataTouchingIntent()
    {
        // Cas volontairement hors de ClassifyAsync_RepresentativeQuestions_ClassifiesCorrectly : la
        // distinction Unknown/OutOfScope est intrinsequement floue (aucune des deux ne touche de
        // donnee) - une egalite stricte serait flaky sur un modele 3.8B jamais calibre sur ce cas
        // precis. Ce test verifie l'invariant qui compte cote securite : un message ambigu ne doit
        // jamais finir classe comme une intention qui touche une donnee reelle.
        if (!await OllamaAvailableAsync()) return;

        var adapter = CreateAdapter();

        var intent = await adapter.ClassifyAsync("Dossier ?");

        intent.Should().BeOneOf(new[] { ConversationIntent.Unknown, ConversationIntent.OutOfScope },
            "un message ambigu doit rester dans les intentions sans acces aux donnees, meme si le routeur hesite entre Unknown et OutOfScope");
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
