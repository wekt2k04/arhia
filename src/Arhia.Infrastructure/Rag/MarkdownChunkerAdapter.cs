using Arhia.Core.Ports;

namespace Arhia.Infrastructure.Rag;

public sealed class MarkdownChunkerAdapter : IDocumentChunkerPort
{
    private readonly MarkdownChunker _chunker;

    public MarkdownChunkerAdapter(XlmRobertaTokenizer tokenizerForCounting, int maxTokensPerChunk = 400, double overlapRatio = 0.15)
    {
        _chunker = new MarkdownChunker(tokenizerForCounting.CountTokens, maxTokensPerChunk, overlapRatio);
    }

    public IReadOnlyList<DocumentChunk> Chunk(string documentSource, string markdown)
    {
        var rawChunks = _chunker.Chunk(markdown);

        return rawChunks
            .Select((chunk, index) => new DocumentChunk(
                DocumentChunk.ComputeId(documentSource, index),
                documentSource,
                index,
                chunk.TitlePath,
                chunk.Content))
            .ToList();
    }
}
