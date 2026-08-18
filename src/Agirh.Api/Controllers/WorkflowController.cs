using Agirh.Api.Auth;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agirh.Api.Controllers;

public record InstantiateWorkflowRequest(Guid EmployeeId, WorkflowType Type);

public record WorkflowInstanceResponse(
    Guid Id,
    Guid EmployeeId,
    Guid TemplateId,
    string TemplateVersion,
    WorkflowType Type,
    WorkflowStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<ChecklistItemResponse> Items);

public record ChecklistItemResponse(Guid Id, string Label, ItemStatus Status, string? Comment);

public record CheckItemRequest(ItemStatus Status, string? Comment);

[ApiController]
[Route("api/workflows")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly InstantiateWorkflowUseCase _instantiate;
    private readonly CheckItemUseCase _checkItem;
    private readonly CloseCaseUseCase _closeCase;
    private readonly ArchiveCaseUseCase _archiveCase;
    private readonly ICurrentUserAccessor _currentUser;

    public WorkflowController(
        InstantiateWorkflowUseCase instantiate,
        CheckItemUseCase checkItem,
        CloseCaseUseCase closeCase,
        ArchiveCaseUseCase archiveCase,
        ICurrentUserAccessor currentUser)
    {
        _instantiate = instantiate;
        _checkItem = checkItem;
        _closeCase = closeCase;
        _archiveCase = archiveCase;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowInstanceResponse>> Instantiate(InstantiateWorkflowRequest request, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            var instance = await _instantiate.ExecuteAsync(actor, request.EmployeeId, request.Type, DateTime.UtcNow, ct);
            return Ok(ToResponse(instance));
        }
        catch (AccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id:guid}/items/{itemId:guid}/check")]
    public async Task<IActionResult> CheckItem(Guid id, Guid itemId, CheckItemRequest request, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _checkItem.ExecuteAsync(actor, id, itemId, request.Status, request.Comment, DateTime.UtcNow, ct);
            return NoContent();
        }
        catch (AccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _closeCase.ExecuteAsync(actor, id, DateTime.UtcNow, ct);
            return NoContent();
        }
        catch (AccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _archiveCase.ExecuteAsync(actor, id, ct);
            return NoContent();
        }
        catch (AccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private static WorkflowInstanceResponse ToResponse(Domain.Entities.WorkflowInstance instance) => new(
        instance.Id, instance.EmployeeId, instance.TemplateId, instance.TemplateVersion, instance.Type,
        instance.Status, instance.CreatedAt,
        instance.Items.Select(i => new ChecklistItemResponse(i.Id, i.Label, i.Status, i.Comment)).ToList());
}
