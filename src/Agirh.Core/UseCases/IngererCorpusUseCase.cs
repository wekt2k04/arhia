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
public sealed class IngererCorpusUseCase
{
    private readonly IDocumentChunkerPort _chunker;
    private readonly IEmbeddingPort _embedding;
    private readonly IVectorSearchPort _rechercheVectorielle;

    public IngererCorpusUseCase(IDocumentChunkerPort chunker, IEmbeddingPort embedding, IVectorSearchPort rechercheVectorielle)
    {
        _chunker = chunker;
        _embedding = embedding;
        _rechercheVectorielle = rechercheVectorielle;
    }

    public async Task<int> ExecuterAsync(
        CompteUtilisateur acteur,
        IReadOnlyDictionary<string, string> documents,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.EstAutorise(acteur.Role, ResourceAction.CorpusIngerer))
            throw new AccesRefuseException("Seul un compte Admin/Qualité peut réindexer le corpus documentaire.");

        await _rechercheVectorielle.PreparerAsync(ct);

        var totalChunks = 0;
        foreach (var (nomDocument, markdown) in documents)
        {
            var chunks = _chunker.Decouper(nomDocument, markdown);
            foreach (var chunk in chunks)
            {
                var vecteur = await _embedding.GenererEmbeddingAsync(chunk.Contenu, ct);
                await _rechercheVectorielle.IndexerAsync(chunk, vecteur, ct);
                totalChunks++;
            }
        }

        return totalChunks;
    }
}
