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

    public async Task<string?> GenererAsync(string modele, string systemPrompt, string prompt, CancellationToken ct = default)
    {
        var requete = new OllamaGenerateRequest(modele, systemPrompt, prompt, Stream: false);

        HttpResponseMessage reponseHttp;
        try
        {
            reponseHttp = await _http.PostAsJsonAsync("/api/generate", requete, OptionsJson, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        using (reponseHttp)
        {
            if (!reponseHttp.IsSuccessStatusCode)
                return null;

            var corps = await reponseHttp.Content.ReadFromJsonAsync<OllamaGenerateResponse>(OptionsJson, ct);
            return corps?.Response;
        }
    }

    /// <summary>
    /// Variante streaming (stream=true) : Ollama renvoie du NDJSON, une frame par fragment de
    /// texte généré, terminée par une frame done=true. HttpCompletionOption.ResponseHeadersRead
    /// est essentiel ici — sans lui, HttpClient bufferise la réponse entière avant de la
    /// retourner et on perd tout l'intérêt du streaming (le client SSE ne verrait rien avant la
    /// fin de la génération complète).
    /// </summary>
    public async IAsyncEnumerable<string> GenererStreamAsync(
        string modele, string systemPrompt, string prompt, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var requete = new OllamaGenerateRequest(modele, systemPrompt, prompt, Stream: true);

        HttpResponseMessage? reponseHttp = null;
        var echec = false;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
            {
                Content = JsonContent.Create(requete, options: OptionsJson)
            };
            reponseHttp = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!reponseHttp.IsSuccessStatusCode)
                echec = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            echec = true;
        }

        if (echec)
        {
            reponseHttp?.Dispose();
            yield break;
        }

        var reponseValide = reponseHttp!;
        using (reponseValide)
        await using (var stream = await reponseValide.Content.ReadAsStreamAsync(ct))
        using (var reader = new StreamReader(stream))
        {
            while (!reader.EndOfStream)
            {
                var ligne = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(ligne))
                    continue;

                var frame = DeserialiserFrameSecurise(ligne);
                if (!string.IsNullOrEmpty(frame?.Response))
                    yield return frame.Response;

                if (frame?.Done == true)
                    yield break;
            }
        }
    }

    private static OllamaGenerateResponse? DeserialiserFrameSecurise(string ligne)
    {
        try
        {
            return JsonSerializer.Deserialize<OllamaGenerateResponse>(ligne, OptionsJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record OllamaGenerateRequest(string Model, string System, string Prompt, bool Stream);

    private sealed record OllamaGenerateResponse(string? Response, bool Done);
}
