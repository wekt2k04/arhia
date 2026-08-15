using Microsoft.ML.Tokenizers;

namespace Agirh.Infrastructure.Rag;

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

    public static XlmRobertaTokenizer ChargerDepuisFichier(string cheminSentencePieceModel)
    {
        if (!File.Exists(cheminSentencePieceModel))
            throw new FileNotFoundException("Fichier sentencepiece.bpe.model introuvable.", cheminSentencePieceModel);

        using var flux = File.OpenRead(cheminSentencePieceModel);
        var tokenizer = SentencePieceTokenizer.Create(flux, addBeginningOfSentence: true, addEndOfSentence: true);
        return new XlmRobertaTokenizer(tokenizer);
    }

    public int CompterTokens(string texte) =>
        _tokenizer.CountTokens(texte, considerNormalization: true, considerPreTokenization: true);

    public long[] EncoderEnIdsHuggingFace(string texte)
    {
        var idsBruts = _tokenizer.EncodeToIds(
            texte,
            addBeginningOfSentence: true,
            addEndOfSentence: true,
            considerNormalization: true,
            considerPreTokenization: true);

        var resultat = new long[idsBruts.Count];
        for (var i = 0; i < idsBruts.Count; i++)
        {
            resultat[i] = CorrigerVersEspaceHuggingFace(idsBruts[i]);
        }

        return resultat;
    }

    private static long CorrigerVersEspaceHuggingFace(int idBrutSentencePiece) => idBrutSentencePiece switch
    {
        0 => UnkHuggingFace,
        1 => BosHuggingFace,
        2 => EosHuggingFace,
        _ => idBrutSentencePiece + 1
    };
}
