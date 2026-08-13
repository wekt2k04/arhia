using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Agirh.Api.Dtos;
using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;

namespace Agirh.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IHardStateExtractor _hardStateExtractor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConversationsController> _logger;

    public ConversationsController(
        IHardStateExtractor hardStateExtractor,
        IUnitOfWork unitOfWork,
        ILogger<ConversationsController> logger)
    {
        _hardStateExtractor = hardStateExtractor;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var hardState = _hardStateExtractor.ExtractFromPrincipal(User);
        var limit = HistoryLimit(hardState.Role);

        var conversations = await _unitOfWork.AgentConversations
            .GetRecentByUserIdAsync(Guid.Parse(hardState.UserId), limit, ct);

        return Ok(conversations.Select(c => new ConversationSummaryDto
        {
            Id = c.Id,
            Title = c.Title,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
        }));
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> Messages(Guid id, CancellationToken ct)
    {
        var hardState = _hardStateExtractor.ExtractFromPrincipal(User);
        var conversation = await _unitOfWork.AgentConversations
            .GetByIdWithMessagesAsync(id, Guid.Parse(hardState.UserId), ct);

        if (conversation == null)
            return NotFound();

        return Ok(conversation.Messages.Select(m => new ConversationMessageDto
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            ToolCalled = m.ToolCalled,
            Timestamp = m.Timestamp,
        }));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var hardState = _hardStateExtractor.ExtractFromPrincipal(User);
        var conversation = await _unitOfWork.AgentConversations.GetByIdAsync(id, ct);

        if (conversation == null)
            return NotFound();

        if (conversation.UserId != Guid.Parse(hardState.UserId))
        {
            _logger.LogWarning("CONVERSATION_IDOR — user {UserId} attempted to delete conversation {ConversationId} owned by {OwnerId}",
                hardState.UserId, id, conversation.UserId);
            return Forbid();
        }

        await _unitOfWork.AgentConversations.DeleteAsync(conversation);
        await _unitOfWork.SaveChangesAsync(ct);
        return NoContent();
    }

    private static int HistoryLimit(string role) => role switch
    {
        "Admin" => 7,
        "Manager" => 5,
        _ => 3,
    };
}
