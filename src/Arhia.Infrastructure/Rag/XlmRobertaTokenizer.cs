using Microsoft.ML.Tokenizers;

namespace Arhia.Infrastructure.Rag;

/// <summary>
/// Nos deux modeles RAG (embedding et reranking) sont de la famille XLM-RoBERTa,
/// tokenises via SentencePiece. Microsoft.ML.Tokenizers.SentencePieceTokenizer restitue
/// les identifiants dans l'espace du vocabulaire SentencePiece brut (unk=0, bos=1, eos=2,
/// pieces a partir de 3), alors que les poids ONNX publies attendent l'espace d'identifiants
/// Hugging Face (bos=0, pad=1, eos=2, unk=3, pieces a partir de 4) - verifie empiriquement en
/// comparant les identifiants retournes au tableau model.vocab du tokenizer.json Hugging Face.
/// Cette classe applique la correction avant tout usage des identifiants.
/// </summary>
public sealed class XlmRobertaTokenizer
{
    private const int BosHuggingFace = 0;
    private const int EosHuggingFace = 2;
    private const int UnkHuggingFace = 3;

    private readonly SentencePieceTokenizer _tokenizer;

    private XlmRobertaTokenizer(SentencePieceTokenizer tokenizer)
    {
        _tokenizer = tokenizer;
    }

    public static XlmRobertaTokenizer LoadFromFile(string sentencePieceModelPath)
    {
        if (!File.Exists(sentencePieceModelPath))
            throw new FileNotFoundException("Fichier sentencepiece.bpe.model introuvable.", sentencePieceModelPath);

        using var stream = File.OpenRead(sentencePieceModelPath);
        var tokenizer = SentencePieceTokenizer.Create(stream, addBeginningOfSentence: true, addEndOfSentence: true);
        return new XlmRobertaTokenizer(tokenizer);
    }

    public int CountTokens(string text) =>
        _tokenizer.CountTokens(text, considerNormalization: true, considerPreTokenization: true);

    public long[] EncodeToHuggingFaceIds(string text)
    {
        var rawIds = _tokenizer.EncodeToIds(
            text,
            addBeginningOfSentence: true,
            addEndOfSentence: true,
            considerNormalization: true,
            considerPreTokenization: true);

        var result = new long[rawIds.Count];
        for (var i = 0; i < rawIds.Count; i++)
        {
            result[i] = FixToHuggingFaceSpace(rawIds[i]);
        }

        return result;
    }

    /// <summary>
    /// Encodage de paire (requete, document) pour un cross-encoder de reranking, au format
    /// standard RoBERTa/XLM-RoBERTa : &lt;s&gt; requete &lt;/s&gt;&lt;/s&gt; document &lt;/s&gt;
    /// (double separateur EOS entre les deux textes, convention de la famille RoBERTa —
    /// pas un choix specifique a ce modele).
    /// </summary>
    public long[] EncodePairToHuggingFaceIds(string textA, string textB)
    {
        var idsA = _tokenizer.EncodeToIds(textA, addBeginningOfSentence: false, addEndOfSentence: false, considerNormalization: true, considerPreTokenization: true);
        var idsB = _tokenizer.EncodeToIds(textB, addBeginningOfSentence: false, addEndOfSentence: false, considerNormalization: true, considerPreTokenization: true);

        var result = new List<long> { BosHuggingFace };
        result.AddRange(idsA.Select(FixToHuggingFaceSpace));
        result.Add(EosHuggingFace);
        result.Add(EosHuggingFace);
        result.AddRange(idsB.Select(FixToHuggingFaceSpace));
        result.Add(EosHuggingFace);

        return result.ToArray();
    }

    private static long FixToHuggingFaceSpace(int rawSentencePieceId) => rawSentencePieceId switch
    {
        0 => UnkHuggingFace,
        1 => BosHuggingFace,
        2 => EosHuggingFace,
        _ => rawSentencePieceId + 1
    };
}
