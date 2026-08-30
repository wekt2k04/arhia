using System.Text;
using System.Text.Json;
using Arhia.Api.Auth;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arhia.Api.Controllers;

public record AskRequest(string Question, Guid? TargetEmployeeId);

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private static readonly JsonSerializerOptions OptionsJson = new(JsonSerializerDefaults.Web);

    private readonly AnswerConversationUseCase _answerConversation;
    private readonly ICurrentUserAccessor _currentUser;

    public ChatController(AnswerConversationUseCase answerConversation, ICurrentUserAccessor currentUser)
    {
        _answerConversation = answerConversation;
        _currentUser = currentUser;
    }

    /// <summary>
    /// SSE (docs/STACK_TECHNIQUE.md §1) : un événement "fragment" par morceau de texte reçu du
    /// générateur au fur et à mesure de sa génération, puis exactement un événement "done"
    /// portant sourced/sources. Un refus RBAC (AccessDeniedException) se traduit en message
    /// conversationnel plutôt qu'une erreur HTTP au milieu du flux — même choix de design que la
    /// version JSON qu'elle remplace (docs/LOGIQUE_METIER.md §9 : l'agent est conversationnel, pas une
    /// API technique brute).
    /// </summary>
    [HttpGet("ask")]
    public async Task Ask([FromQuery] string question, [FromQuery] Guid? targetEmployeeId, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            await foreach (var conversationEvent in _answerConversation.ExecuteStreamingAsync(actor, question, targetEmployeeId, ct))
                await WriteEventAsync(conversationEvent, ct);
        }
        catch (AccessDeniedException)
        {
            await WriteEventAsync(new TextFragment("Vous n'avez pas accès à ce dossier. Contactez le RH de votre pôle si besoin."), ct);
            await WriteEventAsync(new ResponseCompleted(Sourced: false, Array.Empty<string>()), ct);
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

    private async Task WriteEventAsync(ConversationEvent conversationEvent, CancellationToken ct)
    {
        var (type, data) = conversationEvent switch
        {
            TextFragment f => ("fragment", (object)new { text = f.Text }),
            ResponseCompleted r => ("done", new { sourced = r.Sourced, sources = r.Sources }),
            _ => throw new InvalidOperationException($"Type d'événement conversation non géré : {conversationEvent.GetType().Name}")
        };

        var json = JsonSerializer.Serialize(data, OptionsJson);
        var frame = Encoding.UTF8.GetBytes($"event: {type}\ndata: {json}\n\n");

        await Response.Body.WriteAsync(frame, ct);
        await Response.Body.FlushAsync(ct);
    }
}
