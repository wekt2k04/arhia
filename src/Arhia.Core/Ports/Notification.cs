namespace Arhia.Core.Ports;

public enum NotificationType
{
    ItemPendingTooLong,
    UpcomingDeparture,
    TemplatePendingValidation
}

public sealed record Notification(NotificationType Type, string Message, DateTime ReferenceDate, Guid? ReferenceId);
