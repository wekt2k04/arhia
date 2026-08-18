using Agirh.Core.Ports;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Agirh.Infrastructure.Rag;

/// <summary>
/// Phase 4 (docs/STACK_TECHNIQUE.md #4, obligatoire) : reranking cross-encoder ONNX.
/// Un seul logit de pertinence par paire (requête, document), passé au sigmoïde.
/// </summary>
public sealed class OnnxRerankerAdapter : IRerankerPort, IDisposable
{
    private readonly InferenceSession _session;
    private readonly XlmRobertaTokenizer _tokenizer;

    public OnnxRerankerAdapter(string onnxModelPath, string sentencePieceModelPath)
    {
        _session = new InferenceSession(onnxModelPath);
        _tokenizer = XlmRobertaTokenizer.LoadFromFile(sentencePieceModelPath);
    }

    public Task<IReadOnlyList<DocumentChunk>> RerankAsync(
        string query,
        IReadOnlyList<DocumentChunk> candidates,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (candidates.Count == 0)
            return Task.FromResult<IReadOnlyList<DocumentChunk>>(Array.Empty<DocumentChunk>());

        var results = candidates
            .Select(c => c with { Score = ComputeScore(query, c.Content) })
            .OrderByDescending(c => c.Score)
            .ToList();

        return Task.FromResult<IReadOnlyList<DocumentChunk>>(results);
    }

    private float ComputeScore(string query, string document)
    {
        var ids = _tokenizer.EncodePairToHuggingFaceIds(query, document);
        var length = ids.Length;

        var inputIds = new DenseTensor<long>(new[] { 1, length });
        var attentionMask = new DenseTensor<long>(new[] { 1, length });
        for (var i = 0; i < length; i++)
        {
            inputIds[0, i] = ids[i];
            attentionMask[0, i] = 1;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };

        using var onnxResults = _session.Run(inputs);
        var logit = onnxResults.First(r => r.Name == "logits").AsTensor<float>()[0, 0];

        return Sigmoid(logit);
    }

    private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));

    public void Dispose() => _session.Dispose();
}
