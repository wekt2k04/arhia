using System.Threading;
using System.Threading.Tasks;

namespace Arhia.Core.Ports;

public interface IEmbeddingPort
{
    int Dimension { get; }
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
}
