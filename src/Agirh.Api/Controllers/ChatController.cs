using System.Text;
using System.Text.Json;
using Agirh.Api.Auth;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agirh.Api.Controllers;

public record DemanderRequest(string Question, Guid? CollaborateurCibleId);

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private static readonly JsonSerializerOptions OptionsJson = new(JsonSerializerDefaults.Web);

    private readonly RepondreConversationUseCase _repondreConversation;
    private readonly ICurrentUserAccessor _currentUser;

    public ChatController(RepondreConversationUseCase repondreConversation, ICurrentUserAccessor currentUser)
    {
        _repondreConversation = repondreConversation;
        _currentUser = currentUser;
    }

    /// <summary>
    /// SSE (docs/STACK_TECHNIQUE.md §1) : un événement "fragment" par morceau de texte reçu du
    /// générateur au fur et à mesure de sa génération, puis exactement un événement "termine"
    /// portant sourcee/sources. Un refus RBAC (AccessDeniedException) se traduit en message
    /// conversationnel plutôt qu'une erreur HTTP au milieu du flux — même choix de design que la
    /// version JSON qu'elle remplace (docs/LOGIQUE_METIER.md §9 : l'agent est conversationnel, pas une
    /// API technique brute).
    /// </summary>
    [HttpGet("demander")]
    public async Task Demander([FromQuery] string question, [FromQuery] Guid? collaborateurCibleId, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            await foreach (var evenement in _repondreConversation.ExecuterEnStreamingAsync(actor, question, collaborateurCibleId, ct))
                await EcrireEvenementAsync(evenement, ct);
        }
        catch (AccessDeniedException)
        {
            await EcrireEvenementAsync(new FragmentTexte("Vous n'avez pas accès à ce dossier. Contactez le RH de votre pôle si besoin."), ct);
            await EcrireEvenementAsync(new ReponseTerminee(Sourcee: false, Array.Empty<string>()), ct);
        }
        catch (ArgumentException ex)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync(ex.Message, ct);
        }
        catch (OperationCanceledException)
        {
            // Client déconnecté (fermeture d'onglet, navigation) — fin normale du flux SSE.
        }
    }

    private async Task EcrireEvenementAsync(EvenementConversation evenement, CancellationToken ct)
    {
        var (type, donnees) = evenement switch
        {
            FragmentTexte f => ("fragment", (object)new { texte = f.Texte }),
            ReponseTerminee r => ("termine", new { sourcee = r.Sourcee, sources = r.DocumentsSources }),
            _ => throw new InvalidOperationException($"Type d'événement conversation non géré : {evenement.GetType().Name}")
        };

        var json = JsonSerializer.Serialize(donnees, OptionsJson);
        var frame = Encoding.UTF8.GetBytes($"event: {type}\ndata: {json}\n\n");

        await Response.Body.WriteAsync(frame, ct);
        await Response.Body.FlushAsync(ct);
    }
}
