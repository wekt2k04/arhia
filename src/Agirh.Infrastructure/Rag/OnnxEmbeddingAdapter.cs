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

    public OnnxEmbeddingAdapter(string cheminModeleOnnx, string cheminSentencePieceModel)
    {
        _session = new InferenceSession(cheminModeleOnnx);
        _tokenizer = XlmRobertaTokenizer.ChargerDepuisFichier(cheminSentencePieceModel);
    }

    public Task<float[]> GenererEmbeddingAsync(string texte, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var ids = _tokenizer.EncoderEnIdsHuggingFace(texte);
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

        using var resultats = _session.Run(entrees);
        var dernierEtatCache = resultats.First(r => r.Name == "last_hidden_state").AsTensor<float>();

        return Task.FromResult(MoyennerEtNormaliser(dernierEtatCache, longueur));
    }

    private float[] MoyennerEtNormaliser(Tensor<float> dernierEtatCache, int longueur)
    {
        var embedding = new float[Dimension];

        for (var t = 0; t < longueur; t++)
        {
            for (var d = 0; d < Dimension; d++)
            {
                embedding[d] += dernierEtatCache[0, t, d];
            }
        }

        for (var d = 0; d < Dimension; d++)
        {
            embedding[d] /= longueur;
        }

        var norme = MathF.Sqrt(embedding.Sum(v => v * v));
        if (norme > 0)
        {
            for (var d = 0; d < Dimension; d++)
            {
                embedding[d] /= norme;
            }
        }

        return embedding;
    }

    public void Dispose() => _session.Dispose();
}
