using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Agirh.Api.Controllers;
using Agirh.Api.Logging;
using Agirh.Core.Interfaces;
using Agirh.Core.Settings;
using Agirh.Infrastructure.Data;
using Agirh.Infrastructure.Repositories;
using Agirh.Infrastructure.Services;
using Agirh.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Agirh.Tests.Unit;

/// <summary>
/// Behavior tests for the PHASE 6 SSE contract: when the ZeroTrustDispatcher
/// rejects a chat request (RBAC denial), <see cref="AgentController.ChatAsync"/>
/// must emit a SINGLE <c>{"type":"denied","data":"&lt;message poli&gt;"}</c> frame
/// instead of a <c>token</c> frame, then terminate the stream — and no control
/// character (the denial sentinel) may ever leak to the UI, even defensively.
///
/// The controller is instantiated directly with a constructed ClaimsPrincipal and
/// the REAL <see cref="HardStateExtractor"/>; the REAL orchestrator runs against
/// deterministic pipeline doubles (FakeProfiler/FakeDispatcher/FakeWorker/…). The
/// SSE response body is captured on a MemoryStream and parsed frame by frame.
/// </summary>
public class AgentControllerDeniedTests
{
    private sealed record SseFrame(string Type, string Data);

    private const string ExpectedDenialMessage =
        "Je comprends que votre demande concerne l'action suivante : LeaveBalance. Cependant, vos habilitations actuelles (Collaborator) ne vous permettent pas d'y accéder.";

    // --------------------------------------------------------------------- //
    // Given — deterministic harness
    // --------------------------------------------------------------------- //

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IOptions<AIOptions> BuildOptions() => Options.Create(new AIOptions
    {
        CheckerModel = "checker-test-model",
        ProfilerModel = "profiler-test-model",
        MaxReflectionLoops = 2,
        MaxExtractionRetries = 2,
    });

