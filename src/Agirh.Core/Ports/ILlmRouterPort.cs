using System.Threading;
using System.Threading.Tasks;

namespace Agirh.Core.Ports;

public interface ILlmRouterPort
{
    Task<IntentionConversation> ClassifierAsync(string question, CancellationToken ct = default);
}
