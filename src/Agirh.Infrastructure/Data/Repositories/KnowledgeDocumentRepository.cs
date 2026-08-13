using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Agirh.Core.Settings;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Agirh.Infrastructure.Data;

namespace Agirh.Infrastructure.Repositories;

public class KnowledgeDocumentRepository : IKnowledgeDocumentRepository
{
    // Seuil de pertinence cosine : en dessous, un chunk n'est PAS un résultat.
    // 0.60 élimine les chunks thématiquement éloignés qui dégradaient la précision
    // des réponses RAG quand le seuil était à 0.35 (trop permissif pour 768d).
    // Désormais configurable via AIOptions.RagSimilarityThreshold.
    private readonly AppDbContext _context;
    private readonly IOptions<AIOptions> _options;

    public KnowledgeDocumentRepository(AppDbContext context, IOptions<AIOptions> options)
    {
        _context = context;
        _options = options;
    }

    public async Task<KnowledgeDocument?> GetByIdAsync(Guid id) =>
        await _context.KnowledgeDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);

    public async Task<IEnumerable<KnowledgeDocument>> SearchBySimilarityAsync(byte[] queryEmbedding, int topN = 5, CancellationToken ct = default)
    {
        if (queryEmbedding == null || queryEmbedding.Length == 0)
            return Enumerable.Empty<KnowledgeDocument>();

        var isSqlServer = _context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;

        if (isSqlServer)
        {
            // SQL Server 2025 native VECTOR_DISTANCE.
            // Le type vector(768) n'accepte AUCUNE conversion implicite, ni depuis
            // varbinary, ni depuis nvarchar(max). Deux verrous sont donc posés :
            //  1) le paramètre est bindé en nvarchar BORNÉ (SqlParameter Size) —
            //     jamais nvarchar(max), qui est refusé par VECTOR_DISTANCE ;
            //  2) CAST({1} AS vector(768)) force la conversion explicite du JSON
            //     (produit par AppDbContext.SerializeEmbedding) vers vector(768).
            var vectorLiteral = AppDbContext.SerializeEmbedding(queryEmbedding);
            var vectorParam = new SqlParameter("vectorParam", SqlDbType.NVarChar, 20000)
            {
                Value = vectorLiteral,
            };
            // Lot C — seuil de similarité : on filtre sur la distance cosine
            // (≤ 1 - seuil) dans une table dérivée pour n'exposer QUE des chunks
            // réellement proches de la requête. Fail-closed : zéro résultat si le
            // seuil n'est pas atteint, jamais un document non pertinent.
            var maxDistance = 1.0 - _options.Value.RagSimilarityThreshold;
            var query = _context.KnowledgeDocuments
                .FromSqlRaw(
                    "SELECT TOP({0}) Id, Title, ChunkText, SourceFile, ChunkIndex, CreatedAt, Embedding, CAST('' AS NVARCHAR(MAX)) AS Content FROM (SELECT k.Id, k.Title, k.ChunkText, k.SourceFile, k.ChunkIndex, k.CreatedAt, k.Embedding, VECTOR_DISTANCE('cosine', k.Embedding, CAST({1} AS vector(768))) AS _dist FROM KnowledgeDocuments k) AS ranked WHERE ranked._dist <= {2} ORDER BY ranked._dist",
                    topN, vectorParam, maxDistance)
                .AsNoTracking();
            return await query.ToListAsync(ct);
        }

        // SQLite fallback: in-memory cosine similarity + seuil de pertinence
        // Projection sans Content (nvarchar(max)) : non utilisé par RagFunctions.
        var allDocs = await _context.KnowledgeDocuments
            .AsNoTracking()
            .Select(d => new KnowledgeDocument
            {
                Id = d.Id, Title = d.Title, ChunkText = d.ChunkText,
                SourceFile = d.SourceFile, ChunkIndex = d.ChunkIndex,
                CreatedAt = d.CreatedAt, Embedding = d.Embedding,
                Content = string.Empty,
            })
            .ToListAsync(ct);
        var scored = allDocs
            .Select(d => new
            {
                Document = d,
                Score = CosineSimilarity(queryEmbedding, d.Embedding)
            })
            .Where(x => x.Score.HasValue && x.Score.Value >= _options.Value.RagSimilarityThreshold)
            .OrderByDescending(x => x.Score!.Value)
            .Take(topN)
            .Select(x => x.Document)
            .ToList();
        return scored;
    }

    private static double? CosineSimilarity(byte[] a, byte[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return null;
        var floatCount = a.Length / 4;
        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;
        for (int i = 0; i < floatCount; i++)
        {
            var fa = BitConverter.ToSingle(a, i * 4);
            var fb = BitConverter.ToSingle(b, i * 4);
            dotProduct += fa * fb;
            normA += fa * fa;
            normB += fb * fb;
        }
        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? null : dotProduct / denominator;
    }

    public async Task AddRangeAsync(IEnumerable<KnowledgeDocument> documents)
    {
        await _context.KnowledgeDocuments.AddRangeAsync(documents);
    }

    public async Task DeleteAllAsync()
    {
        await _context.KnowledgeDocuments.ExecuteDeleteAsync();
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
