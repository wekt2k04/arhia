using System.Threading;
using System.Threading.Tasks;

namespace Agirh.Core.Ports;

public interface IVectorSearchPort
{
    Task PreparerAsync(CancellationToken ct = default);
    Task IndexerAsync(ChunkDocumentaire chunk, float[] vecteur, CancellationToken ct = default);
    Task<IReadOnlyList<ChunkDocumentaire>> RechercherAsync(float[] vecteurRequete, int topK, CancellationToken ct = default);
}
