using Agirh.Api.Auth;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agirh.Api.Controllers;

public record DemanderRequest(string Question, Guid? CollaborateurCibleId);
public record DemanderResponse(string Texte, bool Sourcee, IReadOnlyList<string> Sources);

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly RepondreConversationUseCase _repondreConversation;
    private readonly ICurrentUserAccessor _currentUser;

    public ChatController(RepondreConversationUseCase repondreConversation, ICurrentUserAccessor currentUser)
    {
        _repondreConversation = repondreConversation;
        _currentUser = currentUser;
    }

    [HttpPost("demander")]
    public async Task<ActionResult<DemanderResponse>> Demander(DemanderRequest request, CancellationToken ct)
    {
        var acteur = await _currentUser.ObtenirActeurAsync(ct);

        try
        {
            var reponse = await _repondreConversation.ExecuterAsync(acteur, request.Question, request.CollaborateurCibleId, ct);
            return Ok(new DemanderResponse(reponse.Texte, reponse.Sourcee, reponse.DocumentsSources));
        }
        catch (AccesRefuseException)
        {
            // L'agent est conversationnel (LOGIQUE_METIER.md §9) : un refus RBAC se dit dans la
            // reponse plutot que de casser la conversation avec un 403 muet.
            return Ok(new DemanderResponse(
                "Vous n'avez pas accès à ce dossier. Contactez le RH de votre pôle si besoin.",
                Sourcee: false,
                Array.Empty<string>()));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
