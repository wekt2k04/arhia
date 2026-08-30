namespace Arhia.Core.Ports;

/// <summary>
/// Unifie les branches streamées (générateur, token par token) et non-streamées (statut de
/// dossier, hors-périmètre — déjà des réponses complètes) sous un seul contrat consommé par le
/// contrôleur SSE : un flux de TextFragment suivi d'exactement un ResponseCompleted.
/// </summary>
public abstract record ConversationEvent;

public sealed record TextFragment(string Text) : ConversationEvent;

public sealed record ResponseCompleted(bool Sourced, IReadOnlyList<string> Sources) : ConversationEvent;
