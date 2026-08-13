using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Agirh.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Agirh.Infrastructure.Services;

public sealed class WorkerExecutor : IWorkerExecutor
{
    private readonly IEnumerable<IMafTool> _tools;
    private readonly ILogger<WorkerExecutor> _logger;

    public WorkerExecutor(
        IEnumerable<IMafTool> tools,
        ILogger<WorkerExecutor> logger)
    {
        _tools = tools;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> ExecuteAsync(ExecutionInput input, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var dispatch = input.Dispatch;
        var sw = Stopwatch.StartNew();

        if (dispatch.ToolName == "GeneralChat")
        {
            sw.Stop();
            _logger.LogInformation(
                "GENERAL_CHAT — MainIdea: \"{MainIdea}\", Intention: {Intention}, Confidence: {Conf:0.00}, UserId: {UserId}",
                input.CognitiveContext.MainIdea,
                input.CognitiveContext.Intention,
                input.CognitiveContext.ConfidenceScore,
                input.HardState.UserId);
            yield return input.CognitiveContext.MainIdea;
            yield break;
        }

        var tool = _tools.FirstOrDefault(t => t.Name == dispatch.ToolName);
        if (tool == null)
        {
            sw.Stop();
            _logger.LogError("Tool {ToolName} not found in registered IMafTool implementations", dispatch.ToolName);
            yield return $"Erreur système : l'outil '{dispatch.ToolName}' n'est pas disponible.";
            yield break;
        }

        string rawText = "";
        string? errorText = null;

        try
        {
            var paramJson = JsonSerializer.Serialize(dispatch.Parameters ?? new Dictionary<string, object?>());
            var parameters = JsonSerializer.Deserialize<JsonElement>(paramJson);
            rawText = await tool.ExecuteAsync(parameters, input.HardState.UserId, ct);
        }
        catch (OperationCanceledException)
        {
            // Durcissement B3 — l'annulation utilisateur se propage ; seule une
            // annulation interne à l'outil devient un message verbeux intercepté
            // par l'orchestrateur ("L'opération a été annulée…").
            if (ct.IsCancellationRequested) throw;
            sw.Stop();
            _logger.LogWarning("Tool execution cancelled for {ToolName} after {Ms}ms", dispatch.ToolName, sw.ElapsedMilliseconds);
            errorText = "L'opération a été annulée.";
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Tool execution failed for {ToolName}", dispatch.ToolName);
            errorText = "Erreur système lors de l'exécution de l'outil. Veuillez réessayer.";
        }

        if (errorText != null)
        {
            yield return errorText;
            yield break;
        }

        sw.Stop();
        _logger.LogInformation("TOOL_EXEC — {ToolName} in {Ms}ms", dispatch.ToolName, sw.ElapsedMilliseconds);

        var isError = rawText.StartsWith("Erreur", StringComparison.Ordinal)
                      || rawText.StartsWith("Accès refusé", StringComparison.Ordinal)
                      || rawText.StartsWith("Aucun", StringComparison.Ordinal);

        if (isError)
            _logger.LogWarning("Tool {ToolName} returned error in response: {Error}", dispatch.ToolName, rawText);

        yield return rawText;
    }
}
