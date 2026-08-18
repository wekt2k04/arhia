using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

/// <summary>
/// Orchestration conversationnelle (docs/STACK_TECHNIQUE.md #5, docs/ARCHITECTURE.md §5) : Router puis,
/// selon l'intention, pipeline RAG (embedding -> recherche Qdrant -> reranking) ou lecture
/// seule d'un WorkflowInstance. Garde-fous docs/LOGIQUE_METIER.md §9 : informatif uniquement,
/// anti-hallucination applique en code (jamais d'appel au generateur sans chunk pertinent),
/// RBAC applique avant toute lecture de dossier.
/// </summary>
public sealed class AnswerConversationUseCase
{
    private const int TopKSearch = 5;
    private const int TopKAfterReranking = 3;
    private const float MinimumRelevanceThreshold = 0.01f; // filtre le bruit evident ; affine par GeneratorRefusalIndicators (voir AnswerDocumentaryAsync)
    private const string GeneratorRefusalPhrase = "Je n'ai pas trouvé cette information";

    // Le generateur ne reprend pas toujours la formule exacte imposee par le prompt malgre la
    // consigne explicite (constate empiriquement, milestone 9 : "le contexte fourni n'indique
    // pas..." / "ne specifie pas..." en pratique aussi frequent que la phrase canonique) - liste
    // volontairement plus large qu'une seule phrase pour rester fiable face a la variabilite
    // d'un modele 3.8B.
    private static readonly string[] GeneratorRefusalIndicators =
    {
        GeneratorRefusalPhrase,
        "ne spécifie pas",
        "n'indique pas",
        "ne précise pas",
        "ne mentionne pas",
        "ne contient pas cette information",
        "ne traite pas de"
    };

    private readonly ILlmRouterPort _router;
    private readonly ILlmGeneratorPort _generator;
    private readonly IEmbeddingPort _embedding;
    private readonly IVectorSearchPort _vectorSearch;
    private readonly IRerankerPort _reranker;
    private readonly IEmployeeRepository _employees;
    private readonly IWorkflowInstanceRepository _workflowInstances;

    public AnswerConversationUseCase(
        ILlmRouterPort router,
        ILlmGeneratorPort generator,
        IEmbeddingPort embedding,
        IVectorSearchPort vectorSearch,
        IRerankerPort reranker,
        IEmployeeRepository employees,
        IWorkflowInstanceRepository workflowInstances)
    {
        _router = router;
        _generator = generator;
        _embedding = embedding;
        _vectorSearch = vectorSearch;
        _reranker = reranker;
        _employees = employees;
        _workflowInstances = workflowInstances;
    }

    public async Task<ConversationResponse> ExecuteAsync(
        UserAccount actor,
        string question,
        Guid? targetEmployeeId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("La question ne peut pas être vide.", nameof(question));

        var intent = await _router.ClassifyAsync(question, ct);

        return intent switch
        {
            ConversationIntent.DocumentaryQuestion => await AnswerDocumentaryAsync(question, ct),
            ConversationIntent.CaseStatus => await AnswerCaseStatusAsync(actor, targetEmployeeId, ct),
            _ => OutOfScopeResponse()
        };
    }

    /// <summary>
    /// Équivalent streamé de <see cref="ExecuteAsync"/>, pour le chat SSE (docs/STACK_TECHNIQUE.md
    /// §1). Émet un TextFragment par fragment de texte reçu du générateur (branche
    /// documentaire) ou un seul TextFragment pour les branches déjà synchrones (statut de
    /// dossier, hors périmètre — pas de generation LLM a etaler dans le temps), puis exactement
    /// un ResponseCompleted portant les métadonnées finales (sourcée, sources) une fois le texte
    /// complet connu. Même garde-fous RBAC/anti-hallucination que la version non streamée — les
    /// deux partagent la même préparation de contexte (PrepareDocumentaryContextAsync).
    /// </summary>
    public async IAsyncEnumerable<ConversationEvent> ExecuteStreamingAsync(
        UserAccount actor,
        string question,
        Guid? targetEmployeeId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("La question ne peut pas être vide.", nameof(question));

        var intent = await _router.ClassifyAsync(question, ct);

        switch (intent)
        {
            case ConversationIntent.DocumentaryQuestion:
                await foreach (var conversationEvent in AnswerDocumentaryStreamingAsync(question, ct))
                    yield return conversationEvent;
                break;

            case ConversationIntent.CaseStatus:
                var statusResponse = await AnswerCaseStatusAsync(actor, targetEmployeeId, ct);
                yield return new TextFragment(statusResponse.Text);
                yield return new ResponseCompleted(statusResponse.Sourced, statusResponse.Sources);
                break;

            default:
                var outOfScope = OutOfScopeResponse();
                yield return new TextFragment(outOfScope.Text);
                yield return new ResponseCompleted(outOfScope.Sourced, outOfScope.Sources);
                break;
        }
    }

