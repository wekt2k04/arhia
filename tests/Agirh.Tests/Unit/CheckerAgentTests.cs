using System.Net;
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
/// Behavior tests for <see cref="CheckerAgent"/> (correctif 2.1 — FAIL-CLOSED).
///
/// Given an Ollama HTTP response (served by a transport stub, never real network)
/// When the checker evaluates a draft
/// Then every non-nominal outcome (empty, non-JSON, malformed JSON, network
/// failure) yields IsValid == false, and only the nominal path survives.
/// </summary>
public class CheckerAgentTests
{
    // --------------------------------------------------------------------- //
    // Given — deterministic context and configuration
    // --------------------------------------------------------------------- //

    private static AgentPipelineContext BuildContext() => new()
    {
        ConversationId = "conv-checker-1",
        RecentMessages =
        [
            new RawMessage { Role = "user", Content = "Quel est mon solde de congés ?", Timestamp = DateTime.UtcNow },
        ],
        Identity = new HardState
        {
            UserId = "u-1",
            Role = "Collaborator",
            RoleFlag = RoleFlags.Collaborator,
            Email = "jean.dupont@agirh.test",
            FirstName = "Jean",
            LastName = "Dupont",
            IsActive = true,
        },
        CognitiveContext = new DynamicContextVector
        {
            Intention = ConversationIntention.LeaveBalance,
            ConfidenceScore = 0.95f,
            MainIdea = "solde de congés",
            Urgency = UrgencyLevel.Low,
            Tone = TonePreference.Professional,
            IsFollowUp = false,
            TriggerPhrase = "",
            ExtractedEntities = new Dictionary<string, string?> { ["employee_id"] = "E-42" },
            ModelUsed = "profiler-test",
        },
        RawWorkerData = "solde: 12 jours",
    };

    private static IOptions<AIOptions> BuildOptions() => Options.Create(new AIOptions
    {
        CheckerModel = "checker-test-model",
        MaxReflectionLoops = 2,
        MaxExtractionRetries = 2,
    });

    private static CheckerAgent BuildAgent(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test") };
        return new CheckerAgent(http, BuildOptions(), NullLogger<CheckerAgent>.Instance);
    }

    private static async Task<EvaluationResult> EvaluateAsync(CheckerAgent agent)
        => await agent.EvaluateAsync(
            BuildContext(),
            draftResponse: "Votre solde de congés est de 12 jours.",
            CancellationToken.None);

