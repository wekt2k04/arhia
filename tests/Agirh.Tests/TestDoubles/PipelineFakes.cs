using System.Runtime.CompilerServices;
using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Microsoft.Extensions.AI;

namespace Agirh.Tests.TestDoubles;

/// <summary>
/// Manual test doubles for the cognitive pipeline.
///
/// Choice documented: hand-written fakes are preferred over Moq because the
/// current suite uses zero mocks and these seams are owned by our own code
/// (ICognitiveProfiler, IZeroTrustDispatcher, IWorkerExecutor,
/// ISynthesizerAgent, ICheckerAgent). Each fake is deterministic and records
/// its calls so assertions stay exact.
/// </summary>
public sealed class FakeProfiler : ICognitiveProfiler
{
    private readonly DynamicContextVector _vector;

    public FakeProfiler(DynamicContextVector vector) => _vector = vector;

    public int CallCount { get; private set; }

    public CognitiveExtractionInput? LastInput { get; private set; }

    public Task<DynamicContextVector> ExtractAsync(CognitiveExtractionInput input, CancellationToken ct = default)
    {
        CallCount++;
        LastInput = input;
        return Task.FromResult(_vector);
    }
}

public sealed class FakeDispatcher : IZeroTrustDispatcher
{
    private readonly DispatchResult _result;

    public FakeDispatcher(DispatchResult result) => _result = result;

    public int CallCount { get; private set; }

    public DispatchInput? LastInput { get; private set; }

    public Task<DispatchResult> DispatchAsync(DispatchInput input, CancellationToken ct = default)
    {
        CallCount++;
        LastInput = input;
        return Task.FromResult(_result);
    }
}

public sealed class FakeWorker : IWorkerExecutor
{
    private readonly IReadOnlyList<string> _chunks;

    public FakeWorker(params string[] chunks) => _chunks = chunks;

    public int CallCount { get; private set; }

    public async IAsyncEnumerable<string> ExecuteAsync(
        ExecutionInput input,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        CallCount++;
        await Task.CompletedTask;
        foreach (var chunk in _chunks)
        {
            ct.ThrowIfCancellationRequested();
            yield return chunk;
        }
    }
}

public sealed class FakeSynthesizer : ISynthesizerAgent
{
    private readonly string _draft;

    public FakeSynthesizer(string draft) => _draft = draft;

    public int CallCount { get; private set; }

    public List<string?> Feedbacks { get; } = [];

    /// <summary>RawWorkerData vu par le synthétiseur au dernier appel (M8 : BoundWords 500).</summary>
    public string? LastRawWorkerData { get; private set; }

    public Task<string> DraftResponseAsync(AgentPipelineContext context, string? feedback = null, CancellationToken ct = default)
    {
        CallCount++;
        Feedbacks.Add(feedback);
        LastRawWorkerData = context.RawWorkerData;
        return Task.FromResult(_draft);
    }
}

public sealed class FakeChecker : ICheckerAgent
{
    private readonly EvaluationResult _result;

    public FakeChecker(EvaluationResult result) => _result = result;

    public int CallCount { get; private set; }

    public List<string> EvaluatedDrafts { get; } = [];

    public Task<EvaluationResult> EvaluateAsync(AgentPipelineContext context, string draftResponse, CancellationToken ct = default)
    {
        CallCount++;
        EvaluatedDrafts.Add(draftResponse);
        return Task.FromResult(_result);
    }
}

/// <summary>Générateur d'embeddings factice : retourne un vecteur constant de la dimension demandée.</summary>
public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly int _dimension;

    public FakeEmbeddingGenerator(int dimension) => _dimension = dimension;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = values.ToList();
        var embeddings = list.Select(_ =>
            new Embedding<float>(Enumerable.Repeat(0.1f, _dimension).ToArray())).ToList();
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

/// <summary>Générateur d'embeddings factice qui lève toujours une exception.</summary>
public sealed class FailingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Embedding service unavailable (test stub)");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

/// <summary>Repository RAG factice : retourne la liste de documents configurée à la construction.</summary>
public sealed class FakeKnowledgeDocumentRepository : IKnowledgeDocumentRepository
{
    private readonly IReadOnlyList<KnowledgeDocument> _docs;

    public FakeKnowledgeDocumentRepository(IEnumerable<KnowledgeDocument> docs)
        => _docs = docs.ToList().AsReadOnly();

    public Task<KnowledgeDocument?> GetByIdAsync(Guid id)
        => Task.FromResult(_docs.FirstOrDefault(d => d.Id == id));

    public Task<IEnumerable<KnowledgeDocument>> SearchBySimilarityAsync(
        byte[] queryEmbedding, int topN = 5, CancellationToken ct = default)
        => Task.FromResult(_docs.Take(topN) as IEnumerable<KnowledgeDocument>);

    public Task AddRangeAsync(IEnumerable<KnowledgeDocument> documents) => Task.CompletedTask;

    public Task DeleteAllAsync() => Task.CompletedTask;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
