using Arhia.Api.Auth;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Arhia.Domain;
using Arhia.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arhia.Api.Controllers;

public record TemplateItemRequest(string Label, int Order, IReadOnlyCollection<ContractType>? ApplicableContractTypes);

public record TemplateSectionRequest(string Name, int Order, IReadOnlyCollection<TemplateItemRequest> Items);

public record ProposeTemplateRequest(WorkflowType Type, string Version, IReadOnlyCollection<TemplateSectionRequest> Sections);

public record RejectTemplateRequest(string Reason);

public record TemplateResponse(Guid Id, WorkflowType Type, string Version, TemplateStatus Status);

[ApiController]
[Route("api/templates")]
[Authorize]
public class TemplateController : ControllerBase
{
    private readonly ProposeTemplateUseCase _propose;
    private readonly VerifyTemplateUseCase _verify;
    private readonly ApproveTemplateUseCase _approve;
    private readonly RejectTemplateUseCase _reject;
    private readonly ICurrentUserAccessor _currentUser;

    public TemplateController(
        ProposeTemplateUseCase propose,
        VerifyTemplateUseCase verify,
        ApproveTemplateUseCase approve,
        RejectTemplateUseCase reject,
        ICurrentUserAccessor currentUser)
    {
        _propose = propose;
        _verify = verify;
        _approve = approve;
        _reject = reject;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<TemplateResponse>> Propose(ProposeTemplateRequest request, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            var sections = request.Sections.Select(s => new TemplateSection(
                Guid.NewGuid(),
                s.Name,
                s.Order,
                s.Items.Select(i => new TemplateItem(Guid.NewGuid(), i.Label, i.Order, i.ApplicableContractTypes)).ToList()
            )).ToList();

            var template = await _propose.ExecuteAsync(actor, request.Type, request.Version, sections, DateTime.UtcNow, ct);
            return Ok(new TemplateResponse(template.Id, template.Type, template.Version, template.Status));
        }
        catch (AccessDeniedException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _verify.ExecuteAsync(actor, id, ct);
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

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _approve.ExecuteAsync(actor, id, ct);
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

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, RejectTemplateRequest request, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _reject.ExecuteAsync(actor, id, request.Reason, ct);
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
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
