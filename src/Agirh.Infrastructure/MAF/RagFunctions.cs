using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agirh.Core.Interfaces;
using Agirh.Core.Settings;
using Agirh.Domain.Interfaces;

namespace Agirh.Infrastructure.MAF;

public sealed record RechercherInformationRagInput([property: JsonPropertyName("query")] string Requete);

public sealed partial class RagFunctions : MafToolBase<RechercherInformationRagInput>
{
    // Détection des marqueurs structurels d'injection de prompt (second-order attack
    // via chunks stockés en base) : balises XML system, tokens d'instructions LLM,
    // et formulations impératives connues comme vecteurs d'override.
    // N'élimine que les MARQUEURS — le contexte documentaire légitime est préservé.
    // Ligne de défense secondaire : la primaire est le system prompt du Synthesizer
    // qui déclare les blocs [DOCUMENT N] comme données uniquement.
    [GeneratedRegex(
        @"</?system>|\[/?INST\]|<</?SYS>>|<\|im_(?:start|end)\|>|" +
        @"\b(?:ignore|forget|disregard|oublie)\b.{0,60}?\b(?:instruction|rule|consigne|previous|précédent)\w*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "fr-FR")]
    private static partial Regex BuildInjectionRegex();

    private static readonly Regex InjectionRegex = BuildInjectionRegex();

    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IKnowledgeDocumentRepository _knowledgeRepo;
    private readonly ILogger<RagFunctions> _logger;
    private readonly IOptions<AIOptions> _options;

    public RagFunctions(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IKnowledgeDocumentRepository knowledgeRepo,
        ILogger<RagFunctions> logger,
        IOptions<AIOptions> options)
    {
        _embeddingGenerator = embeddingGenerator;
        _knowledgeRepo = knowledgeRepo;
        _logger = logger;
        _options = options;
    }

    public override string Name => "RechercherInformationRagAsync";
    public override string Description => "Recherche une information dans la base de connaissance RH en utilisant la recherche sémantique.";
    public override RoleFlags RequiredRoles => RoleFlags.All;

    // internal pour les tests unitaires (pas de réflexion / InternalsVisibleTo nécessaire
    // car les tests référencent directement Agirh.Infrastructure).
    internal static string SanitizeChunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return InjectionRegex.Replace(text, "[CONTENU FILTRÉ]");
    }

    internal static string SanitizeSourceFile(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "inconnu";
        var clean = string.Concat(name.Where(c => !char.IsControl(c) && c != '<' && c != '>'));
        return clean.Length > 200 ? clean[..200] : clean;
    }

    protected override async Task<string> ExecuteTypedAsync(
        RechercherInformationRagInput input,
        string? requestingUserId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Requete))
            return "Veuillez fournir une requête de recherche.";

        try
        {
            var queryEmbedding = await _embeddingGenerator.GenerateAsync(input.Requete, cancellationToken: ct);
            var floats = queryEmbedding.Vector.ToArray();
            var byteArray = new byte[floats.Length * 4];
            for (int i = 0; i < floats.Length; i++)
                Buffer.BlockCopy(BitConverter.GetBytes(floats[i]), 0, byteArray, i * 4, 4);

            var results = await _knowledgeRepo.SearchBySimilarityAsync(byteArray, _options.Value.RagTopN, ct);
            var resultList = results?.ToList();

            if (resultList == null || resultList.Count == 0)
            {
                _logger.LogInformation("RAG — 0 chunk returned for query '{Query}'",
                    input.Requete.Length > 80 ? input.Requete[..80] + "…" : input.Requete);
                return "Aucun résultat trouvé dans la base de connaissance pour votre recherche.";
            }

            _logger.LogInformation("RAG — {Count} chunk(s) returned for query '{Query}'",
                resultList.Count, input.Requete.Length > 80 ? input.Requete[..80] + "…" : input.Requete);

            // Chaque chunk est encapsulé dans des balises [DOCUMENT N] / [FIN DOCUMENT N]
            // qui permettent au Synthesizer de distinguer "source d'information" vs
            // "instruction système" (cf. system prompt SynthesizerAgent).
            var sb = new StringBuilder();
            sb.AppendLine($"Voici les {resultList.Count} résultats trouvés :");
            for (int i = 0; i < resultList.Count; i++)
            {
                var doc = resultList[i];
                sb.AppendLine();
                sb.AppendLine($"[DOCUMENT {i + 1} | Source : {SanitizeSourceFile(doc.SourceFile)}]");
                sb.AppendLine(SanitizeChunk(doc.ChunkText));
                sb.AppendLine($"[FIN DOCUMENT {i + 1}]");
            }
            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG search failed");
            return "Erreur lors de la recherche. Réessayez plus tard.";
        }
    }
}
