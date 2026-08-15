namespace Agirh.Core.Ports;

public interface IDocumentChunkerPort
{
    IReadOnlyList<ChunkDocumentaire> Decouper(string documentSource, string markdown);
}
