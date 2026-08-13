using System.Text;
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
/// Behavior tests for <see cref="AgentOrchestratorService"/> (correctif 2.2).
///
/// Given a LeaveBalance request flowing through the full pipeline (fake profiler,
/// REAL <see cref="PreFlightValidator"/>, fake dispatcher/worker/synthesizer)
/// When the checker rejects the final draft after the reflection loop
/// Then the fallback message is streamed and the unvalidated draft is NEVER
/// streamed to the user.
/// </summary>
public class AgentOrchestratorServiceTests
{
    private const string Draft = "Réponse finale";

    // --------------------------------------------------------------------- //
    // Given — deterministic pipeline doubles
    // --------------------------------------------------------------------- //

    private static IOptions<AIOptions> BuildOptions() => Options.Create(new AIOptions
    {
        CheckerModel = "checker-test-model",
        ProfilerModel = "profiler-test-model",
        MaxReflectionLoops = 2,
        MaxExtractionRetries = 2,
    });

    private static DynamicContextVector LeaveBalanceVector() => new()
    {
        Intention = ConversationIntention.LeaveBalance,
        ConfidenceScore = 0.95f,
        MainIdea = "solde de congés",
        Urgency = UrgencyLevel.Low,
        Tone = TonePreference.Professional,
        IsFollowUp = false,
        TriggerPhrase = "",
        ExtractedEntities = new Dictionary<string, string?> { ["employee_id"] = "E-42" },
        ModelUsed = "profiler-test-model",
    };

    private static AgentPipelineContext BuildContext() => new()
    {
        ConversationId = "conv-orchestrator-1",
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
    };

    private static AgentOrchestratorService BuildOrchestrator(
        FakeChecker checker,
        FakeProfiler? profiler = null,
        FakeDispatcher? dispatcher = null,
        FakeWorker? worker = null,
        FakeSynthesizer? synthesizer = null)
    {
        var dispatchToTool = new DispatchToTool(new ToolDispatch
        {
            ToolName = "ConsulterSoldeAsync",
            SourceIntention = ConversationIntention.LeaveBalance,
            Parameters = new Dictionary<string, object?> { ["employeeId"] = "E-42" },
        });

        return new AgentOrchestratorService(
            profiler ?? new FakeProfiler(LeaveBalanceVector()),
            new PreFlightValidator(), // REAL validator: employee_id present ⇒ valid
            dispatcher ?? new FakeDispatcher(dispatchToTool),
            worker ?? new FakeWorker("Données"),
            synthesizer ?? new FakeSynthesizer(Draft),
            checker,
            BuildOptions(),
            NullLogger<AgentOrchestratorService>.Instance);
    }

    private static DynamicContextVector GreetingVector() => LeaveBalanceVector() with
    {
        Intention = ConversationIntention.Greeting,
        MainIdea = "salutation",
        ExtractedEntities = new Dictionary<string, string?>(),
    };

    private static DispatchToTool GreetingDispatch() => new(new ToolDispatch
    {
        ToolName = "GeneralChat",
        SourceIntention = ConversationIntention.Greeting,
        Parameters = new Dictionary<string, object?>(),
    });

    /// <summary>Contexte dont le dernier message utilisateur est une salutation.</summary>
    private static AgentPipelineContext BuildGreetingContext(string message = "Bonjour") => new()
    {
        ConversationId = "conv-orchestrator-greeting",
        RecentMessages =
        [
            new RawMessage { Role = "user", Content = message, Timestamp = DateTime.UtcNow },
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
    };

    /// <summary>When — consume the whole stream and accumulate the tokens.</summary>
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
    // 2.2 Case A — checker ALWAYS rejects ⇒ fallback message, draft NOT streamed
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Not_Stream_Draft_When_Checker_Always_Rejects()
    {
        // Given a checker that rejects every draft (simulates a failing 2.1 infra)
        var checker = new FakeChecker(new EvaluationResult(false, "corrige le ton"));
        var orchestrator = BuildOrchestrator(checker);

        // When the full pipeline runs
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));

        // Then the fallback message is streamed…
        accumulated.Should().Contain("Je n'ai pas pu générer une réponse validée");

        // …and NOT A SINGLE token of the unvalidated draft is streamed
        accumulated.Should().NotContain(Draft);
        accumulated.Should().NotContain("Réponse");
        accumulated.Should().NotContain("finale");
        accumulated.Should().NotContain("Données");

