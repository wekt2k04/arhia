using System.Text;
using Agirh.Core.Interfaces;
using Agirh.Core.Settings;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Data;
using Agirh.Infrastructure.MAF;
using Agirh.Infrastructure.Repositories;
using Agirh.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Agirh.Tests.Unit;

/// <summary>
/// Behavior tests for the FULL cognitive pipeline (correctifs 2.2 / P0-1 / P0-2).
///
/// Given a SalaryAdvance request whose amount is extracted by the (mocked)
/// profiler as a STRING ("2000")
/// When the real PreFlightValidator → real ZeroTrustDispatcher → real
/// WorkerExecutor → real DemanderAvanceSalaireAsync chain runs
/// Then the persisted entity carries a DECIMAL AmountRequested == 2000m
/// (P0-2: string→decimal conversion at the tool boundary) and Status == "Pending"
/// (P0-1: the VALIDATED parameters from PreFlight traverse the dispatcher).
/// </summary>
public class PipelineChainTests
{
    private const string SuccessDraft = "Demande créée.";

    // --------------------------------------------------------------------- //
    // Given — deterministic harness
    // --------------------------------------------------------------------- //

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IOptions<AIOptions> BuildOptions() => Options.Create(new AIOptions
    {
        MaxExtractionRetries = 2,
        MaxReflectionLoops = 2,
    });

    private static DynamicContextVector BuildVector(
        ConversationIntention intention,
        IReadOnlyDictionary<string, string?>? entities = null,
        float confidence = 0.95f) => new()
    {
        Intention = intention,
        ConfidenceScore = confidence,
        MainIdea = "demande liée à la conversation",
        Urgency = UrgencyLevel.Low,
        Tone = TonePreference.Professional,
        IsFollowUp = false,
        TriggerPhrase = "",
        ExtractedEntities = entities ?? new Dictionary<string, string?>(),
        ModelUsed = "profiler-mock",
    };

    private static HardState Collaborator(Guid userId) => new()
    {
        UserId = userId.ToString(),
        Role = "Collaborator",
        RoleFlag = RoleFlags.Collaborator,
        Email = "jean.dupont@agirh.test",
        FirstName = "Jean",
        LastName = "Dupont",
        IsActive = true,
    };

    private static AgentPipelineContext BuildContext(HardState identity) => new()
    {
        ConversationId = "conv-pipeline-chain-1",
        RecentMessages =
        [
            new RawMessage { Role = "user", Content = "Je veux une avance sur salaire", Timestamp = DateTime.UtcNow },
        ],
        Identity = identity,
    };

    private static AgentOrchestratorService BuildPipeline(
        AppDbContext context,
        Mock<ICognitiveProfiler> profiler,
        DynamicContextVector vector)
    {
        var employeeRepo = new EmployeeRepository(context);
        var leaveRepo = new LeaveRequestRepository(context);
        var payrollRepo = new PayrollProfileRepository(context);
        var advanceRepo = new SalaryAdvanceRepository(context);
        var unitOfWork = new UnitOfWork(
            context,
            employeeRepo,
            leaveRepo,
            payrollRepo,
            advanceRepo,
            new AgentConversationRepository(context));

        var tools = new IMafTool[]
        {
            new DemanderAvanceSalaireAsync(payrollRepo, advanceRepo, unitOfWork),
            new ConsulterSoldeTool(employeeRepo),
            new RevoquerAccesITTool(employeeRepo),
            new ApprouverDemandeCongesTool(leaveRepo, employeeRepo),
        };

        profiler
            .Setup(p => p.ExtractAsync(It.IsAny<CognitiveExtractionInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vector);

        var synthesizer = new Mock<ISynthesizerAgent>();
        synthesizer
            .Setup(s => s.DraftResponseAsync(It.IsAny<AgentPipelineContext>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessDraft);

        var checker = new Mock<ICheckerAgent>();
        checker
            .Setup(c => c.EvaluateAsync(It.IsAny<AgentPipelineContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, ""));

        return new AgentOrchestratorService(
            profiler.Object,
            new PreFlightValidator(),                                            // REAL validator
            new ZeroTrustDispatcher(tools, employeeRepo, leaveRepo, NullLogger<ZeroTrustDispatcher>.Instance), // REAL dispatcher
            new WorkerExecutor(tools, NullLogger<WorkerExecutor>.Instance),       // REAL worker + REAL tools
            synthesizer.Object,
            checker.Object,
            BuildOptions(),
            NullLogger<AgentOrchestratorService>.Instance);
    }

    private static async Task<Employee> AddEmployeeAsync(AppDbContext context)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "Jean",
            LastName = "Dupont",
            Email = $"jean.dupont.{Guid.NewGuid():N}@agirh.test",
            Role = "Collaborator",
            IsActive = true,
        };
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        return employee;
    }

