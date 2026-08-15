using System.Net.Http.Json;
using System.Text.Json;

namespace Agirh.Infrastructure.Llm;

/// <summary>
/// Client HTTP bas niveau pour l'API Ollama locale (POST /api/generate, stream=false).
/// Partagé par le routeur et le générateur (STACK_TECHNIQUE.md #5).
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

    private sealed record OllamaGenerateRequest(string Model, string System, string Prompt, bool Stream);

    private sealed record OllamaGenerateResponse(string? Response, bool Done);
}
