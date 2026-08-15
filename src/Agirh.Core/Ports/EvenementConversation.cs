namespace Agirh.Core.Ports;

/// <summary>
/// Unifie les branches streamées (générateur, token par token) et non-streamées (statut de
/// dossier, hors-périmètre — déjà des réponses complètes) sous un seul contrat consommé par le
/// contrôleur SSE : un flux de FragmentTexte suivi d'exactement un ReponseTerminee.
/// </summary>
public abstract record EvenementConversation;

public sealed record FragmentTexte(string Texte) : EvenementConversation;

public sealed record ReponseTerminee(bool Sourcee, IReadOnlyList<string> DocumentsSources) : EvenementConversation;
