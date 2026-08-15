using System.Threading;
using System.Threading.Tasks;

namespace Agirh.Core.Ports;

public interface IRerankerPort
{
    Task<IReadOnlyList<ChunkDocumentaire>> RerankAsync(string requete, IReadOnlyList<ChunkDocumentaire> candidats, CancellationToken ct = default);
}
