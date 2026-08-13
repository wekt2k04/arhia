using Microsoft.EntityFrameworkCore;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Agirh.Infrastructure.Data;

namespace Agirh.Infrastructure.Repositories;

public class AgentConversationRepository : IAgentConversationRepository
{
    private readonly AppDbContext _context;
    public AgentConversationRepository(AppDbContext context) => _context = context;

    public async Task<AgentConversation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.AgentConversations.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<AgentConversation?> GetByIdWithMessagesAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await _context.AgentConversations.AsNoTracking()
            .Include(c => c.Messages.OrderBy(m => m.Timestamp))
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

    public async Task<IEnumerable<AgentConversation>> GetRecentByUserIdAsync(Guid userId, int limit, CancellationToken ct = default) =>
        await _context.AgentConversations.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .Take(limit)
            .Select(c => new AgentConversation
            {
                Id = c.Id,
                UserId = c.UserId,
                Title = c.Title,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
            })
            .ToListAsync(ct);

    public async Task AddAsync(AgentConversation conversation, CancellationToken ct = default)
    {
        conversation.Id = Guid.NewGuid();
        await _context.AgentConversations.AddAsync(conversation, ct);
    }

    public Task DeleteAsync(AgentConversation conversation)
    {
        _context.AgentConversations.Remove(conversation);
        return Task.CompletedTask;
    }
}
