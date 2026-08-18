using Agirh.Api.Auth;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agirh.Api.Controllers;

public record InstancierWorkflowRequest(Guid CollaborateurId, WorkflowType Type);

public record WorkflowInstanceResponse(
    Guid Id,
    Guid CollaborateurId,
    Guid TemplateId,
    string TemplateVersion,
    WorkflowType Type,
    WorkflowStatus Statut,
    DateTime DateCreation,
    IReadOnlyList<ChecklistItemResponse> Items);

public record ChecklistItemResponse(Guid Id, string Libelle, ItemEtat Etat, string? Commentaire);

public record CocherItemRequest(ItemEtat Etat, string? Commentaire);

[ApiController]
[Route("api/workflows")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly InstancierWorkflowUseCase _instancier;
    private readonly CocherItemUseCase _cocherItem;
    private readonly CloturerDossierUseCase _cloturerDossier;
    private readonly ArchiverDossierUseCase _archiverDossier;
    private readonly ICurrentUserAccessor _currentUser;

    public WorkflowController(
        InstancierWorkflowUseCase instancier,
        CocherItemUseCase cocherItem,
        CloturerDossierUseCase cloturerDossier,
        ArchiverDossierUseCase archiverDossier,
        ICurrentUserAccessor currentUser)
    {
        _instancier = instancier;
        _cocherItem = cocherItem;
        _cloturerDossier = cloturerDossier;
        _archiverDossier = archiverDossier;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowInstanceResponse>> Instancier(InstancierWorkflowRequest request, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            var instance = await _instancier.ExecuteAsync(actor, request.CollaborateurId, request.Type, DateTime.UtcNow, ct);
            return Ok(VersReponse(instance));
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

    [HttpPost("{id:guid}/items/{itemId:guid}/cocher")]
    public async Task<IActionResult> CocherItem(Guid id, Guid itemId, CocherItemRequest request, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _cocherItem.ExecuteAsync(actor, id, itemId, request.Etat, request.Commentaire, DateTime.UtcNow, ct);
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

    [HttpPost("{id:guid}/cloturer")]
    public async Task<IActionResult> Cloturer(Guid id, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _cloturerDossier.ExecuteAsync(actor, id, DateTime.UtcNow, ct);
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

    [HttpPost("{id:guid}/archiver")]
    public async Task<IActionResult> Archiver(Guid id, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            await _archiverDossier.ExecuteAsync(actor, id, ct);
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

    private static WorkflowInstanceResponse VersReponse(Domain.Entities.WorkflowInstance instance) => new(
        instance.Id, instance.CollaborateurId, instance.TemplateId, instance.TemplateVersion, instance.Type,
        instance.Statut, instance.DateCreation,
        instance.Items.Select(i => new ChecklistItemResponse(i.Id, i.Libelle, i.Etat, i.Commentaire)).ToList());
}
