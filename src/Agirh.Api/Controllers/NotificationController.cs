using Agirh.Api.Auth;
using Agirh.Core.UseCases;
using Agirh.Infrastructure.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agirh.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly ObtenirNotificationsUseCase _obtenirNotifications;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly SseNotificationBroadcaster _broadcaster;

    public NotificationController(
        ObtenirNotificationsUseCase obtenirNotifications,
        ICurrentUserAccessor currentUser,
        SseNotificationBroadcaster broadcaster)
    {
        _obtenirNotifications = obtenirNotifications;
        _currentUser = currentUser;
        _broadcaster = broadcaster;
    }

    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            await _broadcaster.DiffuserAsync(
                Response.Body,
                innerCt => _obtenirNotifications.ExecuteAsync(actor, DateTime.UtcNow, innerCt),
                ct);
        }
        catch (OperationCanceledException)
        {
            // Client déconnecté (fermeture d'onglet, navigation) — fin normale du flux SSE.
        }
    }
}