        // The reflection loop honoured AI:MaxReflectionLoops = 2
        checker.CallCount.Should().Be(2);
        checker.EvaluatedDrafts.Should().OnlyContain(draft => draft == Draft);
    }

    // --------------------------------------------------------------------- //
    // 2.2 Case B (regression) — checker accepts on first call ⇒ draft streamed
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Stream_Draft_When_Checker_Accepts_On_First_Evaluation()
    {
        // Given a checker that validates the very first draft
        var checker = new FakeChecker(new EvaluationResult(true, ""));
        var orchestrator = BuildOrchestrator(checker);

        // When the full pipeline runs
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));

        // Then the validated draft is streamed token by token (cumulated == draft + SUGGEST marker)
        accumulated.Should().Contain(Draft);
        accumulated.Should().Contain("||SUGGEST:Poser un congé||");
        checker.CallCount.Should().Be(1);
        checker.EvaluatedDrafts.Should().ContainSingle().Which.Should().Be(Draft);
    }

    // --------------------------------------------------------------------- //
    // Failure — a cancelled token aborts the pipeline instead of hanging
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Throw_OperationCanceledException_When_Token_Already_Cancelled()
    {
        // Given a caller that cancels before consuming the stream
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var orchestrator = BuildOrchestrator(new FakeChecker(new EvaluationResult(true, "")));

        // When the pipeline is enumerated with the cancelled token
        var act = () => ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext(), cts.Token));

        // Then the cancellation propagates instead of streaming anything
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --------------------------------------------------------------------- //
    // Greetings (option c) — l'agent déterministe GreetingClassifier a été
    // supprimé : une salutation passe par le profiler (Greeting) → GeneralChat
    // → worker (MainIdea) → synthétiseur → checker. Aucune réponse statique.
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Flow_Greeting_Through_Synthesizer_And_Checker_Without_Sentinel()
    {
        // Given un profiler qui détecte l'intention Greeting + un routage GeneralChat
        var profiler = new FakeProfiler(GreetingVector());
        var worker = new FakeWorker("salutation");
        var synthesizer = new FakeSynthesizer("Bonjour ! Comment puis-je vous aider ?");
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            profiler: profiler,
            dispatcher: new FakeDispatcher(GreetingDispatch()),
            worker: worker,
            synthesizer: synthesizer);

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildGreetingContext()));

        // Then le draft du synthétiseur est streamé…
        accumulated.Should().Contain("Bonjour ! Comment puis-je vous aider ?");

        // …chaque acteur est appelé exactement une fois (aucun court-circuit)…
        profiler.CallCount.Should().Be(1);
        worker.CallCount.Should().Be(1);
        synthesizer.CallCount.Should().Be(1);

        // …et aucune sentinelle de déni ni réponse statique n'apparaît
        accumulated.Should().NotContain(AgentOrchestratorService.DenialSentinel);
        accumulated.Should().NotContain("Je suis votre assistant RH");
    }

    [Fact]
    public async Task Should_Not_Stream_Greeting_Draft_When_Checker_Rejects()
    {
        // Given un checker qui rejette le draft de salutation
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(false, "corrige le ton")),
            profiler: new FakeProfiler(GreetingVector()),
            dispatcher: new FakeDispatcher(GreetingDispatch()),
            worker: new FakeWorker("salutation"),
            synthesizer: new FakeSynthesizer("Bonjour !"));
        var context = BuildGreetingContext();

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(context));

        // Then le draft n'est JAMAIS streamé et l'issue est NotStreamed
        accumulated.Should().Be("Je n'ai pas pu générer une réponse validée. Pouvez-vous reformuler votre demande ou réessayer ?");
        accumulated.Should().NotContain("Bonjour !");
        context.Outcome!.Outcome.Should().Be(PipelineOutcomeKind.NotStreamed);
        context.Outcome.CheckerValid.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Audit_Greeting_As_Success_With_LLM_Models()
    {
        // Given un flux de salutation complet validé par le checker
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            profiler: new FakeProfiler(GreetingVector()),
            dispatcher: new FakeDispatcher(GreetingDispatch()),
            worker: new FakeWorker("salutation"),
            synthesizer: new FakeSynthesizer("Bonjour !"));
        var context = BuildGreetingContext();

        // When le pipeline est consommé
        await ConsumeAsync(orchestrator.ProcessChatRequestAsync(context));

        // Then l'audit porte l'issue Success et les modèles réellement exécutés
        context.Outcome!.Outcome.Should().Be(PipelineOutcomeKind.Success);
        context.Outcome.Intention.Should().Be(ConversationIntention.Greeting.ToString());
        context.Outcome.ProfilerModel.Should().Be("profiler-test-model");
        context.Outcome.CheckerValid.Should().BeTrue();
        context.Outcome.Tool.Should().Be("GeneralChat");
    }

    [Fact]
    public async Task Should_Strip_Control_Characters_From_Draft_Before_Streaming()
    {
        // Given un draft (LLM compromis) qui contient la sentinelle de déni
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            synthesizer: new FakeSynthesizer("Réponse \u001fDENIED\u001f finale"));
        var context = BuildContext();

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(context));

        // Then le caractère de contrôle est retiré avant le streaming et l'issue reste Success
        accumulated.Should().NotContain("\u001f");
        accumulated.Should().NotContain(AgentOrchestratorService.DenialSentinel);
        context.Outcome!.Outcome.Should().Be(PipelineOutcomeKind.Success);
    }

    [Fact]
    public async Task Should_Process_Business_Message_Prefixed_With_Greeting_Through_Full_Pipeline()
    {
        // Given un message qui commence par une salutation mais porte une question RH
        var profiler = new FakeProfiler(LeaveBalanceVector());
        var checker = new FakeChecker(new EvaluationResult(true, ""));
        var orchestrator = BuildOrchestrator(checker, profiler: profiler);

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));

        // Then le profiler est appelé une fois et le draft est streamé (aucun court-circuit)
        profiler.CallCount.Should().Be(1);
        accumulated.Should().Contain(Draft);
    }

    // --------------------------------------------------------------------- //
    // Failure — IntentUnresolvable : le message du dispatcher remonte tel quel,
    // JAMAIS « Une erreur de routage »
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Surface_IntentUnresolvable_Message_Instead_Of_Routing_Error()
    {
        // Given un dispatcher qui ne peut pas router l'intention
        const string unresolvableMessage = "Votre demande n'a pas pu être rattachée à un service RH.";
        var dispatcher = new FakeDispatcher(new IntentUnresolvable(unresolvableMessage));
        var worker = new FakeWorker("Données");
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            dispatcher: dispatcher,
            worker: worker);

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));

        // Then le UserMessage du dispatcher est streamé verbatim…
        accumulated.Should().Be(unresolvableMessage);

        // …et le message d'erreur de routage n'apparaît JAMAIS
        accumulated.Should().NotContain("Une erreur de routage");
        worker.CallCount.Should().Be(0);
    }

    // --------------------------------------------------------------------- //
    // PHASE 6 — Déni RBAC flagué pour l'UI (événement SSE "denied")
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Prefix_Denied_Sentinel_When_Dispatch_Rejected()
    {
        // Given un dispatcher qui refuse l'accès (déni RBAC) avec un AccessDenied
        var denial = new AccessDenied
        {
            UserMessage = "Seuls les administrateurs peuvent révoquer des accès IT.",
            RequiredRole = "Admin",
            ActualRole = "Collaborator",
            DenialCode = DenialCode.INSUFFICIENT_ROLE,
            UserId = "u-1",
            Intention = "LeaveBalance",
            TimestampUtc = DateTime.UtcNow,
        };
        var dispatcher = new FakeDispatcher(new DispatchRejected(denial));
        var worker = new FakeWorker("Données");
        var synthesizer = new FakeSynthesizer(Draft);
        var checker = new FakeChecker(new EvaluationResult(true, ""));
        var orchestrator = BuildOrchestrator(checker, dispatcher: dispatcher, worker: worker, synthesizer: synthesizer);

        // When le pipeline est consommé
        var tokens = new List<string>();
        await foreach (var token in orchestrator.ProcessChatRequestAsync(BuildContext()))
        {
            tokens.Add(token);
        }

        // Then UN SEUL jeton est produit…
        tokens.Should().ContainSingle();

        // …commençant par la sentinelle de déni RBAC (Phase 6)…
        tokens[0].Should().StartWith(AgentOrchestratorService.DenialSentinel);

        // …et le message de refus poli EXISTANT suit la sentinelle, non modifié
        tokens[0][AgentOrchestratorService.DenialSentinel.Length..].Should().Be(
            "Je comprends que votre demande concerne l'action suivante : LeaveBalance. Cependant, vos habilitations actuelles (Collaborator) ne vous permettent pas d'y accéder.");

        // …et ni le worker, ni le synthétiseur, ni le checker ne sont appelés
        worker.CallCount.Should().Be(0);
        synthesizer.CallCount.Should().Be(0);
        checker.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_Not_Add_Sentinel_On_Other_Paths()
    {
        // Given une intention normale routée (LeaveBalance → ConsulterSoldeAsync)
        var checker = new FakeChecker(new EvaluationResult(true, ""));
        var orchestrator = BuildOrchestrator(checker);
        var routed = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));
        routed.Should().NotContain(AgentOrchestratorService.DenialSentinel);

        // Given un IntentUnresolvable (chemin "je n'ai pas compris")
        const string unresolvableMessage = "Votre demande n'a pas pu être rattachée à un service RH.";
        var unresolvableOrchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            dispatcher: new FakeDispatcher(new IntentUnresolvable(unresolvableMessage)),
            worker: new FakeWorker("Données"));
        var unresolvable = await ConsumeAsync(unresolvableOrchestrator.ProcessChatRequestAsync(BuildContext()));
        unresolvable.Should().NotContain(AgentOrchestratorService.DenialSentinel);

        // Given une salutation complète (profiler Greeting → GeneralChat → synth/checker)
        var greetingOrchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            profiler: new FakeProfiler(GreetingVector()),
            dispatcher: new FakeDispatcher(GreetingDispatch()),
            worker: new FakeWorker("salutation"),
            synthesizer: new FakeSynthesizer("Bonjour !"));
        var greeting = await ConsumeAsync(greetingOrchestrator.ProcessChatRequestAsync(BuildGreetingContext()));
        greeting.Should().NotContain(AgentOrchestratorService.DenialSentinel);

        // Given une erreur IA (fallback profiler "ModelUsed == fallback")
        var fallbackProfiler = new FakeProfiler(LeaveBalanceVector() with
        {
            Intention = ConversationIntention.Unknown,
            ModelUsed = "fallback",
        });
        var fallbackOrchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            profiler: fallbackProfiler);
        var fallback = await ConsumeAsync(fallbackOrchestrator.ProcessChatRequestAsync(BuildContext()));
        fallback.Should().NotContain(AgentOrchestratorService.DenialSentinel);
    }

    // --------------------------------------------------------------------- //
    // R4 — un résultat outil « Aucune… » est streamé VERBATIM, JAMAIS reformulé
    // en affirmation fausse par le synthétiseur
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Not_Reformulate_When_Worker_Returns_No_Results()
    {
        // Given un worker qui renvoie « Aucune tâche de checklist trouvée. »
        var synthesizer = new FakeSynthesizer("REFORMULATION FAUSSE : aucune liste d'onboarding n'existe");
        var checker = new FakeChecker(new EvaluationResult(true, ""));
        var orchestrator = BuildOrchestrator(checker, worker: new FakeWorker("Aucune tâche de checklist trouvée."), synthesizer: synthesizer);

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));

        // Then le message brut est streamé verbatim…
        accumulated.Should().Be("Aucune tâche de checklist trouvée.");

        // …et ni le synthétiseur ni le checker ne sont appelés (pas de reformulation)
        synthesizer.CallCount.Should().Be(0);
        checker.CallCount.Should().Be(0);
    }

    // --------------------------------------------------------------------- //
    // Lot E — branches de l'orchestrateur sans couverture (M1-M12)
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Stream_Reformulation_Request_When_Profiler_Unknown_Without_Fallback()
    {
        // Given un profiler qui renvoie Unknown SANS fallback (modèle réel, pas de panne)
        var profiler = new FakeProfiler(LeaveBalanceVector() with
        {
            Intention = ConversationIntention.Unknown,
            ModelUsed = "profiler-test-model",
        });
        var worker = new FakeWorker("Données");
        var orchestrator = BuildOrchestrator(new FakeChecker(new EvaluationResult(true, "")), profiler: profiler, worker: worker);
        var context = BuildContext();

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(context));

        // Then le message de reformulation exact est streamé, aucun worker appelé
        accumulated.Should().Be("Je ne suis pas sûr de comprendre. Pouvez-vous reformuler votre demande RH ?");
        context.Outcome!.Outcome.Should().Be(PipelineOutcomeKind.Unknown);
        context.Outcome.ProfilerModel.Should().Be("profiler-test-model");
        worker.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_Stream_Generic_AiUnavailable_Message_When_Profiler_Falls_Back_Without_Leaking_Raw_Data()
    {
        // Given un profiler en fallback (Ollama indisponible) avec un core_idea potentiellement sensible
        var profiler = new FakeProfiler(LeaveBalanceVector() with
        {
            Intention = ConversationIntention.Unknown,
            ModelUsed = "fallback",
            MainIdea = "donnée sensible à ne jamais exposer",
        });
        var worker = new FakeWorker("Données");
        var orchestrator = BuildOrchestrator(new FakeChecker(new EvaluationResult(true, "")), profiler: profiler, worker: worker);
        var context = BuildContext();

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(context));

        // Then le message générique exact est streamé, AUCUNE fuite de données brutes
        accumulated.Should().Be("Le service IA est temporairement indisponible. Veuillez réessayer dans quelques instants ou contacter le support technique.");
        context.Outcome!.Outcome.Should().Be(PipelineOutcomeKind.Fallback);
        context.Outcome.ProfilerModel.Should().Be("fallback");
        accumulated.Should().NotContain("donnée sensible");
        worker.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Erreur lors de l'opération.")]
    [InlineData("Accès refusé : droits insuffisants.")]
    [InlineData("Action non autorisée pour votre rôle.")]
    [InlineData("Aucun résultat trouvé.")]
    public async Task Should_Intercept_Worker_Error_Prefixes_And_Stream_Verbatim(string workerOutput)
    {
        // Given un worker qui renvoie un texte d'erreur préfixé (4 préfixes non testés)
        var synthesizer = new FakeSynthesizer("REFORMULATION INTERDITE");
        var checker = new FakeChecker(new EvaluationResult(true, ""));
        var orchestrator = BuildOrchestrator(checker, worker: new FakeWorker(workerOutput), synthesizer: synthesizer);

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));

        // Then le message brut est streamé verbatim, jamais reformulé
        accumulated.Should().Be(workerOutput);
        synthesizer.CallCount.Should().Be(0);
        checker.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_Record_NotStreamed_Outcome_When_Final_Draft_Invalid()
    {
        // Given un checker qui rejette toujours le draft
        var checker = new FakeChecker(new EvaluationResult(false, "corrige le ton"));
        var synthesizer = new FakeSynthesizer(Draft);
        var orchestrator = BuildOrchestrator(checker, synthesizer: synthesizer);
        var context = BuildContext();

        // When le pipeline est consommé
        await ConsumeAsync(orchestrator.ProcessChatRequestAsync(context));

        // Then l'audit porte l'issue NotStreamed + les champs de traçabilité
        context.Outcome!.Outcome.Should().Be(PipelineOutcomeKind.NotStreamed);
        context.Outcome.CheckerValid.Should().BeFalse();
        context.Outcome.ReflectionLoops.Should().Be(2);
        context.Outcome.CheckerModel.Should().Be("checker-test-model");
        synthesizer.Feedbacks.Should().Contain("corrige le ton");
    }

    [Fact]
    public async Task Should_Stream_Routing_Error_When_Dispatch_Result_Is_Unrecognized()
    {
        // Given un dispatcher qui renvoie un type de résultat non reconnu
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            dispatcher: new FakeDispatcher(new UnrecognizedDispatchResult()));
        var context = BuildContext();

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(context));

        // Then le message de routage défensif est streamé
        accumulated.Should().Be("Une erreur de routage est survenue.");
        context.Outcome!.Outcome.Should().Be(PipelineOutcomeKind.RoutingError);
    }

    [Fact]
    public async Task Should_Stream_Generation_Error_When_Final_Draft_Is_Empty()
    {
        // Given un synthétiseur qui produit un draft vide / espaces
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            synthesizer: new FakeSynthesizer("   "));

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));

        // Then le message d'erreur de génération est streamé
        accumulated.Should().Be("Erreur lors de la génération de la réponse finale.");
    }

    [Fact]
    public async Task Should_Truncate_RawData_To_500_Words_For_Synthesizer_And_Restore_Before_Marker_Extraction()
    {
        // Given un worker qui renvoie 600 mots + un marqueur WIDGET en fin
        var longText = string.Join(" ", Enumerable.Repeat("mot", 600));
        const string widgetMarker = "||WIDGET:SalaryAdvance:12345678-1234-1234-1234-123456789abc||";
        var synthesizer = new FakeSynthesizer("Réponse finale");
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            worker: new FakeWorker(longText + "\n" + widgetMarker),
            synthesizer: synthesizer);
        var context = BuildContext();

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(context));

        // Then le synthétiseur a reçu les 500 mots bornés suivis du marqueur " …" et de la note de troncature…
        synthesizer.LastRawWorkerData.Should().Contain(" …");
        synthesizer.LastRawWorkerData.Should().EndWith("[Note : la base de connaissances a retourné plus de résultats, mais seule une partie est visible ici en raison des contraintes de taille.]");

        // …mais le marqueur est ré-injecté depuis le rawData COMPLET restauré
        accumulated.Should().Contain(widgetMarker);
        context.Outcome!.Outcome.Should().Be(PipelineOutcomeKind.Success);
    }

    [Fact]
    public async Task Should_Not_Duplicate_Widget_Marker_When_Draft_Already_Contains_It()
    {
        // Given un draft du synthétiseur qui contient déjà le marqueur WIDGET
        const string marker = "||WIDGET:SalaryAdvance:12345678-1234-1234-1234-123456789abc||";
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            synthesizer: new FakeSynthesizer("Draft avec " + marker));

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));

        // Then le marqueur apparaît EXACTEMENT une fois (jamais dupliqué)
        accumulated.Split(marker, StringSplitOptions.None).Length.Should().Be(2);
    }

    [Fact]
    public async Task Should_Not_Reinject_Widget_Marker_With_Wrong_Guid_Length()
    {
        // Given un worker qui renvoie un marqueur au GUID non conforme (≠ 36 chars)
        const string shortMarker = "||WIDGET:SalaryAdvance:1234-56-78-90-abcdefghijklmnopqr||";
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            worker: new FakeWorker("Données\n" + shortMarker),
            synthesizer: new FakeSynthesizer("Réponse"));

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));

        // Then le marqueur non conforme n'est PAS ré-injecté
        accumulated.Should().NotContain(shortMarker);
    }

    [Fact]
    public async Task Should_Not_Append_Suggest_For_Non_Eligible_Intentions()
    {
        // Given une intention non éligible au SUGGEST (GeneralInquiry → GeneralChat)
        var profiler = new FakeProfiler(LeaveBalanceVector() with
        {
            Intention = ConversationIntention.GeneralInquiry,
            MainIdea = "bavardage",
        });
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            profiler: profiler,
            dispatcher: new FakeDispatcher(new DispatchToTool(new ToolDispatch
            {
                ToolName = "GeneralChat",
                SourceIntention = ConversationIntention.GeneralInquiry,
                Parameters = new Dictionary<string, object?>(),
            })),
            worker: new FakeWorker("bavardage"),
            synthesizer: new FakeSynthesizer("Réponse"));

        // When le pipeline est consommé
        var accumulated = await ConsumeAsync(orchestrator.ProcessChatRequestAsync(BuildContext()));

        // Then aucun marqueur SUGGEST n'est ajouté
        accumulated.Should().NotContain("||SUGGEST:");
    }

    [Fact]
    public async Task Should_Yield_Exactly_One_Token_Per_Word_With_Trailing_Space_Except_Last()
    {
        // Given un draft sans marqueur (GeneralChat) validé par le checker
        var profiler = new FakeProfiler(LeaveBalanceVector() with
        {
            Intention = ConversationIntention.GeneralInquiry,
            MainIdea = "bavardage",
        });
        var orchestrator = BuildOrchestrator(
            new FakeChecker(new EvaluationResult(true, "")),
            profiler: profiler,
            dispatcher: new FakeDispatcher(new DispatchToTool(new ToolDispatch
            {
                ToolName = "GeneralChat",
                SourceIntention = ConversationIntention.GeneralInquiry,
                Parameters = new Dictionary<string, object?>(),
            })),
            worker: new FakeWorker("bavardage"),
            synthesizer: new FakeSynthesizer("Bonjour monde test"));

        // When le pipeline est consommé jeton par jeton
        var tokens = new List<string>();
        await foreach (var token in orchestrator.ProcessChatRequestAsync(BuildContext()))
            tokens.Add(token);

        // Then un jeton par mot, espace de fin sauf au dernier, concat == draft
        tokens.Should().HaveCount(3);
        tokens[0].Should().Be("Bonjour ");
        tokens[1].Should().Be("monde ");
        tokens[2].Should().Be("test");
        string.Concat(tokens).Should().Be("Bonjour monde test");
    }

    // --------------------------------------------------------------------- //
    // Test double — DispatchResult non reconnu (branche RoutingError)
    // --------------------------------------------------------------------- //

    private sealed record UnrecognizedDispatchResult : DispatchResult;
}
