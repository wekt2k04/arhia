using System.Threading;
using System.Threading.Tasks;

namespace Agirh.Core.Ports;

public interface IEmbeddingPort
{
    int Dimension { get; }
    Task<float[]> GenererEmbeddingAsync(string texte, CancellationToken ct = default);
}
