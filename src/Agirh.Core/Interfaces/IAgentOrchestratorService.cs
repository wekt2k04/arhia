using System.Text.Json;
using Agirh.Core.Models;

namespace Agirh.Core.Interfaces;

public class AgentPipelineContext
{
    public required string ConversationId { get; init; }
    public required IReadOnlyList<RawMessage> RecentMessages { get; init; }
    public required HardState Identity { get; init; }
    public DynamicContextVector? CognitiveContext { get; set; }
    public JsonElement ValidatedParameters { get; set; }
    public string? RawWorkerData { get; set; }
    /// <summary>Résumé structuré de l'exécution (modèles, intention, résultat) — rempli par l'orchestrateur.</summary>
    public AgentPipelineOutcome? Outcome { get; set; }
}

public record ValidationResult(bool IsValid, string? ErrorMessage, JsonElement ValidatedJson);

public interface IPreFlightValidator
{
    ValidationResult ValidateParameters(string toolName, IReadOnlyDictionary<string, string?> extractedEntities);
}

public record EvaluationResult(bool IsValid, string ActionableFeedback);

public interface ISynthesizerAgent
{
    Task<string> DraftResponseAsync(AgentPipelineContext context, string? feedback = null, CancellationToken ct = default);
}

public interface ICheckerAgent
{
    Task<EvaluationResult> EvaluateAsync(AgentPipelineContext context, string draftResponse, CancellationToken ct = default);
}

public interface IAgentOrchestratorService
{
    IAsyncEnumerable<string> ProcessChatRequestAsync(AgentPipelineContext context, CancellationToken ct = default);
}
