using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agirh.Core.Interfaces;
using Agirh.Core.Models;
using Agirh.Core.Settings;

// PHASE 6 — la sentinelle de refus RBAC est internal : consommée par l'assembly
// Agirh.Api (AgentController → événement SSE "denied") et par Agirh.Tests
// (assertions). Le seul autre membre internal de l'assembly est
// AppDbContext.SerializeEmbedding (aucun élargissement de surface sensible).
[assembly: InternalsVisibleTo("Agirh.Api")]
[assembly: InternalsVisibleTo("Agirh.Tests")]

namespace Agirh.Infrastructure.Services;

public sealed class AgentOrchestratorService : IAgentOrchestratorService
{
    private readonly ICognitiveProfiler _profiler;
    private readonly IPreFlightValidator _validator;
    private readonly IZeroTrustDispatcher _dispatcher;
    private readonly IWorkerExecutor _worker;
    private readonly ISynthesizerAgent _synthesizer;
    private readonly ICheckerAgent _checker;
    private readonly ILogger<AgentOrchestratorService> _logger;
    private readonly int _maxExtractionRetries;
    private readonly int _maxReflectionLoops;
    private readonly string _profilerModel;
    private readonly string _synthesizerModel;
    private readonly string _checkerModel;
    private readonly string _embeddingModel;

    /// <summary>
    /// PHASE 6 — Sentinelle de refus RBAC. Préfixe le message de refus poli
    /// streamé par l'orchestrateur quand le ZeroTrustDispatcher refuse l'accès
    /// (DispatchRejected / AccessDenied). Le contrôleur transforme ce jeton en
    /// frame SSE {"type":"denied","data":"&lt;message poli&gt;"} (bulle d'alerte
    /// orange côté UI) au lieu d'un "token". Cette sentinelle ne doit JAMAIS être
    /// ajoutée sur IntentUnresolvable, erreurs IA/fallback ni sur tout autre
    /// chemin : la bulle orange n'est déclenchée que par un déni RBAC.
    /// </summary>
    internal const string DenialSentinel = "\u001fDENIED\u001f";

    public AgentOrchestratorService(
        ICognitiveProfiler profiler, IPreFlightValidator validator, IZeroTrustDispatcher dispatcher,
        IWorkerExecutor worker, ISynthesizerAgent synthesizer, ICheckerAgent checker,
        IOptions<AIOptions> options, ILogger<AgentOrchestratorService> logger)
    {
        _profiler = profiler; _validator = validator; _dispatcher = dispatcher;
        _worker = worker; _synthesizer = synthesizer; _checker = checker; _logger = logger;
        _maxExtractionRetries = Math.Max(1, options.Value.MaxExtractionRetries);
        _maxReflectionLoops = options.Value.MaxReflectionLoops;
        _profilerModel = options.Value.ProfilerModel;
        _synthesizerModel = string.IsNullOrWhiteSpace(options.Value.SynthesizerModel)
            ? options.Value.ProfilerModel
            : options.Value.SynthesizerModel;
        _checkerModel = string.IsNullOrWhiteSpace(options.Value.CheckerModel)
            ? options.Value.WorkerModel
            : options.Value.CheckerModel;
        _embeddingModel = options.Value.EmbeddingModel;
    }

