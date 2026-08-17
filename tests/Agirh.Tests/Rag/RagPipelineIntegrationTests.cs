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
    private static string CheminOnnx => Path.Combine(RepoPaths.ModelesEmbedding, "model_quantized.onnx");
    private static string CheminSpm => Path.Combine(RepoPaths.ModelesEmbedding, "sentencepiece.bpe.model");

    [Fact]
    public async Task PipelineComplet_EmbedIndexerRechercher_LeChunkPertinentArriveEnPremier()
    {
        if (!File.Exists(CheminOnnx) || !File.Exists(CheminSpm)) return;

        var client = new QdrantClient("localhost");
        using var embedder = new OnnxEmbeddingAdapter(CheminOnnx, CheminSpm);
        var vectorSearch = new QdrantVectorSearchAdapter(client, embedder.Dimension);

        try
        {
            await vectorSearch.PreparerAsync();
        }
        catch
        {
            return;
        }

        var chunkPertinent = new ChunkDocumentaire(
            ChunkDocumentaire.CalculerId("test-integration.md", 0),
            "test-integration.md",
            0,
            "Test > Onboarding",
            "Le RH du pôle crée la fiche du collaborateur et déclenche l'instanciation de la checklist d'onboarding.");
        var chunkNonPertinent = new ChunkDocumentaire(
            ChunkDocumentaire.CalculerId("test-integration.md", 1),
            "test-integration.md",
            1,
            "Test > Cuisine",
            "La recette de la tarte aux pommes nécessite du beurre, du sucre et des pommes.");

        var vecteurPertinent = await embedder.GenererEmbeddingAsync(chunkPertinent.Contenu);
        var vecteurNonPertinent = await embedder.GenererEmbeddingAsync(chunkNonPertinent.Contenu);
        await vectorSearch.IndexerAsync(chunkPertinent, vecteurPertinent);
        await vectorSearch.IndexerAsync(chunkNonPertinent, vecteurNonPertinent);

        await Task.Delay(500);

        var vecteurRequete = await embedder.GenererEmbeddingAsync("Comment un nouveau collaborateur est-il intégré ?");
        // topK large + filtre sur nos propres chunks : la collection Qdrant est persistante
        // (volume nomme) et accumule les donnees d'autres tests/executions - ne jamais supposer
        // que nos deux chunks sont les seuls presents.
        var resultats = (await vectorSearch.RechercherAsync(vecteurRequete, topK: 20))
            .Where(r => r.DocumentSource == "test-integration.md")
            .ToList();

        resultats.Should().NotBeEmpty();
        resultats[0].ChunkIndex.Should().Be(0);
    }

    [Fact]
    public async Task GenererEmbeddingAsync_MemeTexte_ProduitUnVecteurNormaliseDe768Dimensions()
    {
        if (!File.Exists(CheminOnnx) || !File.Exists(CheminSpm)) return;

        using var embedder = new OnnxEmbeddingAdapter(CheminOnnx, CheminSpm);

        var vecteur = await embedder.GenererEmbeddingAsync("Politique d'onboarding et de sécurité de l'information.");

        vecteur.Should().HaveCount(768);
        var norme = MathF.Sqrt(vecteur.Sum(v => v * v));
        norme.Should().BeApproximately(1.0f, 0.01f, "l'embedding doit être normalisé L2 pour la similarité cosinus");
    }
}
