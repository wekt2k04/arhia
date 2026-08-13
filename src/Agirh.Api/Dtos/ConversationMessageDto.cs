namespace Agirh.Api.Dtos;

public class ConversationMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ToolCalled { get; set; }
    public DateTime Timestamp { get; set; }
}
