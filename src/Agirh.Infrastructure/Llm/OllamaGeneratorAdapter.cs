using System.Runtime.CompilerServices;
using Agirh.Core.Ports;

namespace Agirh.Infrastructure.Llm;

/// <summary>
/// Générateur (docs/STACK_TECHNIQUE.md #5) : phi4-mini:3.8b par défaut sur cette infra (gemma4:12b
/// testé en réel : plus de 2 minutes sans réponse, pas de GPU adapté ici). Modèle configurable
/// (Ollama:GeneratorModele, Program.cs) pour permettre de basculer vers le serveur Ollama
/// d'entreprise du porteur du projet (modèles plus capables) sans recompiler.
/// </summary>
public sealed class OllamaGeneratorAdapter : ILlmGeneratorPort
{
    private const string MessageIndisponible =
        "Je rencontre un problème technique pour répondre à cette question. " +
        "Merci de réessayer, ou de contacter le RH de votre pôle.";

    private readonly OllamaClient _client;
    private readonly string _modele;

    public OllamaGeneratorAdapter(OllamaClient client, string modele = "phi4-mini:3.8b")
    {
        _client = client;
        _modele = modele;
    }

    public async Task<string> GenererReponseAsync(string systemPrompt, string question, CancellationToken ct = default)
    {
        var reponse = await _client.GenererAsync(_modele, systemPrompt, question, ct);
        return string.IsNullOrWhiteSpace(reponse) ? MessageIndisponible : reponse.Trim();
    }

    public async IAsyncEnumerable<string> GenererReponseEnStreamingAsync(
        string systemPrompt, string question, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var recuAuMoinsUnFragment = false;

        await foreach (var fragment in _client.GenererStreamAsync(_modele, systemPrompt, question, ct))
        {
            recuAuMoinsUnFragment = true;
            yield return fragment;
        }

        if (!recuAuMoinsUnFragment)
            yield return MessageIndisponible;
    }
}
