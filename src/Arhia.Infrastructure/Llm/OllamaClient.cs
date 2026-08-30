using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arhia.Infrastructure.Llm;

/// <summary>
/// Paramètres d'échantillonnage transmis à Ollama (champ "options" de /api/generate).
/// MESURÉ EMPIRIQUEMENT sur le jeu de Q/R gold (rag/eval/gold_qa.json, EvaluationGoldEndToEndTests,
/// 2026-08-30) — pas supposé : baisser la température du routeur (essayé à 0.0 puis à 0.15, avec
/// top_p réduit) a fait BAISSER le score gold à chaque fois (30/48 aux valeurs par défaut d'Ollama
/// -> 25/48 dans les deux cas), jamais amélioré. Cause probable : sur un modèle 3.8B qui n'a pas
/// vraiment "compris" la règle de classification (juste appris un pattern approximatif), une
/// température basse rend l'erreur déterministe à 100 % sur les cas limites au lieu de laisser une
/// chance de tomber juste par hasard sur certains tirages. Conclusion retenue : ne PAS s'éloigner
/// des valeurs par défaut d'Ollama sur ce modèle — Router/Generator utilisent donc explicitement
/// les mêmes valeurs par défaut qu'Ollama appliquerait de toute façon (température 0.8, top_p 0.9),
/// pour que ce soit documenté et intentionnel plutôt qu'implicite. Le vrai levier pour améliorer le
/// routage n'est pas ici : c'est la taille du modèle (voir NEXT_SESSION.md — passage prévu à
/// phi4:14b sur le serveur Ollama entreprise, pas testable en local faute de GPU).
/// </summary>
public sealed record OllamaOptions(
    double Temperature,
    [property: JsonPropertyName("top_p")] double TopP = 0.9)
{
    public static readonly OllamaOptions Router = new(Temperature: 0.8, TopP: 0.9);
    public static readonly OllamaOptions Generator = new(Temperature: 0.8, TopP: 0.9);
}

/// <summary>
/// Client HTTP bas niveau pour l'API Ollama locale (POST /api/generate, stream=false).
/// Partagé par le routeur et le générateur (docs/STACK_TECHNIQUE.md #5).
/// </summary>
public sealed class OllamaClient
{
    private static readonly JsonSerializerOptions OptionsJson = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public OllamaClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string?> GenerateAsync(string model, string systemPrompt, string prompt, OllamaOptions options, CancellationToken ct = default)
    {
        var request = new OllamaGenerateRequest(model, systemPrompt, prompt, Stream: false, options);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _http.PostAsJsonAsync("/api/generate", request, OptionsJson, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        using (httpResponse)
        {
            if (!httpResponse.IsSuccessStatusCode)
                return null;

            var body = await httpResponse.Content.ReadFromJsonAsync<OllamaGenerateResponse>(OptionsJson, ct);
            return body?.Response;
        }
    }

    /// <summary>
    /// Variante streaming (stream=true) : Ollama renvoie du NDJSON, une frame par fragment de
    /// texte généré, terminée par une frame done=true. HttpCompletionOption.ResponseHeadersRead
    /// est essentiel ici — sans lui, HttpClient bufferise la réponse entière avant de la
    /// retourner et on perd tout l'intérêt du streaming (le client SSE ne verrait rien avant la
    /// fin de la génération complète).
    /// </summary>
    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string model, string systemPrompt, string prompt, OllamaOptions options, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new OllamaGenerateRequest(model, systemPrompt, prompt, Stream: true, options);

        HttpResponseMessage? httpResponse = null;
        var failed = false;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
            {
                Content = JsonContent.Create(request, options: OptionsJson)
            };
            httpResponse = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!httpResponse.IsSuccessStatusCode)
                failed = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            failed = true;
        }

        if (failed)
        {
            httpResponse?.Dispose();
            yield break;
        }

        var validResponse = httpResponse!;
        using (validResponse)
        await using (var stream = await validResponse.Content.ReadAsStreamAsync(ct))
        using (var reader = new StreamReader(stream))
        {
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var frame = DeserializeFrameSafely(line);
                if (!string.IsNullOrEmpty(frame?.Response))
                    yield return frame.Response;

                if (frame?.Done == true)
                    yield break;
            }
        }
    }

    private static OllamaGenerateResponse? DeserializeFrameSafely(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<OllamaGenerateResponse>(line, OptionsJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record OllamaGenerateRequest(string Model, string System, string Prompt, bool Stream, OllamaOptions Options);

    private sealed record OllamaGenerateResponse(string? Response, bool Done);
}
