using System.Threading;
using System.Threading.Tasks;

namespace Agirh.Core.Ports;

public interface ILlmGeneratorPort
{
    Task<string> GenererReponseAsync(string systemPrompt, string question, CancellationToken ct = default);
}
