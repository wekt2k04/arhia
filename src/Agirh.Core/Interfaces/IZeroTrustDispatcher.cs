using System.Text.Json;

namespace Agirh.Core.Interfaces;

public enum DenialCode { INSUFFICIENT_ROLE, SCOPE_MISMATCH, ACCOUNT_INACTIVE }

public abstract record DispatchResult;

public sealed record DispatchToTool(ToolDispatch Dispatch) : DispatchResult;

public sealed record DispatchRejected(AccessDenied Reason) : DispatchResult;

public sealed record IntentUnresolvable(string UserMessage) : DispatchResult;

public readonly record struct AccessDenied
{
    public required string UserMessage { get; init; }
    public required string RequiredRole { get; init; }
    public required string ActualRole { get; init; }
    public required DenialCode DenialCode { get; init; }

    public required string UserId { get; init; }
    public required string Intention { get; init; }
    public required DateTime TimestampUtc { get; init; }
}

public readonly record struct ToolDispatch
{
    public required string ToolName { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; init; }
    public required ConversationIntention SourceIntention { get; init; }
}

public readonly record struct DispatchInput
{
    public required DynamicContextVector CognitiveContext { get; init; }
    public required HardState Identity { get; init; }
    public JsonElement? ValidatedParameters { get; init; }
}

public interface IZeroTrustDispatcher
{
    Task<DispatchResult> DispatchAsync(DispatchInput input, CancellationToken ct = default);
}
