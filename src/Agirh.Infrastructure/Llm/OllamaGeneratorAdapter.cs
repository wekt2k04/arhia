using Agirh.Core.Ports;

namespace Agirh.Infrastructure.Llm;

/// <summary>
/// Générateur (STACK_TECHNIQUE.md #5) : phi4-mini:3.8b, même modèle que le routeur sur cette
/// infra (gemma4:12b testé en réel : plus de 2 minutes sans réponse, pas de GPU adapté ici —
/// voir STACK_TECHNIQUE.md §5).
/// </summary>
public sealed class OllamaGeneratorAdapter : ILlmGeneratorPort
{
    private const string Modele = "phi4-mini:3.8b";

    private const string MessageIndisponible =
        "Je rencontre un problème technique pour répondre à cette question. " +
        "Merci de réessayer, ou de contacter le RH de votre pôle.";

    private readonly OllamaClient _client;

    public OllamaGeneratorAdapter(OllamaClient client)
    {
        _client = client;
    }

    public async Task<string> GenererReponseAsync(string systemPrompt, string question, CancellationToken ct = default)
    {
        var reponse = await _client.GenererAsync(Modele, systemPrompt, question, ct);
        return string.IsNullOrWhiteSpace(reponse) ? MessageIndisponible : reponse.Trim();
    }
}