    public async IAsyncEnumerable<string> ProcessChatRequestAsync(AgentPipelineContext context, [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Résumé structuré pour le log d'audit (rempli au fil des branches).
        var outcome = context.Outcome ??= new AgentPipelineOutcome();

        var extractionAttempt = 0;
        ValidationResult validation = null!;
        string toolName = "GeneralChat";
        long profilerMs = 0;
        long validatorMs = 0;

        while (extractionAttempt < _maxExtractionRetries)
        {
            extractionAttempt++;
            var swP = Stopwatch.StartNew();
            context.CognitiveContext = await _profiler.ExtractAsync(
                new CognitiveExtractionInput { RecentMessages = context.RecentMessages, ConversationId = context.ConversationId }, ct);
            profilerMs = swP.ElapsedMilliseconds;

            if (context.CognitiveContext.Value.Intention == ConversationIntention.Unknown)
            {
                var cognitive = context.CognitiveContext.Value;
                if (string.Equals(cognitive.ModelUsed, "fallback", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("AI_UNAVAILABLE — Intention: {I}, Confidence: {C:0.000}, ModelUsed: {M}, TimeMs: {T}",
                        cognitive.Intention, cognitive.ConfidenceScore, cognitive.ModelUsed, cognitive.ProcessingTimeMs);
                    outcome.Outcome = PipelineOutcomeKind.Fallback;
                    outcome.Intention = cognitive.Intention.ToString();
                    outcome.Confidence = cognitive.ConfidenceScore;
                    outcome.ProfilerModel = "fallback";
                    yield return "Le service IA est temporairement indisponible. Veuillez réessayer dans quelques instants ou contacter le support technique.";
                    yield break;
                }

                _logger.LogWarning("INTENT_UNKNOWN — Intention: {I}, Confidence: {C:0.000}, MainIdea: \"{M}\", TimeMs: {T}",
                    cognitive.Intention, cognitive.ConfidenceScore, cognitive.MainIdea, cognitive.ProcessingTimeMs);
                outcome.Outcome = PipelineOutcomeKind.Unknown;
                outcome.Intention = cognitive.Intention.ToString();
                outcome.Confidence = cognitive.ConfidenceScore;
                outcome.ProfilerModel = cognitive.ModelUsed;
                yield return "Je ne suis pas sûr de comprendre. Pouvez-vous reformuler votre demande RH ?";
                yield break;
            }

            toolName = RbacMatrix.Default.ResolveTool(context.CognitiveContext.Value.Intention.ToString()) ?? "GeneralChat";
            var swV = Stopwatch.StartNew();
            validation = _validator.ValidateParameters(toolName, context.CognitiveContext.Value.ExtractedEntities);
            validatorMs = swV.ElapsedMilliseconds;

            if (validation.IsValid) { context.ValidatedParameters = validation.ValidatedJson; break; }

            if (extractionAttempt >= _maxExtractionRetries)
            {
                outcome.Outcome = PipelineOutcomeKind.ValidationClarification;
                outcome.Intention = context.CognitiveContext.Value.Intention.ToString();
                outcome.Confidence = context.CognitiveContext.Value.ConfidenceScore;
                outcome.ProfilerModel = context.CognitiveContext.Value.ModelUsed;
                outcome.Tool = toolName;
                yield return $"Il me manque des informations pour procéder. {validation.ErrorMessage} Pouvez-vous me les préciser ?";
                yield break;
            }
        }

        var swDispatcher = Stopwatch.StartNew();
        var dispatchResult = await _dispatcher.DispatchAsync(
            new DispatchInput { CognitiveContext = context.CognitiveContext!.Value, Identity = context.Identity, ValidatedParameters = validation.ValidatedJson }, ct);
        swDispatcher.Stop();

        if (dispatchResult switch { DispatchRejected r => r, _ => null } is DispatchRejected rejection)
        {
            // PHASE 6 — Déni RBAC flagué pour l'UI : ce jeton commence par la
            // sentinelle ; le contrôleur émet une frame SSE "denied" (message poli,
            // sans sentinelle) et termine le flux. Le message reste EXACTEMENT
            // celui d'avant — aucune modification, aucun détail technique ajouté.
            outcome.Outcome = PipelineOutcomeKind.Denied;
            outcome.Intention = context.CognitiveContext!.Value.Intention.ToString();
            outcome.Confidence = context.CognitiveContext.Value.ConfidenceScore;
            outcome.ProfilerModel = context.CognitiveContext.Value.ModelUsed;
            outcome.Tool = toolName;
            yield return DenialSentinel + $"Je comprends que votre demande concerne l'action suivante : {context.CognitiveContext!.Value.Intention}. Cependant, vos habilitations actuelles ({context.Identity.Role}) ne vous permettent pas d'y accéder.";
            yield break;
        }

        if (dispatchResult is IntentUnresolvable unresolvable)
        {
            _logger.LogInformation("IntentUnresolvable — intention non routable : {UserMessage}", unresolvable.UserMessage);
            outcome.Outcome = PipelineOutcomeKind.Unknown;
            outcome.Intention = context.CognitiveContext!.Value.Intention.ToString();
            outcome.ProfilerModel = context.CognitiveContext.Value.ModelUsed;
            yield return unresolvable.UserMessage;
            yield break;
        }

        if (dispatchResult is not DispatchToTool dispatchToTool)
        {
            outcome.Outcome = PipelineOutcomeKind.RoutingError;
            outcome.Intention = context.CognitiveContext!.Value.Intention.ToString();
            outcome.ProfilerModel = context.CognitiveContext.Value.ModelUsed;
            yield return "Une erreur de routage est survenue.";
            yield break;
        }

        var swWorker = Stopwatch.StartNew();
        var rawDataBuilder = new System.Text.StringBuilder();
        await foreach (var chunk in _worker.ExecuteAsync(
            new ExecutionInput { Dispatch = dispatchToTool.Dispatch, HardState = context.Identity, CognitiveContext = context.CognitiveContext.Value }, ct))
        {
            rawDataBuilder.Append(chunk);
        }
        context.RawWorkerData = rawDataBuilder.ToString();
        swWorker.Stop();

        outcome.Intention = context.CognitiveContext!.Value.Intention.ToString();
        outcome.Confidence = context.CognitiveContext.Value.ConfidenceScore;
        outcome.ProfilerModel = context.CognitiveContext.Value.ModelUsed;
        outcome.Tool = dispatchToTool.Dispatch.ToolName;
        if (context.CognitiveContext.Value.Intention == ConversationIntention.KnowledgeSearch)
            outcome.EmbeddingModel = _embeddingModel;

        if (context.RawWorkerData.StartsWith("Erreur", StringComparison.Ordinal)
            || context.RawWorkerData.StartsWith("Accès refusé", StringComparison.Ordinal)
            || context.RawWorkerData.StartsWith("Action non autorisée", StringComparison.Ordinal)
            || context.RawWorkerData.StartsWith("Aucun", StringComparison.Ordinal)
            || context.RawWorkerData.StartsWith("Aucune", StringComparison.Ordinal)
            || context.RawWorkerData.StartsWith("L'opération a été annulée", StringComparison.Ordinal))
        {
            outcome.Outcome = PipelineOutcomeKind.WorkerError;
            yield return context.RawWorkerData;
            yield break;
        }

        var reflectionAttempt = 0;
        string finalDraft = "";
        string? feedback = null;
        EvaluationResult? lastEvaluation = null;

        // R1 — Borner le contexte RAG/outil transmis au Synthesizer et au Checker :
        // un draft/rawData trop long fait « écho » au petit modèle (phi4-mini) au
        // lieu du JSON. On tronque à 500 mots pour l'étape LLM, puis on restaure le
        // rawData complet pour l'extraction des marqueurs (WIDGET/SUGGEST).
        var fullRawData = context.RawWorkerData;
        context.RawWorkerData = BoundWords(fullRawData, 500);
        var swReflection = Stopwatch.StartNew();
        try
        {
            while (reflectionAttempt <= _maxReflectionLoops)
            {
                reflectionAttempt++;
                finalDraft = await _synthesizer.DraftResponseAsync(context, feedback, ct);
                var evaluation = await _checker.EvaluateAsync(context, finalDraft, ct);
                lastEvaluation = evaluation;

                if (evaluation.IsValid) break;

                feedback = evaluation.ActionableFeedback;
                if (reflectionAttempt >= _maxReflectionLoops) break;
            }
        }
        finally
        {
            context.RawWorkerData = fullRawData;
            swReflection.Stop();
        }

        // Dernier draft invalide : ne jamais streamer une réponse non validée
        if (lastEvaluation is { IsValid: false })
        {
            _logger.LogWarning("AgentOrchestrator: final draft invalid after {ReflectionAttempt} reflection loop(s) — response not streamed", reflectionAttempt);
            outcome.Outcome = PipelineOutcomeKind.NotStreamed;
            outcome.ReflectionLoops = reflectionAttempt;
            outcome.CheckerValid = lastEvaluation.IsValid;
            outcome.SynthesizerModel = _synthesizerModel;
            outcome.CheckerModel = _checkerModel;
            yield return "Je n'ai pas pu générer une réponse validée. Pouvez-vous reformuler votre demande ou réessayer ?";
            yield break;
        }

        // -----------------------------------------------------------------
        // PHASE 6 : Livraison avec UX-Streaming (Token Replay)
        // -----------------------------------------------------------------
        if (string.IsNullOrWhiteSpace(finalDraft))
        {
            yield return "Erreur lors de la génération de la réponse finale.";
            yield break;
        }

        // -----------------------------------------------------------------
        // PHASE 5 : Marqueurs pipeline (WIDGET + SUGGEST)
        // -----------------------------------------------------------------
        // Les marqueurs sont ajoutés APRÈS la validation du checker : le LLM
        // n'évalue que le contenu humain ; les marqueurs sont déterministes.

        // 5.2a — Préservation WIDGET : si le worker a émis
        // ||WIDGET:SalaryAdvance:{id}||, le marqueur doit survivre dans le
        // draft final même si le synthétiseur l'a perdu (jamais dupliqué).
        var widgetMarker = Regex.Match(
            context.RawWorkerData ?? "",
            @"\|\|WIDGET:SalaryAdvance:[0-9a-fA-F-]{36}\|\|").Value;

        if (widgetMarker.Length > 0 && !finalDraft.Contains(widgetMarker, StringComparison.Ordinal))
        {
            finalDraft += "\n" + widgetMarker;
        }

        // 5.2b — Suggestions contextuelles selon l'intention (aucun marqueur sinon)
        var suggestion = context.CognitiveContext!.Value.Intention switch
        {
            ConversationIntention.LeaveBalance => "||SUGGEST:Poser un congé||",
            ConversationIntention.SalaryAdvance => "||SUGGEST:Suivre ma demande||",
            _ => null,
        };

        if (suggestion is not null)
        {
            finalDraft += "\n" + suggestion;
        }

        // Durcissement B2 — aucun caractère de contrôle ne doit atteindre le
        // streaming : la sentinelle de déni n'a de sens que sur le chemin Denied.
        finalDraft = finalDraft.Replace("\u001f", "");

        // Résultat de succès pour l'audit : modèles réellement exécutés, boucles de
        // réflexion, validation checker, marqueurs produits.
        outcome.Outcome = PipelineOutcomeKind.Success;
        outcome.ReflectionLoops = reflectionAttempt;
        outcome.CheckerValid = lastEvaluation?.IsValid ?? true;
        outcome.SynthesizerModel = _synthesizerModel;
        outcome.CheckerModel = _checkerModel;
        outcome.WidgetId = widgetMarker.Length > 0
            ? Regex.Match(widgetMarker, @"[0-9a-fA-F-]{36}").Value
            : null;
        outcome.Suggestion = suggestion?.Replace("||SUGGEST:", "").Replace("||", "");

        // Observabilité — dernière branche qui était muette : le succès (y compris
        // les salutations LLM) est désormais traçable dans le log d'application.
        _logger.LogInformation(
            "PIPELINE_SUCCESS — Intention: {Intention}, Tool: {Tool}, Confidence: {Conf:0.000}, ReflectionLoops: {Loops}, CheckerValid: {Valid}",
            outcome.Intention, outcome.Tool, outcome.Confidence, outcome.ReflectionLoops, outcome.CheckerValid);
        _logger.LogInformation(
            "PIPELINE_TIMINGS — Profiler:{Profiler}ms Validator:{Validator}ms Dispatcher:{Dispatcher}ms Worker:{Worker}ms Reflection:{Reflection}ms",
            profilerMs, validatorMs, swDispatcher.ElapsedMilliseconds, swWorker.ElapsedMilliseconds, swReflection.ElapsedMilliseconds);

        // Hachage de la réponse validée pour simuler le streaming côté client (SSE)
        // On sépare par espaces tout en conservant les espaces
        var words = finalDraft.Split(new[] { ' ' }, StringSplitOptions.None);

        for (int i = 0; i < words.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            // On rajoute l'espace perdu lors du Split (sauf pour le dernier mot)
            var tokenToYield = i < words.Length - 1 ? words[i] + " " : words[i];

            yield return tokenToYield;

            // Micro-délai pour simuler la latence d'inférence LLM (30ms = fluide et lisible)
            await Task.Delay(30, ct);
        }
    }

    private static string BoundWords(string? text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords) return text;
        const string truncationNote = "\n[Note : la base de connaissances a retourné plus de résultats, mais seule une partie est visible ici en raison des contraintes de taille.]";
        return string.Join(" ", words.Take(maxWords)) + " …" + truncationNote;
    }
}
