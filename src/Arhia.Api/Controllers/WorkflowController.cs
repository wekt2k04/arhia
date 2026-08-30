using Arhia.Api.Auth;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Arhia.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arhia.Api.Controllers;

public record InstantiateWorkflowRequest(Guid EmployeeId, WorkflowType Type);

public record WorkflowInstanceResponse(
    Guid Id,
    Guid EmployeeId,
    Guid TemplateId,
    string TemplateVersion,
    WorkflowType Type,
    WorkflowStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<ChecklistItemResponse> Items,
    DateTime? ClosureDate = null);

public record ChecklistItemResponse(
    Guid Id, string Label, ItemStatus Status, string? Comment,
    Guid? CheckedBy = null, DateTime? CheckedDate = null);

public record ChecklistSectionResponse(string Name, int Order, IReadOnlyList<ChecklistItemResponse> Items);

public record WorkflowInstanceDetailResponse(
    Guid Id, Guid EmployeeId, string EmployeeFullName, string EmployeeNumber, Guid DepartmentId,
    Guid TemplateId, string TemplateVersion, WorkflowType Type, WorkflowStatus Status,
    DateTime CreatedAt, DateTime? ClosureDate,
    IReadOnlyList<ChecklistSectionResponse> Sections);

public record WorkflowInstanceListItemResponse(
    Guid Id, Guid EmployeeId, string EmployeeFullName, string EmployeeNumber, Guid DepartmentId,
    WorkflowType Type, WorkflowStatus Status, DateTime CreatedAt, DateTime? ClosureDate,
    int TotalItems, int DoneItems, int FailedItems, int PendingItems);

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
    private readonly GetWorkflowInstanceUseCase _getInstance;
    private readonly ListWorkflowInstancesUseCase _listInstances;
    private readonly ICurrentUserAccessor _currentUser;

    public WorkflowController(
        InstantiateWorkflowUseCase instantiate,
        CheckItemUseCase checkItem,
        CloseCaseUseCase closeCase,
        ArchiveCaseUseCase archiveCase,
        GetWorkflowInstanceUseCase getInstance,
        ListWorkflowInstancesUseCase listInstances,
        ICurrentUserAccessor currentUser)
    {
        _instantiate = instantiate;
        _checkItem = checkItem;
        _closeCase = closeCase;
        _archiveCase = archiveCase;
        _getInstance = getInstance;
        _listInstances = listInstances;
        _currentUser = currentUser;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowInstanceDetailResponse>> GetById(Guid id, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);
        try
        {
            var detail = await _getInstance.ExecuteAsync(actor, id, ct);
            return Ok(ToDetailResponse(detail));
        }
        catch (AccessDeniedException) { return Forbid(); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowInstanceListItemResponse>>> List([FromQuery] Guid? employeeId, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);
        try
        {
            var items = await _listInstances.ExecuteAsync(actor, employeeId, ct);
            return Ok(items.Select(ToListItemResponse).ToList());
        }
        catch (AccessDeniedException) { return Forbid(); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
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

    private static WorkflowInstanceDetailResponse ToDetailResponse(WorkflowInstanceDetail detail)
    {
        var instance = detail.Instance;
        var byTemplateItemId = instance.Items.ToDictionary(i => i.TemplateItemId);

        IReadOnlyList<ChecklistSectionResponse> sections = detail.Template is not null
            ? detail.Template.Sections
                .OrderBy(s => s.Order)
                .Select(s => new ChecklistSectionResponse(
                    s.Name, s.Order,
                    s.Items.OrderBy(ti => ti.Order)
                        .Where(ti => byTemplateItemId.ContainsKey(ti.Id))
                        .Select(ti => byTemplateItemId[ti.Id])
                        .Select(st => new ChecklistItemResponse(st.Id, st.Label, st.Status, st.Comment, st.CheckedBy, st.CheckedDate))
                        .ToList()))
                .Where(s => s.Items.Count > 0)
                .ToList()
            : new[] { new ChecklistSectionResponse("Items", 0, instance.Items
                .Select(i => new ChecklistItemResponse(i.Id, i.Label, i.Status, i.Comment, i.CheckedBy, i.CheckedDate))
                .ToList()) };

        return new WorkflowInstanceDetailResponse(
            instance.Id, instance.EmployeeId, $"{detail.Employee.FirstName} {detail.Employee.LastName}",
            detail.Employee.EmployeeNumber.Value, detail.Employee.DepartmentId,
            instance.TemplateId, instance.TemplateVersion, instance.Type, instance.Status,
            instance.CreatedAt, instance.ClosureDate, sections);
    }

    private static WorkflowInstanceListItemResponse ToListItemResponse(WorkflowInstanceWithEmployee item)
    {
        var instance = item.Instance;
        return new WorkflowInstanceListItemResponse(
            instance.Id, instance.EmployeeId, $"{item.Employee.FirstName} {item.Employee.LastName}",
            item.Employee.EmployeeNumber.Value, item.Employee.DepartmentId,
            instance.Type, instance.Status, instance.CreatedAt, instance.ClosureDate,
            instance.Items.Count,
            instance.Items.Count(i => i.Status == ItemStatus.Done),
            instance.Items.Count(i => i.Status == ItemStatus.Failed),
            instance.Items.Count(i => i.Status == ItemStatus.Pending));
    }
}