    // --------------------------------------------------------------------- //
    // Failure — empty content (empty or null) → rejected, never 500
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_When_Ollama_Returns_Empty_Content()
    {
        // Given the checker model returns an empty evaluation payload
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload("")));

        // When the draft is evaluated
        var result = await EvaluateAsync(agent);

        // Then the draft is rejected with a "non validée" explanation
        result.IsValid.Should().BeFalse();
        result.ActionableFeedback.Should().Contain("non validée");
    }

    [Fact]
    public async Task Should_Reject_When_Ollama_Content_Is_Null()
    {
        // Given the checker model returns content: null
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(null)));

        // When the draft is evaluated
        var result = await EvaluateAsync(agent);

        // Then the draft is rejected
        result.IsValid.Should().BeFalse();
        result.ActionableFeedback.Should().Contain("non validée");
    }

    [Fact]
    public async Task Should_Reject_When_Ollama_Message_Is_Null()
    {
        // Given the checker model returns an envelope with a null message
        const string payload = """{"message":null,"done":true}""";
        var agent = BuildAgent(new StubHttpMessageHandler(payload));

        // When the draft is evaluated
        var result = await EvaluateAsync(agent);

        // Then the draft is rejected
        result.IsValid.Should().BeFalse();
        result.ActionableFeedback.Should().Contain("non validée");
    }

    // --------------------------------------------------------------------- //
    // Failure — content is not JSON at all
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_When_Ollama_Content_Is_Not_Json()
    {
        // Given the checker model returns plain text instead of JSON
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload("pas de json")));

        // When the draft is evaluated
        var result = await EvaluateAsync(agent);

        // Then the draft is rejected (JSON introuvable → fail-closed)
        result.IsValid.Should().BeFalse();
        result.ActionableFeedback.Should().Contain("non validée");
    }

    // --------------------------------------------------------------------- //
    // Failure — malformed evaluation JSON → JsonException is caught fail-closed
    // --------------------------------------------------------------------- //

    [Theory]
    [InlineData("""{"is_valid": tru}""")]
    [InlineData("""{is_valid: true}""")]
    public async Task Should_Reject_When_Evaluation_Json_Is_Malformed(string malformedJson)
    {
        // Given the checker model returns malformed JSON inside the Ollama envelope
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(malformedJson)));

        // When the draft is evaluated
        var result = await EvaluateAsync(agent);

        // Then the JsonException is caught and the draft is rejected
        result.IsValid.Should().BeFalse();
        result.ActionableFeedback.Should().Contain("non validée");
    }

    [Fact]
    public async Task Should_Reject_When_Is_Valid_Has_An_Invalid_Type()
    {
        // Given the checker model returns is_valid as a string ("banane") → cannot deserialize
        const string invalidJson = """{"is_valid": "banane", "actionable_feedback": ""}""";
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(invalidJson)));

        // When the draft is evaluated
        var result = await EvaluateAsync(agent);

        // Then the bool conversion failure is caught and the draft is rejected
        result.IsValid.Should().BeFalse();
        result.ActionableFeedback.Should().Contain("non validée");
    }

    // --------------------------------------------------------------------- //
    // Failure — provider unreachable → HttpRequestException is caught fail-closed
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_When_Ollama_Is_Unreachable()
    {
        // Given the HTTP transport throws (provider down / timeout)
        var agent = BuildAgent(new StubHttpMessageHandler(new HttpRequestException("connection refused")));

        // When the draft is evaluated
        var result = await EvaluateAsync(agent);

        // Then the exception is caught and the draft is rejected
        result.IsValid.Should().BeFalse();
        result.ActionableFeedback.Should().Contain("non validée");
    }

    // --------------------------------------------------------------------- //
    // Happy path — nominal evaluations pass through unchanged
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Accept_When_Ollama_Returns_Valid_Evaluation()
    {
        // Given the checker model validates the draft
        const string validJson = """{"is_valid": true, "actionable_feedback": ""}""";
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(validJson));
        var agent = BuildAgent(handler);

        // When the draft is evaluated
        var result = await EvaluateAsync(agent);

        // Then the draft is accepted and the feedback is empty
        result.IsValid.Should().BeTrue();
        result.ActionableFeedback.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Reject_With_Feedback_When_Ollama_Returns_Invalid_Evaluation()
    {
        // Given the checker model rejects the draft with actionable feedback
        const string invalidJson = """{"is_valid": false, "actionable_feedback": "corrige X"}""";
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(invalidJson)));

        // When the draft is evaluated
        var result = await EvaluateAsync(agent);

        // Then the rejection and the exact feedback are propagated
        result.IsValid.Should().BeFalse();
        result.ActionableFeedback.Should().Be("corrige X");
    }

    // --------------------------------------------------------------------- //
    // Transport shape — the checker talks to the configured model via POST /api/chat
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Call_Ollama_Chat_Endpoint_With_Configured_Model()
    {
        // Given a checker wired with AI:CheckerModel = checker-test-model
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload("""{"is_valid": true, "actionable_feedback": ""}"""));
        var agent = BuildAgent(handler);

        // When the draft is evaluated
        await EvaluateAsync(agent);

        // Then exactly one POST to /api/chat carrying the configured model was sent
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/api/chat");

        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        body.Should().Contain("checker-test-model");
        body.Should().Contain("LeaveBalance");
        body.Should().Contain("solde: 12 jours");
    }

    // --------------------------------------------------------------------- //
    // R1 — écho du draft long (document RAG) → fail-closed, jamais de JSON
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_When_Checker_Echoes_Long_Document_Instead_Of_Json()
    {
        // Given le modèle renvoie le draft/document RAG en écho (texte, zéro accolade)
        var echo =
            "**05 - Charte Collaborateur et Dispositions Annexes au Règlement Intérieur**\n\n" +
            "Le présent document, érigé en charte opposable et intégré de plein droit aux dispositions du Règlement Intérieur de l'entreprise AGIRH, " +
            "consolide, explicite et impose aux collaborateurs des politiques rigoureuses de conformité légale, normative et sécuritaire. " +
            string.Join(" ", Enumerable.Repeat(
                "Ces directives ont pour objet principal de consolider, d'expliciter et d'imposer aux collaborateurs des politiques rigoureuses de conformité.",
                20));
        var agent = BuildAgent(new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(echo)));

        // When le draft est évalué
        var result = await EvaluateAsync(agent);

        // Then le draft est rejeté (fail-closed : JSON introuvable dans l'écho)
        result.IsValid.Should().BeFalse();
        result.ActionableFeedback.Should().Contain("non validée");
    }

    [Fact]
    public async Task Should_Use_Bounded_Token_Budget_And_Strict_Prompt()
    {
        // Given un checker branché sur un stub qui capture la requête
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.OllamaPayload(
            """{"is_valid": true, "actionable_feedback": ""}"""));
        var agent = BuildAgent(handler);

        // When le draft est évalué
        await EvaluateAsync(agent);

        // Then le payload respecte : format:json racine + think:false racine + budget 256.
        // format:json est OBLIGATOIRE — sans lui phi4-mini répond en texte libre → fail-closed.
        // think:false en racine (pas dans options) — un modèle reasoning ignorerait la clé
        // dans options et consommerait tout num_predict en raisonnement → content vide.
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        body.Should().Contain("\"format\":\"json\"");
        body.Should().Contain("\"num_predict\":256");
        body.Should().Contain("\"think\":false");
        body.Should().NotContain("\"options\":{\"temperature\":0,\"num_predict\":256,\"think\":false}");
        body.Should().Contain("Interdiction de recopier");
        body.Should().Contain("is_valid");
    }
}
