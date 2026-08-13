namespace Agirh.Domain.Entities;

public class AgentConversation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = "Nouvelle discussion";
    public List<AgentMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AgentMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public AgentConversation? Conversation { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ToolCalled { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
