using Agirh.Api.Auth;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agirh.Api.Controllers;

public record TemplateItemRequest(string Libelle, int Ordre, IReadOnlyCollection<ContractType>? ConditionsTypeContrat);

public record TemplateSectionRequest(string Nom, int Ordre, IReadOnlyCollection<TemplateItemRequest> Items);

public record ProposerTemplateRequest(WorkflowType Type, string Version, IReadOnlyCollection<TemplateSectionRequest> Sections);

public record RejeterTemplateRequest(string Motif);

public record TemplateResponse(Guid Id, WorkflowType Type, string Version, TemplateStatut Statut);

[ApiController]
[Route("api/templates")]
[Authorize]
public class TemplateController : ControllerBase
{
    private readonly ProposerTemplateUseCase _proposer;
    private readonly VerifierTemplateUseCase _verifier;
    private readonly ApprouverTemplateUseCase _approuver;
    private readonly RejeterTemplateUseCase _rejeter;
    private readonly ICurrentUserAccessor _currentUser;

    public TemplateController(
        ProposerTemplateUseCase proposer,
        VerifierTemplateUseCase verifier,
        ApprouverTemplateUseCase approuver,
        RejeterTemplateUseCase rejeter,
        ICurrentUserAccessor currentUser)
    {
        _proposer = proposer;
        _verifier = verifier;
        _approuver = approuver;
        _rejeter = rejeter;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<TemplateResponse>> Proposer(ProposerTemplateRequest request, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            var sections = request.Sections.Select(s => new TemplateSection(
                Guid.NewGuid(),
                s.Nom,
                s.Ordre,
                s.Items.Select(i => new TemplateItem(Guid.NewGuid(), i.Libelle, i.Ordre, i.ConditionsTypeContrat)).ToList()
            )).ToList();

            var template = await _proposer.ExecuteAsync(actor, request.Type, request.Version, sections, DateTime.UtcNow, ct);
            return Ok(new TemplateResponse(template.Id, template.Type, template.Version, template.Statut));
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

    [HttpPost("{id:guid}/verifier")]
    public async Task<IActionResult> Verifier(Guid id, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _verifier.ExecuteAsync(actor, id, ct);
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

    [HttpPost("{id:guid}/approuver")]
    public async Task<IActionResult> Approuver(Guid id, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _approuver.ExecuteAsync(actor, id, ct);
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

    [HttpPost("{id:guid}/rejeter")]
    public async Task<IActionResult> Rejeter(Guid id, RejeterTemplateRequest request, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _rejeter.ExecuteAsync(actor, id, request.Motif, ct);
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
