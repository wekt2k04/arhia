using Agirh.Domain.Entities;

namespace Agirh.Domain.Interfaces;

public interface IKnowledgeDocumentRepository
{
    Task<KnowledgeDocument?> GetByIdAsync(Guid id);
    Task<IEnumerable<KnowledgeDocument>> SearchBySimilarityAsync(byte[] queryEmbedding, int topN = 5, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<KnowledgeDocument> documents);
    Task DeleteAllAsync();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
