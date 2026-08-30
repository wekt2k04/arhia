using System.Threading;
using System.Threading.Tasks;

namespace Arhia.Core.Ports;

public interface ILlmRouterPort
{
    Task<ConversationIntent> ClassifyAsync(string question, CancellationToken ct = default);
}
