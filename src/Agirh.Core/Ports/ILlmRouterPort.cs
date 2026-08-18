using System.Threading;
using System.Threading.Tasks;

namespace Agirh.Core.Ports;

public interface ILlmRouterPort
{
    Task<ConversationIntent> ClassifyAsync(string question, CancellationToken ct = default);
}
