using Agirh.Core.Ports;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Agirh.Infrastructure.Rag;

public sealed class QdrantVectorSearchAdapter : IVectorSearchPort
{
    private const string NomCollection = "agirh-corpus";

    private readonly QdrantClient _client;
    private readonly int _dimension;

    public QdrantVectorSearchAdapter(QdrantClient client, int dimension)
    {
        _client = client;
        _dimension = dimension;
    }

    public async Task PreparerAsync(CancellationToken ct = default)
    {
        var existe = await _client.CollectionExistsAsync(NomCollection, ct);
        if (!existe)
        {
            await _client.CreateCollectionAsync(
                NomCollection,
                new VectorParams { Size = (ulong)_dimension, Distance = Distance.Cosine },
                cancellationToken: ct);
        }
    }

    public async Task IndexerAsync(ChunkDocumentaire chunk, float[] vecteur, CancellationToken ct = default)
    {
        var point = new PointStruct
        {
            Id = chunk.Id,
            Vectors = vecteur
        };
        point.Payload.Add("documentSource", chunk.DocumentSource);
        point.Payload.Add("chunkIndex", (long)chunk.ChunkIndex);
        point.Payload.Add("cheminTitres", chunk.CheminTitres);
        point.Payload.Add("contenu", chunk.Contenu);

        await _client.UpsertAsync(NomCollection, new[] { point }, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<ChunkDocumentaire>> RechercherAsync(float[] vecteurRequete, int topK, CancellationToken ct = default)
    {
        var resultats = await _client.QueryAsync(
            NomCollection,
            query: vecteurRequete,
            limit: (ulong)topK,
            payloadSelector: true,
            cancellationToken: ct);

        return resultats.Select(r => new ChunkDocumentaire(
            Id: Guid.Parse(r.Id.Uuid),
            DocumentSource: r.Payload["documentSource"].StringValue,
            ChunkIndex: (int)r.Payload["chunkIndex"].IntegerValue,
            CheminTitres: r.Payload["cheminTitres"].StringValue,
            Contenu: r.Payload["contenu"].StringValue,
            Score: r.Score
        )).ToList();
    }
}
