using Agirh.Core.Interfaces;
using Agirh.Core.Settings;
using Agirh.Infrastructure.Services;
using Agirh.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agirh.Tests.Unit;

/// <summary>
/// Behavior tests for <see cref="ProfilerService"/> RAG extraction (correctif 2.3).
///
/// Given an Ollama profiler response for a KnowledgeSearch intent
/// When ExtractAsync maps it to a <see cref="DynamicContextVector"/>
/// Then the "query" entity is always populated for RAG: the extracted query is
/// prioritized, and the core_idea (truncated to 20 words) is the deterministic
/// C# fallback when the model omits it.
/// </summary>
public class ProfilerServiceTests
{
    // --------------------------------------------------------------------- //
    // Given — deterministic input and configuration
    // --------------------------------------------------------------------- //

    private static CognitiveExtractionInput BuildInput() => new()
    {
        ConversationId = "conv-profiler-1",
        RecentMessages =
        [
            new RawMessage { Role = "user", Content = "Quelles sont les règles de télétravail en vigueur ?", Timestamp = DateTime.UtcNow },
        ],
    };

    private static IOptions<AIOptions> BuildOptions() => Options.Create(new AIOptions
    {
        ProfilerModel = "profiler-test-model",
        MaxExtractionRetries = 2,
    });

    private static ProfilerService BuildAgent(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test") };
        return new ProfilerService(http, BuildOptions(), NullLogger<ProfilerService>.Instance);
    }

    private static string KnowledgeSearchPayload(string? query = null, string? coreIdea = "règles de télétravail en vigueur")
    {
        var entities = query is null
            ? "{}"
            : $$"""{"query":{{System.Text.Json.JsonSerializer.Serialize(query)}}}""";
        return $$"""
            {"intention":"KnowledgeSearch","confidence_score":0.95,"core_idea":{{System.Text.Json.JsonSerializer.Serialize(coreIdea)}},"extracted_entities":{{entities}}}
            """;
    }

