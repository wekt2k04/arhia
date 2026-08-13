using Agirh.Domain.Entities;

namespace Agirh.Domain.Interfaces;

public interface IAgentConversationRepository
{
    Task<AgentConversation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AgentConversation?> GetByIdWithMessagesAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<AgentConversation>> GetRecentByUserIdAsync(Guid userId, int limit, CancellationToken ct = default);
    Task AddAsync(AgentConversation conversation, CancellationToken ct = default);
    Task DeleteAsync(AgentConversation conversation);
}
