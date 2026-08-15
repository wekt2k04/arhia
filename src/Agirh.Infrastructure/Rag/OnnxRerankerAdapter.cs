using Agirh.Core.Ports;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Agirh.Infrastructure.Rag;

/// <summary>
/// Phase 4 (STACK_TECHNIQUE.md #4, obligatoire) : reranking cross-encoder ONNX.
/// Un seul logit de pertinence par paire (requête, document), passé au sigmoïde.
/// </summary>
public sealed class OnnxRerankerAdapter : IRerankerPort, IDisposable
{
    private readonly InferenceSession _session;
    private readonly XlmRobertaTokenizer _tokenizer;

    public OnnxRerankerAdapter(string cheminModeleOnnx, string cheminSentencePieceModel)
    {
        _session = new InferenceSession(cheminModeleOnnx);
        _tokenizer = XlmRobertaTokenizer.ChargerDepuisFichier(cheminSentencePieceModel);
    }

    public Task<IReadOnlyList<ChunkDocumentaire>> RerankAsync(
        string requete,
        IReadOnlyList<ChunkDocumentaire> candidats,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (candidats.Count == 0)
            return Task.FromResult<IReadOnlyList<ChunkDocumentaire>>(Array.Empty<ChunkDocumentaire>());

        var resultats = candidats
            .Select(c => c with { Score = CalculerScore(requete, c.Contenu) })
            .OrderByDescending(c => c.Score)
            .ToList();

        return Task.FromResult<IReadOnlyList<ChunkDocumentaire>>(resultats);
    }

    private float CalculerScore(string requete, string document)
    {
        var ids = _tokenizer.EncoderPaireEnIdsHuggingFace(requete, document);
        var longueur = ids.Length;

        var inputIds = new DenseTensor<long>(new[] { 1, longueur });
        var attentionMask = new DenseTensor<long>(new[] { 1, longueur });
        for (var i = 0; i < longueur; i++)
        {
            inputIds[0, i] = ids[i];
            attentionMask[0, i] = 1;
        }

        var entrees = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };

        using var resultatsOnnx = _session.Run(entrees);
        var logit = resultatsOnnx.First(r => r.Name == "logits").AsTensor<float>()[0, 0];

        return Sigmoid(logit);
    }

    private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));

    public void Dispose() => _session.Dispose();
}
