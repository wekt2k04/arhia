using Arhia.Infrastructure.Rag;
using FluentAssertions;

namespace Arhia.Tests.Rag;

/// <summary>
/// Tests d'integration reels contre le fichier sentencepiece.bpe.model (~5 Mo, non commite -
/// voir .claude/scripts/download-models.ps1). Si le modele n'est pas present sur la machine, le test
/// se termine sans assertion plutot que d'echouer bloquant la suite sur une machine fraiche.
/// </summary>
public class XlmRobertaTokenizerTests
{
    private static string ModelPath => Path.Combine(RepoPaths.ModelesEmbedding, "sentencepiece.bpe.model");

    [Fact]
    public void EncodeToHuggingFaceIds_SimpleText_IsFramedByBosAndEosInHuggingFaceSpace()
    {
        if (!File.Exists(ModelPath)) return;

        var tokenizer = XlmRobertaTokenizer.LoadFromFile(ModelPath);

        var ids = tokenizer.EncodeToHuggingFaceIds("Bonjour tout le monde.");

        ids.Should().NotBeEmpty();
        ids[0].Should().Be(0, "<s> doit être l'identifiant 0 en espace Hugging Face, pas l'identifiant brut SentencePiece");
        ids[^1].Should().Be(2, "</s> doit être l'identifiant 2 en espace Hugging Face");
        ids.Should().OnlyContain(id => id >= 0 && id < 250002);
    }

    [Fact]
    public void CountTokens_LongerText_GivesAHigherCountThanShortText()
    {
        if (!File.Exists(ModelPath)) return;

        var tokenizer = XlmRobertaTokenizer.LoadFromFile(ModelPath);

        var short_ = tokenizer.CountTokens("Bonjour.");
        var longer = tokenizer.CountTokens("Bonjour, comment allez-vous aujourd'hui ? J'espère que tout va bien pour vous.");

        longer.Should().BeGreaterThan(short_);
    }

    [Fact]
    public void LoadFromFile_NonExistentPath_ThrowsFileNotFoundException()
    {
        var act = () => XlmRobertaTokenizer.LoadFromFile(Path.Combine(RepoPaths.Racine, "models", "inexistant.model"));

        act.Should().Throw<FileNotFoundException>();
    }
}
