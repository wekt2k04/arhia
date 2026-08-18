using Agirh.Core.Ports;
using Agirh.Infrastructure.Rag;
using FluentAssertions;
using Qdrant.Client;

namespace Agirh.Tests.Rag;

/// <summary>
/// Test capstone : chaine les 4 phases (docs/STACK_TECHNIQUE.md #4) sur un vrai document du
/// corpus (rag/corpus/01_politique_onboarding.md), avec les vrais modeles ONNX et un vrai
/// Qdrant. Se termine sans assertion si l'un des prerequis (modeles, Qdrant) est absent.
/// </summary>
public class PipelineCompletCorpusReelTests
{
    private static string EmbeddingOnnxPath => Path.Combine(RepoPaths.ModelesEmbedding, "model_quantized.onnx");
    private static string EmbeddingSpmPath => Path.Combine(RepoPaths.ModelesEmbedding, "sentencepiece.bpe.model");
    private static string RerankerOnnxPath => Path.Combine(RepoPaths.ModelesReranker, "model_quantized.onnx");
    private static string RerankerSpmPath => Path.Combine(RepoPaths.ModelesReranker, "sentencepiece.bpe.model");
    private static string CorpusDocumentPath => Path.Combine(RepoPaths.Racine, "rag", "corpus", "01_politique_onboarding.md");

    [Fact]
    public async Task FullPipeline_ChunkingEmbeddingQdrantReranking_OnARealCorpusDocument()
    {
        if (!File.Exists(EmbeddingOnnxPath) || !File.Exists(EmbeddingSpmPath)) return;
        if (!File.Exists(RerankerOnnxPath) || !File.Exists(RerankerSpmPath)) return;
        if (!File.Exists(CorpusDocumentPath)) return;

        // Phase 1 : chunking structurel du vrai document
        var tokenizerForCounting = XlmRobertaTokenizer.LoadFromFile(EmbeddingSpmPath);
        var chunker = new MarkdownChunker(tokenizerForCounting.CountTokens, maxTokensPerChunk: 400, overlapRatio: 0.15);
        var markdown = await File.ReadAllTextAsync(CorpusDocumentPath);
        var rawChunks = chunker.Chunk(markdown);
        rawChunks.Should().NotBeEmpty();

        // Phase 2 + 3 : embedding + indexation Qdrant de chaque chunk
        var client = new QdrantClient("localhost");
        using var embedder = new OnnxEmbeddingAdapter(EmbeddingOnnxPath, EmbeddingSpmPath);
        var vectorSearch = new QdrantVectorSearchAdapter(client, embedder.Dimension);

        try
        {
            await vectorSearch.PrepareAsync();
        }
        catch
        {
            return;
        }

        var index = 0;
        foreach (var rawChunk in rawChunks)
        {
            var chunk = new DocumentChunk(
                DocumentChunk.ComputeId("corpus-test:01_politique_onboarding.md", index),
                "corpus-test:01_politique_onboarding.md",
                index,
                rawChunk.TitlePath,
                rawChunk.Content);
            var vector = await embedder.GenerateEmbeddingAsync(rawChunk.Content);
            await vectorSearch.IndexAsync(chunk, vector);
            index++;
        }

        await Task.Delay(500);

        // Phase 2 (requete) + 3 (recherche) + 4 (reranking)
        using var reranker = new OnnxRerankerAdapter(RerankerOnnxPath, RerankerSpmPath);
        var query = "Qui est responsable de créer la fiche d'un nouveau collaborateur ?";
        var queryVector = await embedder.GenerateEmbeddingAsync(query);
        var candidates = await vectorSearch.SearchAsync(queryVector, topK: 5);
        candidates.Should().NotBeEmpty();

        var testCorpusCandidates = candidates.Where(c => c.DocumentSource == "corpus-test:01_politique_onboarding.md").ToList();
        testCorpusCandidates.Should().NotBeEmpty("la recherche doit retrouver au moins un chunk du document qu'on vient d'indexer");

        var rerankedResults = await reranker.RerankAsync(query, testCorpusCandidates);

        rerankedResults.Should().NotBeEmpty();
        rerankedResults[0].Content.Should().ContainAny("RH", "fiche", "collaborateur", "pôle");
    }
}
