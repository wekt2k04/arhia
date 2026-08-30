using Arhia.Core.Ports;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Arhia.Infrastructure.Rag;

public sealed class QdrantVectorSearchAdapter : IVectorSearchPort
{
    private const string CollectionName = "agirh-corpus";

    private readonly QdrantClient _client;
    private readonly int _dimension;

    public QdrantVectorSearchAdapter(QdrantClient client, int dimension)
    {
        _client = client;
        _dimension = dimension;
    }

    public async Task PrepareAsync(CancellationToken ct = default)
    {
        var exists = await _client.CollectionExistsAsync(CollectionName, ct);
        if (!exists)
        {
            await _client.CreateCollectionAsync(
                CollectionName,
                new VectorParams { Size = (ulong)_dimension, Distance = Distance.Cosine },
                cancellationToken: ct);
        }
    }

    public async Task IndexAsync(DocumentChunk chunk, float[] vector, CancellationToken ct = default)
    {
        var point = new PointStruct
        {
            Id = chunk.Id,
            Vectors = vector
        };
        point.Payload.Add("documentSource", chunk.DocumentSource);
        point.Payload.Add("chunkIndex", (long)chunk.ChunkIndex);
        point.Payload.Add("titlePath", chunk.TitlePath);
        point.Payload.Add("content", chunk.Content);

        await _client.UpsertAsync(CollectionName, new[] { point }, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(float[] queryVector, int topK, CancellationToken ct = default)
    {
        var results = await _client.QueryAsync(
            CollectionName,
            query: queryVector,
            limit: (ulong)topK,
            payloadSelector: true,
            cancellationToken: ct);

        return results.Select(r => new DocumentChunk(
            Id: Guid.Parse(r.Id.Uuid),
            DocumentSource: r.Payload["documentSource"].StringValue,
            ChunkIndex: (int)r.Payload["chunkIndex"].IntegerValue,
            TitlePath: r.Payload["titlePath"].StringValue,
            Content: r.Payload["content"].StringValue,
            Score: r.Score
        )).ToList();
    }
}
