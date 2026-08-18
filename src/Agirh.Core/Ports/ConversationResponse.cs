namespace Agirh.Core.Ports;

public sealed record ConversationResponse(
    string Text,
    bool Sourced,
    IReadOnlyList<string> Sources);
