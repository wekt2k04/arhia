using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agirh.Core.Settings;

namespace Agirh.Infrastructure.Services;

/// <summary>
/// Précharge les modèles Ollama (Profiler/Synthesizer/Checker + Embedding) en
/// VRAM au démarrage de l'API, pour éviter le cold-start (~5-6s par modèle,
/// observé en logs) sur la toute première requête utilisateur. Tourne en
/// tâche de fond (BackgroundService) : ne bloque jamais le démarrage de
/// Kestrel, et un échec de warmup (Ollama pas encore prêt) est avalé — la
/// première requête réelle retombera simplement sur le comportement actuel.
/// </summary>
public sealed class OllamaWarmupService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IOptions<AIOptions> _options;
    private readonly ILogger<OllamaWarmupService> _logger;

    public OllamaWarmupService(
        IHttpClientFactory httpClientFactory,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOptions<AIOptions> options,
        ILogger<OllamaWarmupService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _embeddingGenerator = embeddingGenerator;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var ai = _options.Value;
            var endpoint = string.IsNullOrWhiteSpace(ai.Endpoint) ? "http://localhost:11434" : ai.Endpoint;
            var keepAlive = string.IsNullOrWhiteSpace(ai.OllamaKeepAlive) ? "30m" : ai.OllamaKeepAlive;

            // Même cascade de repli que ProfilerService/SynthesizerAgent/CheckerAgent —
            // cible exactement les modèles réellement appelés par le pipeline.
            var profilerModel = string.IsNullOrWhiteSpace(ai.ProfilerModel) ? "phi4-mini:3.8b" : ai.ProfilerModel;
            var synthesizerModel = !string.IsNullOrWhiteSpace(ai.SynthesizerModel) ? ai.SynthesizerModel : profilerModel;
            var checkerModel = !string.IsNullOrWhiteSpace(ai.CheckerModel)
                ? ai.CheckerModel
                : !string.IsNullOrWhiteSpace(ai.WorkerModel) ? ai.WorkerModel : "qwen3.5:9b";

            var chatModels = new[] { profilerModel, synthesizerModel, checkerModel }
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogInformation(
                "OLLAMA_WARMUP — démarrage, {Count} modèle(s) de chat distinct(s) + 1 modèle d'embedding",
                chatModels.Count);

            // Séquentiel (pas Task.WhenAll) : évite de charger 2 modèles en VRAM
            // simultanément sur du matériel de bureau/personnel non dédié.
            foreach (var model in chatModels)
            {
                if (stoppingToken.IsCancellationRequested) return;
                await WarmupChatModelAsync(model, endpoint, keepAlive, stoppingToken);
            }

            await WarmupEmbeddingModelAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Arrêt normal de l'hôte pendant le warmup — rien à signaler.
        }
        catch (Exception ex)
        {
            // Défense en profondeur : un warmup qui échoue ne doit JAMAIS faire
            // planter l'API (BackgroundServiceExceptionBehavior par défaut = StopHost).
            _logger.LogWarning(ex, "OLLAMA_WARMUP — échec inattendu, warmup abandonné (l'API démarre normalement)");
        }
    }

    private async Task WarmupChatModelAsync(string model, string endpoint, string keepAlive, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var http = _httpClientFactory.CreateClient("OllamaClient");
            http.BaseAddress = new Uri(endpoint);
            http.Timeout = TimeSpan.FromSeconds(120);

            // Pas de "prompt" : Ollama charge le modèle en VRAM sans générer de token.
            var request = new { model, stream = false, keep_alive = keepAlive };
            var response = await http.PostAsJsonAsync("/api/generate", request, ct);
            response.EnsureSuccessStatusCode();

            sw.Stop();
            _logger.LogInformation("OLLAMA_WARMUP — {Model} chargé en {Ms}ms", model, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OLLAMA_WARMUP — échec pour {Model} après {Ms}ms — ignoré (Ollama probablement pas encore prêt)", model, sw.ElapsedMilliseconds);
        }
    }

    private async Task WarmupEmbeddingModelAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Un modèle d'embedding pur ne répond pas à /api/generate côté Ollama —
            // on réutilise l'abstraction déjà en DI (/api/embed), qui applique aussi
            // le keep_alive configuré (cf. OllamaEmbeddingGenerator.EmbedRequest).
            await _embeddingGenerator.GenerateAsync(["warmup"], cancellationToken: ct);

            sw.Stop();
            _logger.LogInformation("OLLAMA_WARMUP — modèle d'embedding chargé en {Ms}ms", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OLLAMA_WARMUP — échec pour le modèle d'embedding après {Ms}ms — ignoré", sw.ElapsedMilliseconds);
        }
    }
}
