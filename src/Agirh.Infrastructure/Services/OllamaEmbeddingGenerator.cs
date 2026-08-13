using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agirh.Core.Settings;

namespace Agirh.Infrastructure.Services;

public class OllamaEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly int _expectedDimension;
    private readonly string _keepAlive;
    private readonly ILogger<OllamaEmbeddingGenerator> _logger;

    public OllamaEmbeddingGenerator(HttpClient http, IOptions<AIOptions> options, ILogger<OllamaEmbeddingGenerator> logger)
    {
        _http = http;
        _logger = logger;
        _model = string.IsNullOrWhiteSpace(options.Value.EmbeddingModel) ? "embeddinggemma" : options.Value.EmbeddingModel;
        _expectedDimension = options.Value.EmbeddingExpectedDimension;
        _keepAlive = string.IsNullOrWhiteSpace(options.Value.OllamaKeepAlive) ? "30m" : options.Value.OllamaKeepAlive;
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = values.ToList();
        var request = new EmbedRequest { Model = _model, Input = list, KeepAlive = _keepAlive };

        try
        {
            var response = await _http.PostAsJsonAsync("/api/embed", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogCritical(
                    "Ollama HTTP {StatusCode} on /api/embed — body: {Body}",
                    (int)response.StatusCode, errorBody);
            }
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken: cancellationToken);

            if (result?.Embeddings == null || result.Embeddings.Count == 0)
                throw new InvalidOperationException("Empty embedding response from Ollama");

            // Garde de dimension (R1) : le schéma est vector({_expectedDimension}) —
            // un modèle inadapté (ex. 384 dims) doit échouer proprement ICI, avant
            // toute écriture DB / tout VECTOR_DISTANCE, au lieu d'une SqlException.
            if (result.Embeddings.Count != list.Count)
                throw new InvalidOperationException(
                    $"Ollama a renvoyé {result.Embeddings.Count} embeddings pour {list.Count} entrées.");

            var embeddings = new List<Embedding<float>>(result.Embeddings.Count);
            foreach (var raw in result.Embeddings)
            {
                var vector = raw.ToArray();
                if (vector.Length != _expectedDimension)
                    throw new InvalidOperationException(
                        $"Modèle d'embedding '{_model}' : dimension {vector.Length} attendue {_expectedDimension} (vector({_expectedDimension})). "
                        + "Vérifiez AI:EmbeddingModel / Embedding:ExpectedDimension.");
                embeddings.Add(new Embedding<float>(vector));
            }
            return new GeneratedEmbeddings<Embedding<float>>(embeddings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding failed");
            throw;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }

    private class EmbedRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("input")] public List<string> Input { get; set; } = new();
        [JsonPropertyName("keep_alive")] public string KeepAlive { get; set; } = "";
    }

    private class EmbedResponse
    {
        [JsonPropertyName("embeddings")] public List<List<float>> Embeddings { get; set; } = new();
    }
}
