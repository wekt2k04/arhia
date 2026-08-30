using Arhia.Infrastructure.Llm;
using FluentAssertions;

namespace Arhia.Tests.Llm;

/// <summary>
/// Tests d'integration reels contre Ollama local. Se termine sans assertion si indisponible.
/// </summary>
public class OllamaGeneratorAdapterTests
{
    private static async Task<bool> OllamaAvailableAsync()
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(3) };
            var response = await http.GetAsync("/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task GenerateResponseAsync_ContextProvided_ProducesANonEmptyResponse()
    {
        if (!await OllamaAvailableAsync()) return;

        var adapter = new OllamaGeneratorAdapter(new OllamaClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(60) }));

        var systemPrompt =
            "Réponds uniquement à partir de ce contexte : « La politique de mot de passe exige au moins 10 caractères. » " +
            "Si l'information n'y est pas, dis que tu ne l'as pas trouvée.";

        var response = await adapter.GenerateResponseAsync(systemPrompt, "Quelle est la longueur minimale du mot de passe ?");

        response.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateResponseAsync_ServiceUnavailable_ReturnsGracefulFallbackMessage()
    {
        var adapter = new OllamaGeneratorAdapter(new OllamaClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:1"), Timeout = TimeSpan.FromSeconds(2) }));

        var response = await adapter.GenerateResponseAsync("system", "question");

        response.Should().Contain("problème technique");
    }

    [Fact]
    public async Task GenerateResponseStreamingAsync_ContextProvided_ProducesAtLeastOneFragmentAndReassemblesANonEmptyResponse()
    {
        if (!await OllamaAvailableAsync()) return;

        var adapter = new OllamaGeneratorAdapter(new OllamaClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(60) }));

        var systemPrompt =
            "Réponds uniquement à partir de ce contexte : « La politique de mot de passe exige au moins 10 caractères. » " +
            "Si l'information n'y est pas, dis que tu ne l'as pas trouvée.";

        var fragments = new List<string>();
        await foreach (var fragment in adapter.GenerateResponseStreamingAsync(systemPrompt, "Quelle est la longueur minimale du mot de passe ?"))
            fragments.Add(fragment);

        fragments.Should().NotBeEmpty();
        string.Concat(fragments).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateResponseStreamingAsync_ServiceUnavailable_ReturnsASingleGracefulFallbackFragment()
    {
        var adapter = new OllamaGeneratorAdapter(new OllamaClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:1"), Timeout = TimeSpan.FromSeconds(2) }));

        var fragments = new List<string>();
        await foreach (var fragment in adapter.GenerateResponseStreamingAsync("system", "question"))
            fragments.Add(fragment);

        fragments.Should().ContainSingle(f => f.Contains("problème technique"));
    }
}
