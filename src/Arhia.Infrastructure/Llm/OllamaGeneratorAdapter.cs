using System.Runtime.CompilerServices;
using Arhia.Core.Ports;

namespace Arhia.Infrastructure.Llm;

/// <summary>
/// Générateur (docs/STACK_TECHNIQUE.md #5) : phi4-mini:3.8b par défaut sur cette infra (gemma4:12b
/// testé en réel : plus de 2 minutes sans réponse, pas de GPU adapté ici). Modèle configurable
/// (Ollama:GeneratorModel, Program.cs) pour permettre de basculer vers le serveur Ollama
/// d'entreprise du porteur du projet (modèles plus capables) sans recompiler.
/// </summary>
public sealed class OllamaGeneratorAdapter : ILlmGeneratorPort
{
    private const string MessageIndisponible =
        "Je rencontre un problème technique pour répondre à cette question. " +
        "Merci de réessayer, ou de contacter le RH de votre pôle.";

    private readonly OllamaClient _client;
    private readonly string _model;

    public OllamaGeneratorAdapter(OllamaClient client, string model = "phi4-mini:3.8b")
    {
        _client = client;
        _model = model;
    }

    public async Task<string> GenerateResponseAsync(string systemPrompt, string question, CancellationToken ct = default)
    {
        var response = await _client.GenerateAsync(_model, systemPrompt, question, OllamaOptions.Generator, ct);
        return string.IsNullOrWhiteSpace(response) ? MessageIndisponible : response.Trim();
    }

    public async IAsyncEnumerable<string> GenerateResponseStreamingAsync(
        string systemPrompt, string question, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var receivedAtLeastOneFragment = false;

        await foreach (var fragment in _client.GenerateStreamAsync(_model, systemPrompt, question, OllamaOptions.Generator, ct))
        {
            receivedAtLeastOneFragment = true;
            yield return fragment;
        }

        if (!receivedAtLeastOneFragment)
            yield return MessageIndisponible;
    }
}