    private async Task<ConversationResponse> AnswerDocumentaryAsync(string question, CancellationToken ct)
    {
        var preparation = await PrepareDocumentaryContextAsync(question, ct);
        if (!preparation.Found)
            return NotFoundResponse();

        var text = await _generator.GenerateResponseAsync(preparation.SystemPrompt!, question, ct);

        if (IsGeneratorRefusal(text))
            return new ConversationResponse(text, Sourced: false, Array.Empty<string>());

        var sources = preparation.Best.Select(c => c.DocumentSource).Distinct().ToList();
        return new ConversationResponse(text, Sourced: true, sources);
    }

    private async IAsyncEnumerable<ConversationEvent> AnswerDocumentaryStreamingAsync(
        string question, [EnumeratorCancellation] CancellationToken ct)
    {
        var preparation = await PrepareDocumentaryContextAsync(question, ct);
        if (!preparation.Found)
        {
            var notFound = NotFoundResponse();
            yield return new TextFragment(notFound.Text);
            yield return new ResponseCompleted(notFound.Sourced, notFound.Sources);
            yield break;
        }

        var accumulatedText = new System.Text.StringBuilder();
        await foreach (var fragment in _generator.GenerateResponseStreamingAsync(preparation.SystemPrompt!, question, ct))
        {
            accumulatedText.Append(fragment);
            yield return new TextFragment(fragment);
        }

        var completeText = accumulatedText.ToString();
        if (IsGeneratorRefusal(completeText))
        {
            yield return new ResponseCompleted(Sourced: false, Array.Empty<string>());
            yield break;
        }

        var sources = preparation.Best.Select(c => c.DocumentSource).Distinct().ToList();
        yield return new ResponseCompleted(Sourced: true, sources);
    }

    private readonly record struct DocumentaryPreparation(bool Found, string? SystemPrompt, IReadOnlyList<DocumentChunk> Best);

    /// <summary>
    /// Récupération partagée par la version synchrone et la version streamée : embedding ->
    /// recherche Qdrant -> reranking -> filtrage par seuil -> construction du prompt système.
    /// Point unique de vérité pour que les deux chemins ne divergent jamais silencieusement.
    /// </summary>
    private async Task<DocumentaryPreparation> PrepareDocumentaryContextAsync(string question, CancellationToken ct)
    {
        var queryVector = await _embedding.GenerateEmbeddingAsync(question, ct);
        var candidates = await _vectorSearch.SearchAsync(queryVector, TopKSearch, ct);

        if (candidates.Count == 0)
            return new DocumentaryPreparation(false, null, Array.Empty<DocumentChunk>());

        var reranked = await _reranker.RerankAsync(question, candidates, ct);
        var best = reranked
            .Where(c => c.Score >= MinimumRelevanceThreshold)
            .Take(TopKAfterReranking)
            .ToList();

        if (best.Count == 0)
            return new DocumentaryPreparation(false, null, Array.Empty<DocumentChunk>());

        var context = string.Join(
            "\n\n---\n\n",
            best.Select(c => $"[Source: {c.DocumentSource} — {c.TitlePath}]\n{c.Content}"));

        var systemPrompt =
            "Tu es l'assistant RH d'AGIRH. Le contexte ci-dessous contient des extraits de la documentation " +
            "interne déjà sélectionnés comme pertinents pour cette question. Lis-le attentivement en entier " +
            "avant de répondre : la réponse s'y trouve généralement, parfois formulée différemment de la " +
            "question. Réponds en français, de façon concise et complète, UNIQUEMENT à partir de ce contexte. " +
            $"Dis explicitement « {GeneratorRefusalPhrase} » UNIQUEMENT si le contexte ne traite " +
            "vraiment pas du sujet de la question — pas simplement parce que la formulation diffère.\n\n" +
            $"Contexte :\n{context}";

        return new DocumentaryPreparation(true, systemPrompt, best);
    }

