using Agirh.Core.Ports;

namespace Agirh.Infrastructure.Rag;

public sealed class MarkdownChunkerAdapter : IDocumentChunkerPort
{
    private readonly MarkdownChunker _chunker;

    public MarkdownChunkerAdapter(XlmRobertaTokenizer tokenizerPourComptage, int maxTokensParChunk = 400, double tauxRecouvrement = 0.15)
    {
        _chunker = new MarkdownChunker(tokenizerPourComptage.CompterTokens, maxTokensParChunk, tauxRecouvrement);
    }

    public IReadOnlyList<ChunkDocumentaire> Decouper(string documentSource, string markdown)
    {
        var chunksBruts = _chunker.Decouper(markdown);

        return chunksBruts
            .Select((chunk, index) => new ChunkDocumentaire(
                ChunkDocumentaire.CalculerId(documentSource, index),
                documentSource,
                index,
                chunk.CheminTitres,
                chunk.Contenu))
            .ToList();
    }
}
