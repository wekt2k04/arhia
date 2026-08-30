using System.Threading;
using System.Threading.Tasks;

namespace Arhia.Core.Ports;

public interface ILlmGeneratorPort
{
    Task<string> GenerateResponseAsync(string systemPrompt, string question, CancellationToken ct = default);

    IAsyncEnumerable<string> GenerateResponseStreamingAsync(string systemPrompt, string question, CancellationToken ct = default);
}
