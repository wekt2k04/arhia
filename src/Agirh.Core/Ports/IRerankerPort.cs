using System.Threading;
using System.Threading.Tasks;

namespace Agirh.Core.Ports;

public interface IRerankerPort
{
    Task<IReadOnlyList<DocumentChunk>> RerankAsync(string query, IReadOnlyList<DocumentChunk> candidates, CancellationToken ct = default);
}
