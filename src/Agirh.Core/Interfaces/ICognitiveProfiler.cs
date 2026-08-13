using System.Text.RegularExpressions;

namespace Agirh.Core.Interfaces;

public enum UrgencyLevel { Low, Medium, High, Critical }

public enum TonePreference { Professional, Concise, Empathetic, Directive }

public enum ConversationIntention
{
    LeaveRequest,
    LeaveBalance,
    PayrollSettlement,
    OnboardingChecklist,
    KnowledgeSearch,
    ITAccessRevocation,
    ManagerAlert,
    LeaveApproval,
    GeneralInquiry,
    SmallTalk,
    Greeting,
    SalaryAdvance,
    Unknown
}

public readonly record struct RawMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; }
}

public readonly record struct CognitiveExtractionInput
{
    public required IReadOnlyList<RawMessage> RecentMessages { get; init; }
    public required string ConversationId { get; init; }
}

public readonly record struct DynamicContextVector
{
    public required ConversationIntention Intention { get; init; }
    public required float ConfidenceScore { get; init; }

    public required string MainIdea { get; init; }
    public required UrgencyLevel Urgency { get; init; }
    public required TonePreference Tone { get; init; }

    public required bool IsFollowUp { get; init; }
    public required string TriggerPhrase { get; init; }
    public IReadOnlyDictionary<string, string?> ExtractedEntities { get; init; }

    public long ProcessingTimeMs { get; init; }
    public string? ModelUsed { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
}

public interface ICognitiveProfiler
{
    Task<DynamicContextVector> ExtractAsync(CognitiveExtractionInput input, CancellationToken ct = default);
}
