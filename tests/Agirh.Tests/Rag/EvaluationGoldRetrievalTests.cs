using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Rag;
using FluentAssertions;
using Qdrant.Client;
using Xunit;

namespace Agirh.Tests.Rag;

/// <summary>
/// Fixture partagée (une seule fois pour toute la classe, pas par question) : ingère le vrai
/// rag/corpus/*.md dans Qdrant sous ses vrais noms de documents avant de mesurer la précision de
/// récupération sur le jeu de Q/R gold (rag/eval/gold_qa.json, milestone 9). Se termine sans
/// initialiser <see cref="PrerequisDisponibles"/> si modèles ONNX ou Qdrant sont absents.
/// </summary>
public sealed class GoldCorpusFixture : IAsyncLifetime
{
    public bool PrerequisDisponibles { get; private set; }
    public OnnxEmbeddingAdapter? Embedder { get; private set; }
    public OnnxRerankerAdapter? Reranker { get; private set; }
    public QdrantVectorSearchAdapter? VectorSearch { get; private set; }
    public IReadOnlySet<string> NomsDocumentsReels { get; private set; } = new HashSet<string>();

    public async Task InitializeAsync()
    {
        var embeddingOnnx = Path.Combine(RepoPaths.ModelesEmbedding, "model_quantized.onnx");
        var embeddingSpm = Path.Combine(RepoPaths.ModelesEmbedding, "sentencepiece.bpe.model");
        var rerankerOnnx = Path.Combine(RepoPaths.ModelesReranker, "model_quantized.onnx");
        var rerankerSpm = Path.Combine(RepoPaths.ModelesReranker, "sentencepiece.bpe.model");
        var corpusDir = Path.Combine(RepoPaths.Racine, "rag", "corpus");

        if (!File.Exists(embeddingOnnx) || !File.Exists(embeddingSpm)) return;
        if (!File.Exists(rerankerOnnx) || !File.Exists(rerankerSpm)) return;
        if (!Directory.Exists(corpusDir)) return;

        Embedder = new OnnxEmbeddingAdapter(embeddingOnnx, embeddingSpm);
        Reranker = new OnnxRerankerAdapter(rerankerOnnx, rerankerSpm);

        var client = new QdrantClient("localhost");
        var vectorSearch = new QdrantVectorSearchAdapter(client, Embedder.Dimension);
        try
        {
            await vectorSearch.PrepareAsync();
        }
        catch
        {
            return;
        }

        VectorSearch = vectorSearch;

        var tokenizer = XlmRobertaTokenizer.LoadFromFile(embeddingSpm);
        var chunker = new MarkdownChunkerAdapter(tokenizer);
        var ingestion = new IngestCorpusUseCase(chunker, Embedder, vectorSearch);

        var documents = new Dictionary<string, string>();
        foreach (var fichier in Directory.GetFiles(corpusDir, "*.md"))
            documents[Path.GetFileName(fichier)] = await File.ReadAllTextAsync(fichier);
        NomsDocumentsReels = documents.Keys.ToHashSet();

        var actor = new UserAccount(Guid.NewGuid(), "eval-admin@agirh.test", "hash", RoleType.QualityAdmin, null, DateTime.UtcNow);
        await ingestion.ExecuteAsync(actor, documents);
        await Task.Delay(500);

        PrerequisDisponibles = true;
    }

    public Task DisposeAsync()
    {
        Embedder?.Dispose();
        Reranker?.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mesure de retrieval (docs/STACK_TECHNIQUE.md #4, milestone 9) : pour chaque question
/// "documentaire" du jeu de Q/R gold, le document en tête après reranking doit être l'une des
/// sources acceptées. Rapide et déterministe (pas d'appel LLM) — fait partie de la suite par
/// défaut. La fidélité de la réponse générée (avec LLM) est mesurée séparément par
/// <see cref="EvaluationGoldEndToEndTests"/>, exclue de la suite par défaut.
/// </summary>
public class EvaluationGoldRetrievalTests : IClassFixture<GoldCorpusFixture>
{
    private readonly GoldCorpusFixture _fixture;

    public EvaluationGoldRetrievalTests(GoldCorpusFixture fixture)
    {
        _fixture = fixture;
    }

    public static IEnumerable<object[]> QuestionsDocumentaires() => GoldQa.ParCategorie("documentaire");

    [Theory]
    [MemberData(nameof(QuestionsDocumentaires))]
    public async Task Retrieval_QuestionDocumentaire_LeTopResultApresRerankingEstUneSourceAcceptee(GoldQuestion gold)
    {
        if (!_fixture.PrerequisDisponibles) return;

        var queryVector = await _fixture.Embedder!.GenerateEmbeddingAsync(gold.Question);
        // topK large + filtre sur les vrais noms de documents : Qdrant est persistant (volume nommé)
        // et peut contenir des fixtures d'autres tests (ex. PipelineCompletCorpusReelTests) au contenu
        // parfois identique à un vrai document sous un autre nom — même pattern de fix que
        // RagPipelineIntegrationTests (cf. .claude/HANDOFF/NEXT_SESSION.md).
        var rawCandidates = await _fixture.VectorSearch!.SearchAsync(queryVector, topK: 20);
        var candidates = rawCandidates.Where(c => _fixture.NomsDocumentsReels.Contains(c.DocumentSource)).ToList();
        candidates.Should().NotBeEmpty($"la question gold {gold.Id} doit retrouver au moins un candidat du vrai corpus indexé");

        var results = await _fixture.Reranker!.RerankAsync(gold.Question, candidates);
        results.Should().NotBeEmpty();

        results[0].DocumentSource.Should().BeOneOf(gold.SourcesAcceptees,
            $"{gold.Id} ({gold.Question}) devrait être sourcée depuis {string.Join(" ou ", gold.SourcesAcceptees)}, pas {results[0].DocumentSource}");
    }
}
