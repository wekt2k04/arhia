using System.Threading;
using System.Threading.Tasks;

namespace Agirh.Core.Ports;

public interface IVectorSearchPort
{
    Task PrepareAsync(CancellationToken ct = default);
    Task IndexAsync(DocumentChunk chunk, float[] vector, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(float[] queryVector, int topK, CancellationToken ct = default);
}
