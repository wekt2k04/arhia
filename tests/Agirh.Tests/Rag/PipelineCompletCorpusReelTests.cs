using Agirh.Core.Ports;
using Agirh.Infrastructure.Rag;
using FluentAssertions;
using Qdrant.Client;

namespace Agirh.Tests.Rag;

/// <summary>
/// Test capstone : chaine les 4 phases (STACK_TECHNIQUE.md #4) sur un vrai document du
/// corpus (corpus/01_politique_onboarding.md), avec les vrais modeles ONNX et un vrai
/// Qdrant. Se termine sans assertion si l'un des prerequis (modeles, Qdrant) est absent.
/// </summary>
public class PipelineCompletCorpusReelTests
{
    private static string CheminEmbeddingOnnx => Path.Combine(RepoPaths.ModelesEmbedding, "model_quantized.onnx");
    private static string CheminEmbeddingSpm => Path.Combine(RepoPaths.ModelesEmbedding, "sentencepiece.bpe.model");
    private static string CheminRerankerOnnx => Path.Combine(RepoPaths.ModelesReranker, "model_quantized.onnx");
    private static string CheminRerankerSpm => Path.Combine(RepoPaths.ModelesReranker, "sentencepiece.bpe.model");
    private static string CheminDocumentCorpus => Path.Combine(RepoPaths.Racine, "corpus", "01_politique_onboarding.md");

    [Fact]
    public async Task PipelineComplet_ChunkingEmbeddingQdrantReranking_SurUnVraiDocumentDuCorpus()
    {
        if (!File.Exists(CheminEmbeddingOnnx) || !File.Exists(CheminEmbeddingSpm)) return;
        if (!File.Exists(CheminRerankerOnnx) || !File.Exists(CheminRerankerSpm)) return;
        if (!File.Exists(CheminDocumentCorpus)) return;

        // Phase 1 : chunking structurel du vrai document
        var tokenizerPourComptage = XlmRobertaTokenizer.ChargerDepuisFichier(CheminEmbeddingSpm);
        var chunker = new MarkdownChunker(tokenizerPourComptage.CompterTokens, maxTokensParChunk: 400, tauxRecouvrement: 0.15);
        var markdown = await File.ReadAllTextAsync(CheminDocumentCorpus);
        var chunksBruts = chunker.Decouper(markdown);
        chunksBruts.Should().NotBeEmpty();

        // Phase 2 + 3 : embedding + indexation Qdrant de chaque chunk
        var client = new QdrantClient("localhost");
        using var embedder = new OnnxEmbeddingAdapter(CheminEmbeddingOnnx, CheminEmbeddingSpm);
        var vectorSearch = new QdrantVectorSearchAdapter(client, embedder.Dimension);

        try
        {
            await vectorSearch.PreparerAsync();
        }
        catch
        {
            return;
        }

        var index = 0;
        foreach (var chunkBrut in chunksBruts)
        {
            var chunk = new ChunkDocumentaire(
                ChunkDocumentaire.CalculerId("corpus-test:01_politique_onboarding.md", index),
                "corpus-test:01_politique_onboarding.md",
                index,
                chunkBrut.CheminTitres,
                chunkBrut.Contenu);
            var vecteur = await embedder.GenererEmbeddingAsync(chunkBrut.Contenu);
            await vectorSearch.IndexerAsync(chunk, vecteur);
            index++;
        }

        await Task.Delay(500);

        // Phase 2 (requete) + 3 (recherche) + 4 (reranking)
        using var reranker = new OnnxRerankerAdapter(CheminRerankerOnnx, CheminRerankerSpm);
        var requete = "Qui est responsable de créer la fiche d'un nouveau collaborateur ?";
        var vecteurRequete = await embedder.GenererEmbeddingAsync(requete);
        var candidats = await vectorSearch.RechercherAsync(vecteurRequete, topK: 5);
        candidats.Should().NotBeEmpty();

        var candidatsCorpusTest = candidats.Where(c => c.DocumentSource == "corpus-test:01_politique_onboarding.md").ToList();
        candidatsCorpusTest.Should().NotBeEmpty("la recherche doit retrouver au moins un chunk du document qu'on vient d'indexer");

        var resultatsRerankes = await reranker.RerankAsync(requete, candidatsCorpusTest);

        resultatsRerankes.Should().NotBeEmpty();
        resultatsRerankes[0].Contenu.Should().ContainAny("RH", "fiche", "collaborateur", "pôle");
    }
}
