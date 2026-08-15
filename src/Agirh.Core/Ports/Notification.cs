namespace Agirh.Core.Ports;

public enum TypeNotification
{
    ItemEnAttenteDepuisLongtemps,
    EcheanceDepartApprochante,
    TemplateEnAttenteValidation
}

public sealed record Notification(TypeNotification Type, string Message, DateTime DateReference, Guid? ReferenceId);
