using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Agirh.Infrastructure.Llm;

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

    public async Task<string?> GenerateAsync(string model, string systemPrompt, string prompt, CancellationToken ct = default)
    {
        var request = new OllamaGenerateRequest(model, systemPrompt, prompt, Stream: false);

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
        string model, string systemPrompt, string prompt, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new OllamaGenerateRequest(model, systemPrompt, prompt, Stream: true);

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

    private sealed record OllamaGenerateRequest(string Model, string System, string Prompt, bool Stream);

    private sealed record OllamaGenerateResponse(string? Response, bool Done);
}
