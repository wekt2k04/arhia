using Agirh.Core.Ports;
using Agirh.Infrastructure.Rag;
using FluentAssertions;
using Qdrant.Client;

namespace Agirh.Tests.Rag;

/// <summary>
/// Test d'integration de bout en bout du pipeline RAG (embedding ONNX reel + Qdrant reel).
/// Necessite les modeles telecharges (.claude/scripts/download-models.ps1) et un conteneur Qdrant
/// accessible sur localhost:6334 (docker start agirh-qdrant). Se termine sans assertion si
/// l'un des deux est absent, plutot que d'echouer bloquant la suite sur une machine fraiche.
/// </summary>
public class RagPipelineIntegrationTests
{
    private static string OnnxPath => Path.Combine(RepoPaths.ModelesEmbedding, "model_quantized.onnx");
    private static string SpmPath => Path.Combine(RepoPaths.ModelesEmbedding, "sentencepiece.bpe.model");

    [Fact]
    public async Task FullPipeline_EmbedIndexSearch_TheRelevantChunkComesFirst()
    {
        if (!File.Exists(OnnxPath) || !File.Exists(SpmPath)) return;

        var client = new QdrantClient("localhost");
        using var embedder = new OnnxEmbeddingAdapter(OnnxPath, SpmPath);
        var vectorSearch = new QdrantVectorSearchAdapter(client, embedder.Dimension);

        try
        {
            await vectorSearch.PrepareAsync();
        }
        catch
        {
            return;
        }

        var relevantChunk = new DocumentChunk(
            DocumentChunk.ComputeId("test-integration.md", 0),
            "test-integration.md",
            0,
            "Test > Onboarding",
            "Le RH du pôle crée la fiche du collaborateur et déclenche l'instanciation de la checklist d'onboarding.");
        var irrelevantChunk = new DocumentChunk(
            DocumentChunk.ComputeId("test-integration.md", 1),
            "test-integration.md",
            1,
            "Test > Cuisine",
            "La recette de la tarte aux pommes nécessite du beurre, du sucre et des pommes.");

        var relevantVector = await embedder.GenerateEmbeddingAsync(relevantChunk.Content);
        var irrelevantVector = await embedder.GenerateEmbeddingAsync(irrelevantChunk.Content);
        await vectorSearch.IndexAsync(relevantChunk, relevantVector);
        await vectorSearch.IndexAsync(irrelevantChunk, irrelevantVector);

        await Task.Delay(500);

        var queryVector = await embedder.GenerateEmbeddingAsync("Comment un nouveau collaborateur est-il intégré ?");
        // topK large + filtre sur nos propres chunks : la collection Qdrant est persistante
        // (volume nomme) et accumule les donnees d'autres tests/executions - ne jamais supposer
        // que nos deux chunks sont les seuls presents.
        var results = (await vectorSearch.SearchAsync(queryVector, topK: 20))
            .Where(r => r.DocumentSource == "test-integration.md")
            .ToList();

        results.Should().NotBeEmpty();
        results[0].ChunkIndex.Should().Be(0);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SameText_ProducesANormalizedVectorOf768Dimensions()
    {
        if (!File.Exists(OnnxPath) || !File.Exists(SpmPath)) return;

        using var embedder = new OnnxEmbeddingAdapter(OnnxPath, SpmPath);

        var vector = await embedder.GenerateEmbeddingAsync("Politique d'onboarding et de sécurité de l'information.");

        vector.Should().HaveCount(768);
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        norm.Should().BeApproximately(1.0f, 0.01f, "l'embedding doit être normalisé L2 pour la similarité cosinus");
    }
}
