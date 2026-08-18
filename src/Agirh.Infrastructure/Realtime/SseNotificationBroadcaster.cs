using System.Text;
using System.Text.Json;
using Agirh.Core.Ports;

namespace Agirh.Infrastructure.Realtime;

/// <summary>
/// Diffuse la liste courante des notifications (GetNotificationsUseCase) via SSE,
/// recalculée à intervalle fixe côté serveur (docs/STACK_TECHNIQUE.md §1 : SSE, seul mécanisme de
/// transport temps réel). Pas de bus d'événements : plus simple et suffisant pour un prototype
/// où les notifications sont dérivées de données déjà persistées, jamais leur propre source de
/// vérité.
/// </summary>
public sealed class SseNotificationBroadcaster
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions OptionsJson = new(JsonSerializerDefaults.Web);

    public async Task BroadcastAsync(
        Stream destination,
        Func<CancellationToken, Task<IReadOnlyList<Notification>>> getNotifications,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var notifications = await getNotifications(ct);
            var json = JsonSerializer.Serialize(notifications, OptionsJson);
            var frame = Encoding.UTF8.GetBytes($"data: {json}\n\n");

            await destination.WriteAsync(frame, ct);
            await destination.FlushAsync(ct);

            await Task.Delay(RefreshInterval, ct);
        }
    }
}
