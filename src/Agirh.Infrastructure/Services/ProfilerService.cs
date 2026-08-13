using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Agirh.Core.Interfaces;
using Agirh.Core.Security;
using Agirh.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agirh.Infrastructure.Services;

public sealed partial class ProfilerService : ICognitiveProfiler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly Regex DocumentKeywordRegex = GenerateDocumentKeywordRegex();

    private readonly HttpClient _http;
    private readonly ILogger<ProfilerService> _logger;
    private readonly string _model;
    private readonly string _keepAlive;

    public ProfilerService(
        HttpClient http,
        IOptions<AIOptions> options,
        ILogger<ProfilerService> logger)
    {
        _http = http;
        _logger = logger;
        _model = string.IsNullOrWhiteSpace(options.Value.ProfilerModel) ? "phi4-mini:3.8b" : options.Value.ProfilerModel;
        _keepAlive = string.IsNullOrWhiteSpace(options.Value.OllamaKeepAlive) ? "30m" : options.Value.OllamaKeepAlive;
    }

    private static string SanitizeContent(string raw)
    {
        return PiiRedactor.Redact(raw);
    }

    public async Task<DynamicContextVector> ExtractAsync(CognitiveExtractionInput input, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var conversationId = input.ConversationId;

        var sanitizedMessages = input.RecentMessages
            .Select(m => new RawMessage
            {
                Role = m.Role,
                Content = SanitizeContent(m.Content),
                Timestamp = m.Timestamp,
            })
            .ToList();

        var formattedMessages = string.Join("\n",
            sanitizedMessages.Select(m =>
                $"[{m.Timestamp:O}] {m.Role}: {m.Content}"));

        var systemPrompt = GetProfilerSystemPrompt();

        var ollamaRequest = new
        {
            model = _model,
            stream = false,
            keep_alive = _keepAlive,
            format = "json",
            // Cohérence think:false racine (cf. CheckerAgent) : protège contre un
            // modèle reasoning configuré comme ProfilerModel.
            think = false,
            options = new { temperature = 0, num_predict = 512 },
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new
                {
                    role = "user",
                    content = $"[CONVERSATION_ID]: {conversationId}\n[MESSAGES]:\n{formattedMessages}"
                },
            },
        };

        DynamicContextVector result = default;
        var attempt = 0;
        const int maxAttempts = 2;

        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                var response = await _http.PostAsJsonAsync("/api/chat", ollamaRequest, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogCritical(
                        "Ollama HTTP {StatusCode} — body: {Body}",
                        (int)response.StatusCode, errorBody);
                }
                response.EnsureSuccessStatusCode();

                var raw = await response.Content.ReadFromJsonAsync<OllamaJsonResponse>(cancellationToken: ct);

                if (raw?.Message?.Content is not { Length: > 0 } content)
                {
                    _logger.LogWarning("Profiler attempt {Attempt}: empty response from Ollama", attempt);
                    if (attempt >= maxAttempts) return BuildFallback();
                    continue;
                }

                var jsonText = ExtractJson(content);
                if (jsonText == null)
                {
                    _logger.LogWarning("Profiler attempt {Attempt}: no JSON found in response: {Truncated}", attempt, content[..Math.Min(content.Length, 120)]);
                    if (attempt >= maxAttempts) return BuildFallback();
                    continue;
                }

                var parsed = JsonSerializer.Deserialize<ProfilerOutput>(jsonText, JsonOptions);
                if (parsed == null)
                {
                    if (attempt >= maxAttempts) return BuildFallback();
                    continue;
                }

                sw.Stop();
                result = MapToVector(parsed, sw.ElapsedMilliseconds, _model);

                // R2 — Garde déterministe anti-hallucination : une question portant
                // sur un document d'entreprise classée "GeneralInquiry" par le LLM
                // est forcée vers "KnowledgeSearch" (le RAG est requis, jamais une
                // réponse inventée sans source).
                if (result.Intention == ConversationIntention.GeneralInquiry)
                {
                    var lastUser = sanitizedMessages.LastOrDefault(m => m.Role == "user").Content ?? "";
                    if (DocumentKeywordRegex.IsMatch(lastUser))
                    {
                        result = result with
                        {
                            Intention = ConversationIntention.KnowledgeSearch,
                            ExtractedEntities = new Dictionary<string, string?>(result.ExtractedEntities)
                            {
                                ["query"] = string.IsNullOrWhiteSpace(result.MainIdea) ? lastUser : result.MainIdea,
                            },
                        };
                    }
                }
                break;
            }
            catch (OperationCanceledException)
            {
                // Durcissement B3 — l'annulation utilisateur n'est JAMAIS rejouée :
                // on propage l'OperationCanceledException au lieu de re-tenter avec
                // un token mort (ce qui produisait un faux « IA indisponible »).
                if (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Profiler: operation cancelled by caller — propagating");
                    throw;
                }

                _logger.LogWarning("Ollama Unreachable: timeout/cancelled (attempt {Attempt})", attempt);
                if (attempt >= maxAttempts)
                {
                    _logger.LogWarning("Profiler: falling back to Unknown — Ollama timeout after {Attempt} attempts", attempt);
                    return BuildFallback(sw);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Ollama Unreachable: {Reason} (attempt {Attempt})", ex.Message, attempt);
                if (attempt >= maxAttempts)
                {
                    _logger.LogWarning("Profiler: falling back to Unknown — Ollama unreachable after {Attempt} attempts", attempt);
                    return BuildFallback(sw);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Profiler attempt {Attempt}: JSON parse error", attempt);
                if (attempt >= maxAttempts) return BuildFallback(sw);
            }
        }

        return result;
    }

    private static DynamicContextVector BuildFallback(Stopwatch? sw = null)
    {
        sw?.Stop();
        return new DynamicContextVector
        {
            Intention = ConversationIntention.Unknown,
            ConfidenceScore = 0.0f,
            MainIdea = "",
            Urgency = UrgencyLevel.Low,
            Tone = TonePreference.Professional,
            IsFollowUp = false,
            TriggerPhrase = "",
            ExtractedEntities = new Dictionary<string, string?>(),
            ProcessingTimeMs = sw?.ElapsedMilliseconds ?? 0,
            ModelUsed = "fallback",
        };
    }

    private static DynamicContextVector MapToVector(ProfilerOutput parsed, long elapsedMs, string modelUsed)
    {
        var intention = ParseIntention(parsed.Intention);

        var entities = new Dictionary<string, string?>
        {
            ["employee_name"] = parsed.ExtractedEntities?.EmployeeName,
            ["employee_id"] = parsed.ExtractedEntities?.EmployeeId,
            ["leave_request_id"] = parsed.ExtractedEntities?.LeaveRequestId,
            ["category"] = parsed.ExtractedEntities?.Category,
            ["amount"] = parsed.ExtractedEntities?.Amount,
            ["date_reference"] = parsed.ExtractedEntities?.DateReference,
            ["days"] = parsed.ExtractedEntities?.Days,
            ["query"] = parsed.ExtractedEntities?.Query,
        };

        // Repli déterministe (C#) pour le RAG : la query extraite est prioritaire ;
        // pour KnowledgeSearch, le core_idea tronqué garantit que PreFlight dispose de "query".
        if (intention == ConversationIntention.KnowledgeSearch && string.IsNullOrWhiteSpace(entities["query"]))
            entities["query"] = TruncateWords(parsed.CoreIdea ?? "", 20);

        return new DynamicContextVector
        {
            Intention = intention,
            ConfidenceScore = parsed.ConfidenceScore,
            MainIdea = TruncateWords(parsed.CoreIdea ?? "", 20),
            Urgency = UrgencyLevel.Low,
            Tone = TonePreference.Professional,
            IsFollowUp = false,
            TriggerPhrase = "",
            ExtractedEntities = entities,
            ProcessingTimeMs = elapsedMs,
            ModelUsed = modelUsed,
            InputTokens = 0,
            OutputTokens = 0,
        };
    }

    private static ConversationIntention ParseIntention(string? intention) => intention switch
    {
        "LeaveBalance" => ConversationIntention.LeaveBalance,
        "LeaveRequest" => ConversationIntention.LeaveRequest,
        "PayrollSettlement" => ConversationIntention.PayrollSettlement,
        "OnboardingChecklist" => ConversationIntention.OnboardingChecklist,
        "KnowledgeSearch" => ConversationIntention.KnowledgeSearch,
        "ITAccessRevocation" => ConversationIntention.ITAccessRevocation,
        "ManagerAlert" => ConversationIntention.ManagerAlert,
        "LeaveApproval" => ConversationIntention.LeaveApproval,
        "GeneralInquiry" => ConversationIntention.GeneralInquiry,
        "SmallTalk" => ConversationIntention.SmallTalk,
        "Greeting" => ConversationIntention.Greeting,
        "SalaryAdvance" => ConversationIntention.SalaryAdvance,
        _ => ConversationIntention.Unknown,
    };

    private static string? ExtractJson(string raw)
    {
        var trimmed = raw.Trim();

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            return trimmed;

        var mdBlock = "```json";
        var idx = trimmed.IndexOf(mdBlock, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = idx + mdBlock.Length;
            var end = trimmed.IndexOf("```", start, StringComparison.Ordinal);
            if (end >= 0)
            {
                var extracted = trimmed[start..end].Trim();
                if (extracted.StartsWith('{') && extracted.EndsWith('}'))
                    return extracted;
            }
        }

        var braceStart = trimmed.IndexOf('{');
        var braceEnd = trimmed.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
            return trimmed[braceStart..(braceEnd + 1)];

        return null;
    }

    private static string TruncateWords(string text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Take(maxWords));
    }

    [GeneratedRegex(@"(document|règlement|charte|politique|procédure|processus|guide|livret)", RegexOptions.IgnoreCase | RegexOptions.Compiled, "fr-FR")]
    private static partial Regex GenerateDocumentKeywordRegex();

    private static string GetProfilerSystemPrompt()
    {
        return """
        Tu es un routeur cognitif pour le système RH AGIRH. Retourne UNIQUEMENT un objet JSON valide. Zéro texte avant ou après.

        INTENTIONS AUTORISÉES :
        LeaveBalance, LeaveRequest, PayrollSettlement, OnboardingChecklist, KnowledgeSearch, ITAccessRevocation, ManagerAlert, LeaveApproval, GeneralInquiry, SmallTalk, Greeting, SalaryAdvance, Unknown

        RÈGLES STRICTES DE DÉSAMBIGUÏSATION :
        - Règle 1 : Si l'utilisateur parle de "Solde de tout compte", "STC", "Indemnités de départ", ou "Licenciement", c'est OBLIGATOIREMENT "PayrollSettlement".
        - Règle 2 : Si l'utilisateur parle de "Solde de congés", "Jours restants", "RTT", c'est OBLIGATOIREMENT "LeaveBalance".
        - Règle 3 : Le mot "solde" SEUL ou associé à "départ" = PayrollSettlement.
        - Règle 4 : Toute demande liée à une avance sur salaire, un acompte, ou le versement anticipé d'une partie du salaire correspond EXCLUSIVEMENT à "SalaryAdvance".
        - Règle 5 : Pour l'intention "KnowledgeSearch", la clé "query" dans "extracted_entities" est OBLIGATOIRE : requête de recherche compacte en français, issue du core_idea.
        - Règle 6 : Pour une salutation seule, un remerciement ou un au revoir → intention "Greeting".
        - Règle 7 : Toute question portant sur un DOCUMENT d'entreprise (règlement intérieur, charte, politique, procédure, guide, processus RH, livret d'accueil) → OBLIGATOIREMENT "KnowledgeSearch". "GeneralInquiry" est réservé au bavardage général SANS référence documentaire.

        EXEMPLES :
        User: Combien me reste-t-il de jours de congés ?
        {"intention":"LeaveBalance","confidence_score":0.97,"core_idea":"Solde de congés","extracted_entities":{}}

        User: Génère le solde de tout compte de Marwane.
        {"intention":"PayrollSettlement","confidence_score":0.99,"core_idea":"Générer STC pour Marwane","extracted_entities":{"employee_name":"Marwane"}}

        User: J'ai besoin d'une avance sur salaire de 2000 Dhs ce mois-ci s'il vous plaît.
        {"intention":"SalaryAdvance","confidence_score":0.99,"core_idea":"Demande d'avance sur salaire de 2000 Dhs","extracted_entities":{"amount":"2000"}}

        User: Quelles sont les règles de télétravail en vigueur ?
        {"intention":"KnowledgeSearch","confidence_score":0.95,"core_idea":"Règles de télétravail en vigueur","extracted_entities":{"query":"règles de télétravail en vigueur"}}

        User: Que dit le règlement intérieur sur les retards ?
        {"intention":"KnowledgeSearch","confidence_score":0.9,"core_idea":"Règlement intérieur sur les retards","extracted_entities":{"query":"règlement intérieur sur les retards"}}

        User: Bonjour
        {"intention":"Greeting","confidence_score":0.99,"core_idea":"salutation","extracted_entities":{}}

        User: Merci pour votre aide
        {"intention":"Greeting","confidence_score":0.98,"core_idea":"remerciement","extracted_entities":{}}

        User: Bonjour, quel est mon solde de congés ?
        {"intention":"LeaveBalance","confidence_score":0.97,"core_idea":"Solde de congés","extracted_entities":{}}

        User: Je veux poser 2 jours de congés à partir du 15/08/2026
        {"intention":"LeaveRequest","confidence_score":0.97,"core_idea":"Poser 2 jours de congés","extracted_entities":{"date_reference":"15/08/2026","days":"2"}}

        CONSIGNE : Le JSON DOIT contenir exactement "intention", "confidence_score", "core_idea", "extracted_entities".
        """;
    }

    private sealed record OllamaJsonResponse
    {
        [JsonPropertyName("message")] public OllamaResponseMessage? Message { get; set; }
        [JsonPropertyName("done")] public bool Done { get; set; }
    }

    private sealed record OllamaResponseMessage
    {
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed record ProfilerOutput
    {
        public string? Intention { get; set; }
        public float ConfidenceScore { get; set; }
        public string? CoreIdea { get; set; }
        public ProfilerExtractedEntities? ExtractedEntities { get; set; }
    }

    private sealed record ProfilerExtractedEntities
    {
        public string? EmployeeName { get; set; }
        public string? EmployeeId { get; set; }
        public string? LeaveRequestId { get; set; }
        public string? Category { get; set; }
        public string? Amount { get; set; }
        public string? DateReference { get; set; }
        public string? Days { get; set; }
        public string? Query { get; set; }
    }
}
