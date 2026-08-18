using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

/// <summary>
/// Ingestion du corpus documentaire dans le pipeline RAG : chunking -> embedding -> indexation
/// Qdrant (docs/STACK_TECHNIQUE.md #4, phases 1-3). Reservee a Admin/Qualite : reindexer le corpus
/// modifie ce que l'agent conversationnel considere comme source de verite documentaire.
/// </summary>
public sealed class IngestCorpusUseCase
{
    private readonly IDocumentChunkerPort _chunker;
    private readonly IEmbeddingPort _embedding;
    private readonly IVectorSearchPort _vectorSearch;

    public IngestCorpusUseCase(IDocumentChunkerPort chunker, IEmbeddingPort embedding, IVectorSearchPort vectorSearch)
    {
        _chunker = chunker;
        _embedding = embedding;
        _vectorSearch = vectorSearch;
    }

    public async Task<int> ExecuteAsync(
        UserAccount actor,
        IReadOnlyDictionary<string, string> documents,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.CorpusIngest))
            throw new AccessDeniedException("Seul un compte Admin/Qualité peut réindexer le corpus documentaire.");

        await _vectorSearch.PrepareAsync(ct);

        var totalChunks = 0;
        foreach (var (documentName, markdown) in documents)
        {
            var chunks = _chunker.Chunk(documentName, markdown);
            foreach (var chunk in chunks)
            {
                var vector = await _embedding.GenerateEmbeddingAsync(chunk.Content, ct);
                await _vectorSearch.IndexAsync(chunk, vector, ct);
                totalChunks++;
            }
        }

        return totalChunks;
    }
}
