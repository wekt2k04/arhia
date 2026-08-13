using System.Text.Json;

namespace Agirh.Api.Logging;

/// <summary>Contrat du log d'audit structuré (1 ligne JSON par requête de chat).</summary>
public interface IChatAuditLogger
{
    Task AppendAsync(ChatAuditEntry entry);
}

/// <summary>Entrée d'audit : rôle de l'utilisateur, modèles ayant intervenu, résultat.</summary>
public sealed class ChatAuditEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public string? ConversationId { get; set; }
    public string? UserId { get; set; }
    public string? Role { get; set; }
    public string? Intention { get; set; }
    public float? Confidence { get; set; }
    public ChatAuditModels? Models { get; set; }
    public string? Tool { get; set; }
    public string? Outcome { get; set; }
    public int? Status { get; set; }
    public long LatencyMs { get; set; }
    public int TokenCount { get; set; }
    public int ReflectionLoops { get; set; }
    public bool? CheckerValid { get; set; }
    public string? WidgetId { get; set; }
    public string? Suggestion { get; set; }
    public string? Message { get; set; }
    public string? Response { get; set; }
    public string? Error { get; set; }
}

public sealed class ChatAuditModels
{
    public string? Profiler { get; set; }
    public string? Synthesizer { get; set; }
    public string? Checker { get; set; }
    public string? Embedding { get; set; }
}

/// <summary>
/// Écrit les entrées d'audit en JSON Lines dans un fichier RÉINITIALISÉ à chaque
/// démarrage (FileMode.Create au premier writer) — un run de l'API = un audit frais.
/// </summary>
public sealed class ChatAuditLogger : IChatAuditLogger, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly StreamWriter _writer;
    private readonly object _sync = new();

    public ChatAuditLogger(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _writer = new StreamWriter(new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true,
        };
    }

    public Task AppendAsync(ChatAuditEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, JsonOptions);
        lock (_sync)
        {
            _writer.WriteLine(line);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer.Dispose();
        }
    }
}

/// <summary>No-op : utilisé si le fichier d'audit ne peut pas être initialisé (audit désactivé).</summary>
public sealed class NoopChatAuditLogger : IChatAuditLogger
{
    public Task AppendAsync(ChatAuditEntry entry) => Task.CompletedTask;
}
