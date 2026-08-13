using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agirh.Core.Interfaces;
using Agirh.Core.Security;
using Agirh.Core.Settings;

namespace Agirh.Infrastructure.Services;

public sealed class CheckerAgent : ICheckerAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _http;
    private readonly ILogger<CheckerAgent> _logger;
    private readonly string _model;
    private readonly string _keepAlive;

    public CheckerAgent(
        HttpClient http,
        IOptions<AIOptions> options,
        ILogger<CheckerAgent> logger)
    {
        _http = http;
        _logger = logger;
        _model = !string.IsNullOrWhiteSpace(options.Value.CheckerModel)
            ? options.Value.CheckerModel
            : !string.IsNullOrWhiteSpace(options.Value.WorkerModel)
                ? options.Value.WorkerModel
                : "qwen3.5:9b";
        _keepAlive = string.IsNullOrWhiteSpace(options.Value.OllamaKeepAlive) ? "30m" : options.Value.OllamaKeepAlive;
    }

    public async Task<EvaluationResult> EvaluateAsync(AgentPipelineContext context, string draftResponse, CancellationToken ct = default)
    {
        var intention = context.CognitiveContext?.Intention.ToString() ?? "Inconnue";
        var rawData = context.RawWorkerData ?? "";

        var systemPrompt = """
Tu es un correcteur de réponses pour un assistant RH. Évalue si le projet de réponse (draft) est correct.

RÈGLES :
- Le draft doit être cohérent avec l'intention détectée.
- Le draft ne doit PAS contenir d'inventions absentes des données système (is_valid:false si une affirmation n'est pas appuyée par les données).
- Le draft doit répondre à la question posée (ne pas changer de sujet).
- Le draft doit être formulé comme une réponse professionnelle.
- Si l'intention est "Greeting" (salutation, remerciement ou au revoir), un projet de réponse
  poli et bref est VALIDE même s'il ne s'appuie pas sur les Données système (elles ne contiennent
  que le contexte de la salutation).

FORMAT STRICT :
- Zéro texte avant ou après le JSON. Interdiction de recopier le draft ou les données système dans la réponse.
- Réponds exactement : {"is_valid": true|false, "actionable_feedback": "…"}
""";

        var request = new
        {
            model = _model,
            stream = false,
            keep_alive = _keepAlive,
            // `format:"json"` est OBLIGATOIRE : sans lui, les modèles non-reasoning
            // (ex. phi4-mini:3.8b) peuvent répondre en texte libre → ExtractJsonObject
            // renvoie null → fail-closed systématique (NotStreamed sur toute question).
            // Compatible reasoning models car `think:false` racine supprime le raisonnement
            // avant que la contrainte de grammaire JSON s'applique.
            format = "json",
            // Lot C (corrigé) — `think` est un champ RACINE de l'API /api/chat,
            // PAS une option de modèle : placé dans `options`, il est ignoré et un
            // modèle reasoning consomme tout le budget num_predict en raisonnement
            // → message.content vide → fail-closed systématique.
            // `num_predict` à 256 : le verdict est court (~15-25 tokens),
            // la marge évite la troncature `done_reason:"length"`.
            think = false,
            options = new { temperature = 0, num_predict = 256 },
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new
                {
                    role = "user",
                    content = $"Intention : {intention}\nDonnées système : {rawData}\nProjet de réponse à évaluer : {draftResponse}",
                },
            },
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await _http.PostAsJsonAsync("/api/chat", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                sw.Stop();
                var errBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("CheckerAgent: Ollama HTTP {Code} after {Ms}ms — {Body}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, Truncate(errBody, 500));
                return new EvaluationResult(false, "Le service de vérification est indisponible. Réponse non validée.");
            }

            // V6.1 : lire le corps en TEXTE d'abord (ReadFromJsonAsync consomme le
            // stream) pour pouvoir logguer le body brut en cas d'anomalie.
            // F2 : le body contient "Données système" → masqué avant persistance.
            var rawBody = await response.Content.ReadAsStringAsync(ct);
            sw.Stop();
            _logger.LogInformation("CheckerAgent: {Ms}ms, model={Model} — raw = {Body}",
                sw.ElapsedMilliseconds, _model, PiiRedactor.Redact(Truncate(rawBody, 500)));

            var raw = JsonSerializer.Deserialize<OllamaJsonResponse>(rawBody, JsonOptions);

            var content = raw?.Message?.Content?.Trim();
            if (string.IsNullOrEmpty(content))
            {
                // Lot C — repli sur message.thinking : certains modèles raisonnants
                // répondent uniquement dans "thinking" malgré think:false.
                content = raw?.Message?.Thinking?.Trim();
                if (!string.IsNullOrEmpty(content))
                    _logger.LogWarning("CheckerAgent: message.content vide — repli sur message.thinking (modèle raisonnant {Model})", _model);
            }

            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("CheckerAgent: empty/absent message.content. Raw={Raw}", PiiRedactor.Redact(Truncate(rawBody, 300)));
                return new EvaluationResult(false, "Le service de vérification n'a pas renvoyé d'évaluation. Réponse non validée.");
            }

            var jsonText = ExtractJsonObject(content);
            if (jsonText == null)
            {
                _logger.LogWarning("CheckerAgent: no JSON found in response: {Truncated}", content[..Math.Min(content.Length, 120)]);
                return new EvaluationResult(false, "Évaluation illisible (JSON invalide). Réponse non validée.");
            }

            var parsed = JsonSerializer.Deserialize<CheckerEvaluationDto>(jsonText, JsonOptions);
            if (parsed == null)
            {
                _logger.LogWarning("CheckerAgent: failed to deserialize evaluation JSON");
                return new EvaluationResult(false, "Évaluation illisible. Réponse non validée.");
            }

            return new EvaluationResult(parsed.IsValid, parsed.ActionableFeedback ?? "");
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            // Durcissement B3 — seule l'annulation UTILISATEUR se propage ; un
            // timeout Ollama (ct non annulé) reste fail-closed comme le chemin
            // générique, sans tuer le flux SSE.
            if (ct.IsCancellationRequested)
            {
                _logger.LogInformation("CheckerAgent: operation cancelled by caller after {Ms}ms — propagating", sw.ElapsedMilliseconds);
                throw;
            }
            _logger.LogWarning("CheckerAgent: Ollama timeout after {Ms}ms — rejecting draft (fail-closed)", sw.ElapsedMilliseconds);
            return new EvaluationResult(false, "Le service de vérification est indisponible. Réponse non validée.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "CheckerAgent evaluation failed after {Ms}ms — rejecting draft (fail-closed)", sw.ElapsedMilliseconds);
            return new EvaluationResult(false, "Le service de vérification est indisponible. Réponse non validée.");
        }
    }

    private static string? ExtractJsonObject(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            return trimmed;

        var braceStart = trimmed.IndexOf('{');
        var braceEnd = trimmed.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
            return trimmed[braceStart..(braceEnd + 1)];

        return null;
    }

    private static string Truncate(string text, int max)
        => string.IsNullOrEmpty(text) ? "" : text[..Math.Min(text.Length, max)];

    private sealed record OllamaJsonResponse
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
        [JsonPropertyName("done")] public bool Done { get; set; }
    }

    private sealed record OllamaMessage
    {
        [JsonPropertyName("content")] public string Content { get; set; } = "";
        [JsonPropertyName("thinking")] public string? Thinking { get; set; }
    }

    private sealed record CheckerEvaluationDto
    {
        [JsonPropertyName("is_valid")] public bool IsValid { get; set; }
        [JsonPropertyName("actionable_feedback")] public string? ActionableFeedback { get; set; }
    }
}