    private static IConfiguration BuildControllerConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["AI:Endpoint"] = "http://ollama.test" })
        .Build();

    private static DynamicContextVector LeaveBalanceVector() => new()
    {
        Intention = ConversationIntention.LeaveBalance,
        ConfidenceScore = 0.95f,
        MainIdea = "congés de Marie",
        Urgency = UrgencyLevel.Low,
        Tone = TonePreference.Professional,
        IsFollowUp = false,
        TriggerPhrase = "",
        ExtractedEntities = new Dictionary<string, string?> { ["employee_id"] = "E-42" },
        ModelUsed = "profiler-test-model",
    };

    private static ClaimsPrincipal BuildPrincipal(string role, Guid? userId = null)
    {
        // Le JWT réel porte un NameIdentifier au format GUID — le contrôleur le
        // parse (Guid.Parse) pour posséder la conversation.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Email, "jean.dupont@agirh.test"),
            new(ClaimTypes.GivenName, "Jean"),
            new(ClaimTypes.Surname, "Dupont"),
            new("is_active", "true"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test-auth"));
    }

    private static AgentController BuildController(AppDbContext context, ClaimsPrincipal principal, DispatchResult dispatchResult)
    {
        var unitOfWork = new UnitOfWork(
            context,
            new EmployeeRepository(context),
            new LeaveRequestRepository(context),
            new PayrollProfileRepository(context),
            new SalaryAdvanceRepository(context),
            new AgentConversationRepository(context));

        // Orchestrateur RÉEL (profiler/validator/worker/synthesizer/checker réels
        // ou faux déterministes) ; le seul choix du scénario est le résultat du
        // dispatcher — deny (DispatchRejected) ou allow (DispatchToTool).
        var orchestrator = new AgentOrchestratorService(
            new FakeProfiler(LeaveBalanceVector()),
            new PreFlightValidator(),
            new FakeDispatcher(dispatchResult),
            new FakeWorker("Données"),
            new FakeSynthesizer("Réponse finale"),
            new FakeChecker(new EvaluationResult(true, "")),
            BuildOptions(),
            NullLogger<AgentOrchestratorService>.Instance);

        var controller = new AgentController(
            orchestrator,
            new HardStateExtractor(), // REAL extractor — reads JWT claims
            unitOfWork,
            new Mock<IHttpClientFactory>().Object, // jamais appelé sur /chat
            new NoopChatAuditLogger(),
            BuildControllerConfiguration(),
            NullLogger<AgentController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
        return controller;
    }

    private static DispatchRejected BuildRbacDenial() => new(new AccessDenied
    {
        UserMessage = "Seuls les administrateurs peuvent révoquer des accès IT.",
        RequiredRole = "Admin",
        ActualRole = "Collaborator",
        DenialCode = DenialCode.INSUFFICIENT_ROLE,
        UserId = "u-1",
        Intention = "LeaveBalance",
        TimestampUtc = DateTime.UtcNow,
    });

    private static DispatchToTool BuildAllowedDispatch() => new(new ToolDispatch
    {
        ToolName = "ConsulterSoldeAsync",
        SourceIntention = ConversationIntention.LeaveBalance,
        Parameters = new Dictionary<string, object?> { ["employeeId"] = "E-42" },
    });

    // When — consume the SSE stream produced by ChatAsync
    private static async Task<List<SseFrame>> ActChatAsync(AgentController controller, string message)
    {
        var responseBody = new MemoryStream();
        controller.HttpContext!.Response.Body = responseBody;

        await controller.ChatAsync(new ChatRequest { Message = message }, CancellationToken.None);

        return ParseSseFrames(Encoding.UTF8.GetString(responseBody.ToArray()));
    }

    private static List<SseFrame> ParseSseFrames(string raw)
    {
        var frames = new List<SseFrame>();
        foreach (var rawFrame in raw.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawFrame.Trim();
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var json = line["data:".Length..].Trim();
            using var document = JsonDocument.Parse(json);
            var type = document.RootElement.GetProperty("type").GetString();
            var data = document.RootElement.GetProperty("data").GetString();
            frames.Add(new SseFrame(type!, data!));
        }

        return frames;
    }

    // ===================================================================== //
    // PHASE 6 — déni RBAC : frame "denied", jamais de "token", flux terminé
    // ===================================================================== //

    [Fact]
    public async Task ChatAsync_Should_Emit_Denied_Frame_When_Dispatch_Rejected()
    {
        // Given un Collaborator (Jean) qui demande à voir les congés de Marie
        // → le dispatcher refuse l'accès (déni RBAC)
        var context = CreateContext();
        var controller = BuildController(context, BuildPrincipal("Collaborator"), BuildRbacDenial());

        // When le flux SSE de ProcessChat est consommé
        var frames = await ActChatAsync(controller, "Je veux voir les congés de Marie");

        // Then UNE frame "denied" est émise avec le message poli EXISTANT…
        frames.Should().ContainSingle(f => f.Type == "denied");
        frames.Single(f => f.Type == "denied").Data.Should().Be(ExpectedDenialMessage);

        // …et le flux se termine immédiatement après : conversation + denied,
        // AUCUNE frame token, AUCUNE frame done.
        frames.Select(f => f.Type).Should().Equal("conversation", "denied");
        frames.Should().HaveCount(2);

        // …et aucun caractère de contrôle (sentinelle) ne fuit vers l'UI,
        // ni dans la frame denied ni ailleurs dans le corps SSE.
        frames.Single(f => f.Type == "denied").Data.Should().NotContain(AgentOrchestratorService.DenialSentinel);
    }

    [Fact]
    public async Task ChatAsync_Should_Persist_Polite_Message_Without_Sentinel()
    {
        // Given un déni RBAC sur la conversation de Jean
        var context = CreateContext();
        var controller = BuildController(context, BuildPrincipal("Collaborator"), BuildRbacDenial());

        // When le flux SSE est consommé
        await ActChatAsync(controller, "Je veux voir les congés de Marie");

        // Then l'historique persisté contient le message de refus poli, jamais la
        // sentinelle (l'état observé est vérifié en base, pas seulement la réponse)
        var saved = context.AgentConversations.Include(c => c.Messages).Single();
        saved.Messages.Should().HaveCount(2); // user + assistant
        var assistant = saved.Messages.Single(m => m.Role == "assistant");
        assistant.Content.Should().Be(ExpectedDenialMessage);
        assistant.Content.Should().NotContain(AgentOrchestratorService.DenialSentinel);
    }

    // ===================================================================== //
    // Régression — le flux "token"/"done" normal reste INCHANGÉ par Phase 6
    // ===================================================================== //

    [Fact]
    public async Task ChatAsync_Should_Keep_Token_And_Done_Frames_When_Dispatch_Allowed()
    {
        // Given un Collaborator dont la demande est autorisée (routage normal)
        var context = CreateContext();
        var controller = BuildController(context, BuildPrincipal("Collaborator"), BuildAllowedDispatch());

        // When le flux SSE est consommé
        var frames = await ActChatAsync(controller, "Quel est mon solde de congés ?");

        // Then les frames conversation → token* → done sont émises comme avant…
        frames[0].Type.Should().Be("conversation");
        frames.Where(f => f.Type == "token").Should().NotBeEmpty();
        frames[^1].Type.Should().Be("done");

        // …et AUCUNE sentinelle de déni ne pollue le flux normal
        frames.Where(f => f.Type == "token").Should().OnlyContain(f => !f.Data.Contains(AgentOrchestratorService.DenialSentinel));
    }

    // ===================================================================== //
    // Durcissement B1 — la sentinelle n'est honorée que si Outcome == Denied
    // ===================================================================== //

    [Fact]
    public async Task ChatAsync_Should_Not_Emit_Denied_Frame_When_Sentinel_Prefix_Comes_From_Non_Denied_Path()
    {
        // Given un IntentUnresolvable dont le message (écho LLM) commence par la
        // sentinelle — l'orchestrateur ne pose JAMAIS Outcome.Denied sur ce chemin.
        var controller = BuildController(
            CreateContext(),
            BuildPrincipal("Collaborator"),
            new IntentUnresolvable(AgentOrchestratorService.DenialSentinel + "Votre demande n'a pas pu être rattachée à un service RH."));

        // When le flux SSE est consommé
        var frames = await ActChatAsync(controller, "Bonjour");

        // Then AUCUNE frame "denied" n'est émise (la bulle orange est réservée aux
        // vrais dénis RBAC) — on retombe sur le flux token + done, sentinelle retirée.
        frames.Select(f => f.Type).Should().Equal("conversation", "token", "done");
        frames.Should().NotContain(f => f.Type == "denied");
        frames.Where(f => f.Type == "token").Should().OnlyContain(f => !f.Data.Contains(AgentOrchestratorService.DenialSentinel));
    }
}
