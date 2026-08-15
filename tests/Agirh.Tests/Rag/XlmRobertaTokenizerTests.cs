using Agirh.Infrastructure.Rag;
using FluentAssertions;

namespace Agirh.Tests.Rag;

/// <summary>
/// Tests d'integration reels contre le fichier sentencepiece.bpe.model (~5 Mo, non commite -
/// voir scripts/download-models.ps1). Si le modele n'est pas present sur la machine, le test
/// se termine sans assertion plutot que d'echouer bloquant la suite sur une machine fraiche.
/// </summary>
public class XlmRobertaTokenizerTests
{
    private static string CheminModele => Path.Combine(RepoPaths.ModelesEmbedding, "sentencepiece.bpe.model");

    [Fact]
    public void EncoderEnIdsHuggingFace_TexteSimple_EncadreParBosEtEosEnEspaceHuggingFace()
    {
        if (!File.Exists(CheminModele)) return;

        var tokenizer = XlmRobertaTokenizer.ChargerDepuisFichier(CheminModele);

        var ids = tokenizer.EncoderEnIdsHuggingFace("Bonjour tout le monde.");

        ids.Should().NotBeEmpty();
        ids[0].Should().Be(0, "<s> doit être l'identifiant 0 en espace Hugging Face, pas l'identifiant brut SentencePiece");
        ids[^1].Should().Be(2, "</s> doit être l'identifiant 2 en espace Hugging Face");
        ids.Should().OnlyContain(id => id >= 0 && id < 250002);
    }

    [Fact]
    public void CompterTokens_TextePlusLong_DonneUnCompteSuperieurATexteCourt()
    {
        if (!File.Exists(CheminModele)) return;

        var tokenizer = XlmRobertaTokenizer.ChargerDepuisFichier(CheminModele);

        var court = tokenizer.CompterTokens("Bonjour.");
        var plusLong = tokenizer.CompterTokens("Bonjour, comment allez-vous aujourd'hui ? J'espère que tout va bien pour vous.");

        plusLong.Should().BeGreaterThan(court);
    }

    [Fact]
    public void ChargerDepuisFichier_CheminInexistant_LeveFileNotFoundException()
    {
        var act = () => XlmRobertaTokenizer.ChargerDepuisFichier(Path.Combine(RepoPaths.Racine, "models", "inexistant.model"));

        act.Should().Throw<FileNotFoundException>();
    }
}
