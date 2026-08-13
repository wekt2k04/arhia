namespace Agirh.Core.Interfaces;

/// <summary>
/// Issue d'une requête de chat au niveau du pipeline (utilisée par le log d'audit JSONL).
/// </summary>
public enum PipelineOutcomeKind
{
    Success,
    Denied,
    NotStreamed,
    Fallback,
    Unknown,
    ValidationClarification,
    WorkerError,
    RoutingError,
    Error
}

/// <summary>
/// Résumé structuré d'une exécution du pipeline (modèles ayant intervenu, intention,
/// résultat). Rempli par <c>AgentOrchestratorService</c> via <c>AgentPipelineContext.Outcome</c>.
/// </summary>
public sealed class AgentPipelineOutcome
{
    public string? Intention { get; set; }
    public float? Confidence { get; set; }
    public string? ProfilerModel { get; set; }
    public string? SynthesizerModel { get; set; }
    public string? CheckerModel { get; set; }
    public string? EmbeddingModel { get; set; }
    public string? Tool { get; set; }
    public PipelineOutcomeKind Outcome { get; set; } = PipelineOutcomeKind.Error;
    public int ReflectionLoops { get; set; }
    public bool? CheckerValid { get; set; }
    public string? WidgetId { get; set; }
    public string? Suggestion { get; set; }
}
