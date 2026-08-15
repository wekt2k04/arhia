using Agirh.Infrastructure.Llm;
using FluentAssertions;

namespace Agirh.Tests.Llm;

/// <summary>
/// Tests d'integration reels contre Ollama local. Se termine sans assertion si indisponible.
/// </summary>
public class OllamaGeneratorAdapterTests
{
    private static async Task<bool> OllamaDisponibleAsync()
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(3) };
            var reponse = await http.GetAsync("/api/tags");
            return reponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task GenererReponseAsync_ContexteFourni_ProduitUneReponseNonVide()
    {
        if (!await OllamaDisponibleAsync()) return;

        var adapter = new OllamaGeneratorAdapter(new OllamaClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(60) }));

        var systemPrompt =
            "Réponds uniquement à partir de ce contexte : « La politique de mot de passe exige au moins 10 caractères. » " +
            "Si l'information n'y est pas, dis que tu ne l'as pas trouvée.";

        var reponse = await adapter.GenererReponseAsync(systemPrompt, "Quelle est la longueur minimale du mot de passe ?");

        reponse.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenererReponseAsync_ServiceIndisponible_RetourneMessageDeReplilGracieux()
    {
        var adapter = new OllamaGeneratorAdapter(new OllamaClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:1"), Timeout = TimeSpan.FromSeconds(2) }));

        var reponse = await adapter.GenererReponseAsync("system", "question");

        reponse.Should().Contain("problème technique");
    }

    [Fact]
    public async Task GenererReponseEnStreamingAsync_ContexteFourni_ProduitAuMoinsUnFragmentEtReconstitueUneReponseNonVide()
    {
        if (!await OllamaDisponibleAsync()) return;

        var adapter = new OllamaGeneratorAdapter(new OllamaClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(60) }));

        var systemPrompt =
            "Réponds uniquement à partir de ce contexte : « La politique de mot de passe exige au moins 10 caractères. » " +
            "Si l'information n'y est pas, dis que tu ne l'as pas trouvée.";

        var fragments = new List<string>();
        await foreach (var fragment in adapter.GenererReponseEnStreamingAsync(systemPrompt, "Quelle est la longueur minimale du mot de passe ?"))
            fragments.Add(fragment);

        fragments.Should().NotBeEmpty();
        string.Concat(fragments).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenererReponseEnStreamingAsync_ServiceIndisponible_RetourneUnSeulFragmentDeReplilGracieux()
    {
        var adapter = new OllamaGeneratorAdapter(new OllamaClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:1"), Timeout = TimeSpan.FromSeconds(2) }));

        var fragments = new List<string>();
        await foreach (var fragment in adapter.GenererReponseEnStreamingAsync("system", "question"))
            fragments.Add(fragment);

        fragments.Should().ContainSingle(f => f.Contains("problème technique"));
    }
}
