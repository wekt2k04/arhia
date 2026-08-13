using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Agirh.Core.Interfaces;
using Agirh.Core.Security;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Services;
using Agirh.Api.Logging;

namespace Agirh.Api.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize]
public class AgentController : ControllerBase
{
    private readonly IAgentOrchestratorService _orchestrator;
    private readonly IHardStateExtractor _hardStateExtractor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IChatAuditLogger _auditLogger;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IAgentOrchestratorService orchestrator,
        IHardStateExtractor hardStateExtractor,
        IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        IChatAuditLogger auditLogger,
        IConfiguration configuration,
        ILogger<AgentController> logger)
    {
        _orchestrator = orchestrator;
        _hardStateExtractor = hardStateExtractor;
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _auditLogger = auditLogger;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task ChatAsync([FromBody] ChatRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var tokenCount = 0;

        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            await WriteSseEvent("error", "Message requis", ct);
            await WriteAuditAsync(null, null, null, request?.Message, null, null, 200, sw.ElapsedMilliseconds, tokenCount, "Message requis");
            return;
        }

        HardState hardState;
        try
        {
            hardState = _hardStateExtractor.ExtractFromPrincipal(User);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to extract HardState from JWT");
            Response.StatusCode = 401;
            await WriteSseEvent("error", "Authentification invalide.", ct);
            await WriteAuditAsync(null, null, null, request.Message, null, null, 401, sw.ElapsedMilliseconds, tokenCount, "Authentification invalide");
            return;
        }

        AgentConversation conversation;
        try
        {
            conversation = await GetOrCreateConversationAsync(hardState, request.ConversationId, request.Message, ct);
        }
        catch (UnauthorizedAccessException)
        {
            Response.StatusCode = 403;
            await WriteSseEvent("error", "Cette conversation ne vous appartient pas.", CancellationToken.None);
            await WriteAuditAsync(request.ConversationId, hardState.UserId, hardState.Role, request.Message, null, null, 403, sw.ElapsedMilliseconds, tokenCount, "Conversation not owned");
            return;
        }

        conversation.Messages.Add(new AgentMessage
        {
            Role = "user",
            Content = request.Message,
            Timestamp = DateTime.UtcNow,
        });

        var recentMessages = request.PreviousMessages?.Select(m => new RawMessage
        {
            Role = m.Role,
            Content = m.Content,
            Timestamp = m.Timestamp,
        }).ToList() ?? new List<RawMessage>();

        recentMessages.Add(new RawMessage
        {
            Role = "user",
            Content = request.Message,
            Timestamp = DateTime.UtcNow,
        });

        var context = new AgentPipelineContext
        {
            ConversationId = conversation.Id.ToString(),
            RecentMessages = recentMessages.TakeLast(3).ToList(),
            Identity = hardState,
            Outcome = new AgentPipelineOutcome(),
        };

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers.Connection = "keep-alive";

        await WriteSseEvent("conversation", conversation.Id.ToString(), ct);

        var responseBuilder = new StringBuilder();
        Exception? streamError = null;
        try
        {
            await foreach (var token in _orchestrator.ProcessChatRequestAsync(context, ct))
            {
                // PHASE 6 — Refus RBAC flagué : l'orchestrateur préfixe le message
                // de refus poli avec la sentinelle. On émet UNE seule frame SSE
                // "denied" (message sans sentinelle) au lieu d'un "token", puis on
                // termine le flux (le finally persisté l'historique, pas de "done").
                // Durcissement B1 — la sentinelle n'est honorée QUE si l'orchestrateur
                // a réellement conclu à un déni RBAC (Outcome.Denied) : un écho
                // accidentel de la sentinelle par le LLM sur un autre chemin ne peut
                // plus être interprété comme une fausse bulle orange.
                if (token.StartsWith(AgentOrchestratorService.DenialSentinel, StringComparison.Ordinal)
                    && context.Outcome?.Outcome == PipelineOutcomeKind.Denied)
                {
                    var denialMessage = token[AgentOrchestratorService.DenialSentinel.Length..]
                        .Replace(AgentOrchestratorService.DenialSentinel, "", StringComparison.Ordinal);

                    responseBuilder.Append(denialMessage);
                    await WriteSseEvent("denied", denialMessage, ct);
                    return;
                }

                // Filtre défensif : aucun caractère de contrôle ne doit fuir vers
                // l'UI, même si un chemin non prévu injectait la sentinelle au
                // milieu d'un jeton. Les autres événements restent inchangés.
                var safeToken = token.Replace(AgentOrchestratorService.DenialSentinel, "", StringComparison.Ordinal);
                responseBuilder.Append(safeToken);
                tokenCount++;
                await WriteSseEvent("token", safeToken, ct);
            }
        }
        catch (Exception ex)
        {
            streamError = ex;
            throw;
        }
        finally
        {
            conversation.Messages.Add(new AgentMessage
            {
                Role = "assistant",
                Content = responseBuilder.ToString(),
                Timestamp = DateTime.UtcNow,
            });
            conversation.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);

            // Audit structuré : rôle, modèles ayant intervenu, résultat de la requête.
            await WriteAuditAsync(
                conversation.Id.ToString(),
                hardState.UserId,
                hardState.Role,
                request.Message,
                responseBuilder.ToString(),
                context.Outcome,
                Response.StatusCode,
                sw.ElapsedMilliseconds,
                tokenCount,
                streamError?.GetType().Name);
        }

        await WriteSseEvent("done", "[DONE]", CancellationToken.None);
        await Response.Body.FlushAsync(CancellationToken.None);
    }

    private async Task WriteAuditAsync(
        string? conversationId, string? userId, string? role, string? message,
        string? response, AgentPipelineOutcome? outcome, int status, long latencyMs, int tokenCount, string? error = null)
    {
        try
        {
            await _auditLogger.AppendAsync(new ChatAuditEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                ConversationId = conversationId,
                UserId = userId,
                Role = role,
                Message = PiiRedactor.Redact(Truncate(message, 400)),
                Response = PiiRedactor.Redact(Truncate(response, 400)),
                Intention = outcome?.Intention,
                Confidence = outcome?.Confidence,
                Models = outcome is null ? null : new ChatAuditModels
                {
                    Profiler = outcome.ProfilerModel,
                    Synthesizer = outcome.SynthesizerModel,
                    Checker = outcome.CheckerModel,
                    Embedding = outcome.EmbeddingModel,
                },
                Tool = outcome?.Tool,
                Outcome = outcome?.Outcome.ToString(),
                Status = status,
                LatencyMs = latencyMs,
                TokenCount = tokenCount,
                ReflectionLoops = outcome?.ReflectionLoops ?? 0,
                CheckerValid = outcome?.CheckerValid,
                WidgetId = outcome?.WidgetId,
                Suggestion = outcome?.Suggestion,
                Error = error,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write chat audit entry");
        }
    }

    private static string Truncate(string? text, int max)
        => string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max] + "…";

    private async Task<AgentConversation> GetOrCreateConversationAsync(
        HardState hardState, string? conversationId, string firstMessage, CancellationToken ct)
    {
        var ownerId = Guid.Parse(hardState.UserId);
        if (Guid.TryParse(conversationId, out var existingId))
        {
            var existing = await _unitOfWork.AgentConversations.GetByIdAsync(existingId, ct);
            if (existing != null)
            {
                if (existing.UserId != ownerId)
                    throw new UnauthorizedAccessException("Conversation owned by another user.");
                return existing;
            }
        }

        var conversation = new AgentConversation
        {
            UserId = ownerId,
            Title = DeriveTitle(firstMessage),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await _unitOfWork.AgentConversations.AddAsync(conversation, ct);
        return conversation;
    }

    private static string DeriveTitle(string firstMessage)
    {
        var trimmed = firstMessage.Trim();
        return trimmed.Length <= 60 ? trimmed : trimmed[..60] + "…";
    }

    private async Task WriteSseEvent(string eventType, string data, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { type = eventType, data });
        await Response.WriteAsync($"data: {payload}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> Health()
    {
        var configuredEndpoint = _configuration["AI:Endpoint"];
        var endpoint = string.IsNullOrWhiteSpace(configuredEndpoint) ? "http://localhost:11434" : configuredEndpoint;
        try
        {
            var http = _httpClientFactory.CreateClient("OllamaClient");
            http.BaseAddress = new Uri(endpoint);
            var response = await http.GetAsync("/api/tags");
            var ollamaOk = response.IsSuccessStatusCode;
            if (!ollamaOk)
                _logger.LogWarning("Health degraded — Ollama responded {Status} on {Endpoint}", (int)response.StatusCode, http.BaseAddress);
            return Ok(new { Status = ollamaOk ? "healthy" : "degraded", OllamaAvailable = ollamaOk });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health degraded — Ollama unreachable on {Endpoint}", endpoint);
            return Ok(new { Status = "degraded", OllamaAvailable = false });
        }
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public List<PreviousMessage>? PreviousMessages { get; set; }
}

public class PreviousMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
