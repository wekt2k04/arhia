using Agirh.Core.Ports;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Agirh.Infrastructure.Rag;

/// <summary>
/// Phase 2 (docs/STACK_TECHNIQUE.md #4) : embedding multilingue via ONNX Runtime .NET pur.
/// Le graphe ONNX retourne last_hidden_state (par token) — le mean-pooling masque + la
/// normalisation L2 sont faits ici pour obtenir l'embedding de phrase (768d).
/// </summary>
public sealed class OnnxEmbeddingAdapter : IEmbeddingPort, IDisposable
{
    public int Dimension { get; } = 768;

    private readonly InferenceSession _session;
    private readonly XlmRobertaTokenizer _tokenizer;

    public OnnxEmbeddingAdapter(string onnxModelPath, string sentencePieceModelPath)
    {
        _session = new InferenceSession(onnxModelPath);
        _tokenizer = XlmRobertaTokenizer.LoadFromFile(sentencePieceModelPath);
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var ids = _tokenizer.EncodeToHuggingFaceIds(text);
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

        using var results = _session.Run(inputs);
        var lastHiddenState = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();

        return Task.FromResult(MeanPoolAndNormalize(lastHiddenState, length));
    }

    private float[] MeanPoolAndNormalize(Tensor<float> lastHiddenState, int length)
    {
        var embedding = new float[Dimension];

        for (var t = 0; t < length; t++)
        {
            for (var d = 0; d < Dimension; d++)
            {
                embedding[d] += lastHiddenState[0, t, d];
            }
        }

        for (var d = 0; d < Dimension; d++)
        {
            embedding[d] /= length;
        }

        var norm = MathF.Sqrt(embedding.Sum(v => v * v));
        if (norm > 0)
        {
            for (var d = 0; d < Dimension; d++)
            {
                embedding[d] /= norm;
            }
        }

        return embedding;
    }

    public void Dispose() => _session.Dispose();
}
