namespace Arhia.Core.Ports;

public interface IDocumentChunkerPort
{
    IReadOnlyList<DocumentChunk> Chunk(string documentSource, string markdown);
}