    // Le score de reranking mesure la proximité thématique, pas "la réponse est présente" :
    // vérifié empiriquement (milestone 9, jeu de Q/R gold) qu'un chunk du bon sujet mais muet
    // sur le fait précis demandé peut scorer aussi haut qu'une vraie réponse (ex. 0.70-0.78,
    // proche de vrais positifs). Remonter le seuil pénaliserait autant de bonnes réponses qu'il
    // n'en filtrerait. Le signal fiable est le générateur lui-même : le prompt système lui
    // impose la formule de refus quand le contexte ne traite pas du sujet — on la relit ici
    // plutôt que de sourcer une réponse qui est en fait un refus.
    private static bool IsGeneratorRefusal(string text) =>
        GeneratorRefusalIndicators.Any(indicator => text.Contains(indicator, StringComparison.OrdinalIgnoreCase));

    private async Task<ConversationResponse> AnswerCaseStatusAsync(
        UserAccount actor,
        Guid? targetEmployeeId,
        CancellationToken ct)
    {
        var targetId = targetEmployeeId ?? await ResolvePersonalCaseAsync(actor, ct);
        if (targetId is null)
            return CaseNotFoundResponse();

        var employee = await _employees.GetByIdAsync(targetId.Value, ct);
        if (employee is null)
            return CaseNotFoundResponse();

        if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
            throw new AccessDeniedException("Vous n'avez pas accès au dossier de ce collaborateur.");

        var instance = await _workflowInstances.GetByEmployeeAsync(employee.Id, WorkflowType.Onboarding, ct)
            ?? await _workflowInstances.GetByEmployeeAsync(employee.Id, WorkflowType.Offboarding, ct);

        if (instance is null)
            return new ConversationResponse(
                "Aucun dossier onboarding ou offboarding actif n'a été trouvé pour ce collaborateur.",
                Sourced: false,
                Array.Empty<string>());

        var remainingItems = instance.Items.Count(i => i.Status == ItemStatus.Pending);
        var suffix = remainingItems > 0
            ? $"{remainingItems} item(s) restent en attente sur {instance.Items.Count}."
            : "Tous les items ont été traités.";

        var text = $"Le dossier {instance.Type} de {employee.FirstName} {employee.LastName} " +
                     $"est au statut {FormatStatusInFrench(instance.Status)}. {suffix}";

        return new ConversationResponse(text, Sourced: false, Array.Empty<string>());
    }

    // WorkflowStatus est un enum du vocabulaire code (anglais, cf. Domain/Enums.cs), mais cette
    // phrase est du texte final montre a l'utilisateur du chat, qui doit rester en francais
    // (regle produit actee) — d'ou cette table de correspondance plutot qu'un ToString() direct.
    private static string FormatStatusInFrench(WorkflowStatus status) => status switch
    {
        WorkflowStatus.InProgress => "en cours",
        WorkflowStatus.Closed => "clôturé",
        WorkflowStatus.Archived => "archivé",
        WorkflowStatus.Cancelled => "annulé",
        WorkflowStatus.Suspended => "suspendu",
        _ => status.ToString()
    };

    private async Task<Guid?> ResolvePersonalCaseAsync(UserAccount actor, CancellationToken ct)
    {
        if (actor.Role != RoleType.Employee)
            return null; // RH/Admin doivent préciser explicitement quel collaborateur (targetEmployeeId)

        var employee = await _employees.GetByUserAccountIdAsync(actor.Id, ct);
        return employee?.Id;
    }

    private static ConversationResponse CaseNotFoundResponse() => new(
        "Je n'ai pas trouvé de dossier collaborateur associé à cette demande. " +
        "Si vous êtes un nouveau collaborateur, contactez le RH de votre pôle pour que votre fiche soit créée.",
        Sourced: false,
        Array.Empty<string>());

    private static ConversationResponse NotFoundResponse() => new(
        "Je n'ai pas trouvé cette information dans la documentation disponible. " +
        "Vous pouvez contacter le RH de votre pôle.",
        Sourced: false,
        Array.Empty<string>());

    private static ConversationResponse OutOfScopeResponse() => new(
        "Cette question sort du périmètre de l'assistant AGIRH (onboarding/offboarding et politiques internes). " +
        "Contactez le RH de votre pôle pour toute autre demande.",
        Sourced: false,
        Array.Empty<string>());
}