    // --------------------------------------------------------------------- //
    // 2.3 — deterministic fallback when the model omits "query"
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Fallback_To_Core_Idea_For_KnowledgeSearch_Query_When_Query_Missing()
    {
        // Given a KnowledgeSearch profiler response WITHOUT an extracted query
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(KnowledgeSearchPayload(query: null))));

        // When the profile is extracted
        var result = await agent.ExtractAsync(BuildInput(), CancellationToken.None);

        // Then the query entity falls back deterministically to the core_idea
        result.Intention.Should().Be(ConversationIntention.KnowledgeSearch);
        result.ConfidenceScore.Should().BeApproximately(0.95f, 0.001f);
        result.ExtractedEntities["query"].Should().Be("règles de télétravail en vigueur");
    }

    // --------------------------------------------------------------------- //
    // 2.3 — extracted query is prioritized over the fallback
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Prefer_Extracted_Query_For_KnowledgeSearch()
    {
        // Given a KnowledgeSearch profiler response WITH an extracted query
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(
            KnowledgeSearchPayload(query: "règles de télétravail"))));

        // When the profile is extracted
        var result = await agent.ExtractAsync(BuildInput(), CancellationToken.None);

        // Then the extracted query wins over the core_idea fallback
        result.ExtractedEntities["query"].Should().Be("règles de télétravail");
    }

    // --------------------------------------------------------------------- //
    // Boundary — the deterministic fallback truncates core_idea to 20 words
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Truncate_Core_Idea_To_20_Words_For_Query_Fallback()
    {
        // Given a KnowledgeSearch core_idea longer than 20 words and no query
        var longCoreIdea = string.Join(" ", Enumerable.Range(1, 25).Select(i => $"mot{i}"));
        var expectedFallback = string.Join(" ", Enumerable.Range(1, 20).Select(i => $"mot{i}"));
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(
            KnowledgeSearchPayload(query: null, coreIdea: longCoreIdea))));

        // When the profile is extracted
        var result = await agent.ExtractAsync(BuildInput(), CancellationToken.None);

        // Then the query fallback and MainIdea are capped at exactly 20 words
        result.ExtractedEntities["query"].Should().Be(expectedFallback);
        result.MainIdea.Should().Be(expectedFallback);
    }

    // --------------------------------------------------------------------- //
    // Optional — a non-RAG intent does not populate a usable query
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Not_Populate_Query_For_Non_Rag_Intention()
    {
        // Given a LeaveBalance profiler response (non-RAG intent)
        const string payload = """
            {"intention":"LeaveBalance","confidence_score":0.95,"core_idea":"solde de congés","extracted_entities":{"employee_id":"E-42"}}
            """;
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(payload)));

        // When the profile is extracted
        var result = await agent.ExtractAsync(BuildInput(), CancellationToken.None);

        // Then no usable query is populated for the non-RAG intent
        result.Intention.Should().Be(ConversationIntention.LeaveBalance);
        result.ExtractedEntities.TryGetValue("query", out var query).Should().BeTrue();
        query.Should().BeNull();
    }

    // --------------------------------------------------------------------- //
    // Greetings — the "Greeting" intention is mapped by ParseIntention
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Map_Greeting_Intention()
    {
        // Given a profiler response with the Greeting intention
        const string payload = """
            {"intention":"Greeting","confidence_score":0.99,"core_idea":"salutation","extracted_entities":{}}
            """;
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(payload)));

        // When the profile is extracted
        var result = await agent.ExtractAsync(BuildInput(), CancellationToken.None);

        // Then the Greeting intention is mapped (2e ligne de défense de l'orchestrateur)
        result.Intention.Should().Be(ConversationIntention.Greeting);
        result.ConfidenceScore.Should().BeApproximately(0.99f, 0.001f);
    }

    // --------------------------------------------------------------------- //
    // Prompt — the request body teaches the model to route pure salutations
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Include_Greeting_Rules_And_Examples_In_Request()
    {
        // Given a profiler wired to a capturing transport stub
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(
            """{"intention":"Greeting","confidence_score":0.99,"core_idea":"salutation","extracted_entities":{}}"""));
        var agent = BuildAgent(handler);

        // When the profile is extracted
        await agent.ExtractAsync(BuildInput(), CancellationToken.None);

        // Then the request body contains the Greeting rule and the canonical examples.
        // The raw transport body escapes non-ASCII (R\u00E8gle 6) : the system
        // prompt is re-read from the JSON payload to assert the semantic content.
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        body.Should().Contain("Greeting");
        body.Should().Contain("Bonjour");

        var systemPrompt = System.Text.Json.JsonDocument.Parse(body)
            .RootElement.GetProperty("messages")[0]
            .GetProperty("content")
            .GetString();

        systemPrompt.Should().Contain("Règle 6");
    }

    // --------------------------------------------------------------------- //
    // R2 — règle de désambiguïsation « document interne → KnowledgeSearch »
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Include_Rag_Disambiguation_Rule_For_Reglement_Interieur()
    {
        // Given un profiler branché sur un stub capturant la requête
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(
            KnowledgeSearchPayload(query: "règlement intérieur sur les retards")));
        var agent = BuildAgent(handler);

        // When le profil est extrait
        await agent.ExtractAsync(BuildInput(), CancellationToken.None);

        // Then le prompt système contient la Règle 7 et l'exemple « règlement intérieur »
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        var systemPrompt = System.Text.Json.JsonDocument.Parse(body)
            .RootElement.GetProperty("messages")[0]
            .GetProperty("content")
            .GetString();

        systemPrompt.Should().Contain("Règle 7");
        systemPrompt.Should().Contain("règlement intérieur");
    }

    [Fact]
    public async Task Should_Force_KnowledgeSearch_When_GeneralInquiry_On_Document_Question()
    {
        // Given le modèle répond GeneralInquiry pour une question sur un document
        const string payload = """
            {"intention":"GeneralInquiry","confidence_score":0.85,"core_idea":"Règlement intérieur sur les retards","extracted_entities":{}}
            """;
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(payload)));
        var input = new CognitiveExtractionInput
        {
            ConversationId = "conv-profiler-rag-guard",
            RecentMessages =
            [
                new RawMessage { Role = "user", Content = "Que dit le règlement intérieur sur les retards ?", Timestamp = DateTime.UtcNow },
            ],
        };

        // When le profil est extrait
        var result = await agent.ExtractAsync(input, CancellationToken.None);

        // Then la garde déterministe force KnowledgeSearch (anti-hallucination)
        result.Intention.Should().Be(ConversationIntention.KnowledgeSearch);
        result.ExtractedEntities["query"]!.ToLowerInvariant().Should().Contain("règlement intérieur");
    }
}