    private static async Task<PayrollProfile> AddPayrollProfileAsync(AppDbContext context, Guid employeeId)
    {
        var profile = new PayrollProfile
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            NetSalary = 10000m,
            MaxAdvancePercentage = 0.50m,
            Iban = "FR7630006000011234567890189",
        };
        context.PayrollProfiles.Add(profile);
        await context.SaveChangesAsync();
        return profile;
    }

    private static async Task<string> ConsumeAsync(IAsyncEnumerable<string> stream, CancellationToken ct = default)
    {
        var builder = new StringBuilder();
        await foreach (var token in stream.WithCancellation(ct))
        {
            builder.Append(token);
        }
        return builder.ToString();
    }

    // --------------------------------------------------------------------- //
    // Happy path — string "2000" becomes decimal 2000m; Pending row persisted
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Persist_Decimal_Amount_And_Pending_Status_When_Amount_Is_A_String()
    {
        // Given a profiler that extracts amount="2000" as a STRING + the target employee
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context);
        await AddPayrollProfileAsync(context, employee.Id);

        var profiler = new Mock<ICognitiveProfiler>();
        var orchestrator = BuildPipeline(
            context,
            profiler,
            BuildVector(
                ConversationIntention.SalaryAdvance,
                new Dictionary<string, string?>
                {
                    ["amount"] = "2000",
                    ["employee_id"] = employee.Id.ToString(),
                }));

        // When the full chain runs
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext(Collaborator(employee.Id))));

        // Then the validated draft is streamed (human content + WIDGET + SUGGEST markers)…
        accumulated.Should().Contain(SuccessDraft);
        accumulated.Should().Contain("||WIDGET:SalaryAdvance:");
        accumulated.Should().Contain("||SUGGEST:Suivre ma demande||");

        // …and the tool received the VALIDATED string amount and persisted a DECIMAL 2000m (P0-2)
        var requests = context.SalaryAdvanceRequests.ToList();
        requests.Should().HaveCount(1);
        requests[0].EmployeeId.Should().Be(employee.Id);
        requests[0].AmountRequested.Should().Be(2000m);
        requests[0].Status.Should().Be("Pending");
    }

    // --------------------------------------------------------------------- //
    // Security — RBAC rejection crosses the whole pipeline; tool never runs
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Yield_Habilitation_Refusal_And_Not_Execute_Tool_When_Collaborator_Asks_It_Revocation()
    {
        // Given a Collaborator asking to revoke IT access (Admin-only tool)
        var context = CreateContext();
        var collaborator = await AddEmployeeAsync(context);
        var target = await AddEmployeeAsync(context);

        var profiler = new Mock<ICognitiveProfiler>();
        var orchestrator = BuildPipeline(
            context,
            profiler,
            BuildVector(
                ConversationIntention.ITAccessRevocation,
                new Dictionary<string, string?> { ["employee_id"] = target.Id.ToString() }));

        // When the pipeline runs
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext(Collaborator(collaborator.Id))));

        // Then the refusal message is streamed — prefixed by the RBAC denial
        // sentinel (Phase 6) so the controller can emit an SSE "denied" frame
        // with the exact polite message (never a "token")…
        accumulated.Should().StartWith(AgentOrchestratorService.DenialSentinel);
        accumulated[AgentOrchestratorService.DenialSentinel.Length..].Should().Be(
            "Je comprends que votre demande concerne l'action suivante : ITAccessRevocation. Cependant, vos habilitations actuelles (Collaborator) ne vous permettent pas d'y accéder.");

        // …and the Admin-only tool NEVER ran (target account stays active)
        var reloaded = context.Employees.Single(e => e.Id == target.Id);
        reloaded.IsActive.Should().BeTrue();
    }

    // --------------------------------------------------------------------- //
    // Failure — PreFlight rejects a missing amount; nothing is dispatched
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Ask_For_Missing_Amount_And_Create_Nothing()
    {
        // Given a profiler that extracts NO amount (only the employee_id)
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context);
        await AddPayrollProfileAsync(context, employee.Id);

        var profiler = new Mock<ICognitiveProfiler>();
        var orchestrator = BuildPipeline(
            context,
            profiler,
            BuildVector(
                ConversationIntention.SalaryAdvance,
                new Dictionary<string, string?> { ["employee_id"] = employee.Id.ToString() }));

        // When the pipeline runs
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext(Collaborator(employee.Id))));

        // Then the exact PreFlight message is streamed after the retry budget…
        accumulated.Should().Be(
            "Il me manque des informations pour procéder. Paramètres obligatoires manquants : montant souhaité de l'avance. Pouvez-vous me les préciser ?");

        // …the profiler was re-asked exactly MaxExtractionRetries times…
        profiler.Verify(
            p => p.ExtractAsync(It.IsAny<CognitiveExtractionInput>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // …and NOTHING was created in the database (no dispatch)
        context.SalaryAdvanceRequests.ToList().Should().BeEmpty();
    }
}
