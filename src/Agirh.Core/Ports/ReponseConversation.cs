namespace Agirh.Core.Ports;

public sealed record ReponseConversation(
    string Texte,
    bool Sourcee,
    IReadOnlyList<string> DocumentsSources);
