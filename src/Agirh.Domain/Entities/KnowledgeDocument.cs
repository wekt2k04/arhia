namespace Agirh.Domain.Entities;

public class KnowledgeDocument
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ChunkText { get; set; } = string.Empty;
    public byte[] Embedding { get; set; } = Array.Empty<byte>();
    public string SourceFile { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool HasEmbedding => Embedding.Length > 0;
}
