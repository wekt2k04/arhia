using Arhia.Infrastructure.Rag;
using FluentAssertions;

namespace Arhia.Tests.Rag;

public class MarkdownChunkerTests
{
    private static int CountWords(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    [Fact]
    public void Constructor_NegativeOrZeroMaxTokens_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new MarkdownChunker(CountWords, maxTokensPerChunk: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void Constructor_OverlapRatioOutOfBounds_ThrowsArgumentOutOfRangeException(double ratio)
    {
        var act = () => new MarkdownChunker(CountWords, overlapRatio: ratio);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Chunk_EmptyOrNullText_ReturnsNoChunk()
    {
        var chunker = new MarkdownChunker(CountWords);

        chunker.Chunk("").Should().BeEmpty();
        chunker.Chunk("   \n  ").Should().BeEmpty();
    }

    [Fact]
    public void Chunk_OneShortSection_ProducesASingleChunkPrefixedByTheTitle()
    {
        var markdown = "# Politique d'Onboarding\n\nCeci est un court paragraphe d'introduction.";
        var chunker = new MarkdownChunker(CountWords, maxTokensPerChunk: 100);

        var chunks = chunker.Chunk(markdown);

        chunks.Should().ContainSingle();
        chunks[0].TitlePath.Should().Be("Politique d'Onboarding");
        chunks[0].Content.Should().Contain("Politique d'Onboarding").And.Contain("court paragraphe");
    }

    [Fact]
    public void Chunk_SeveralH2Sections_ProducesOneChunkPerSection()
    {
        var markdown = """
            # Politique

            ## Section A
            Contenu de la section A.

            ## Section B
            Contenu de la section B.
            """;
        var chunker = new MarkdownChunker(CountWords, maxTokensPerChunk: 100);

        var chunks = chunker.Chunk(markdown);

        chunks.Should().HaveCount(2);
        chunks[0].TitlePath.Should().Be("Politique > Section A");
        chunks[1].TitlePath.Should().Be("Politique > Section B");
    }

    [Fact]
    public void Chunk_NestedH1H2H3Titles_BuildsTheFullPath()
    {
        var markdown = """
            # Titre Un

            ## Titre Deux

            ### Titre Trois
            Contenu profond.
            """;
        var chunker = new MarkdownChunker(CountWords, maxTokensPerChunk: 100);

        var chunks = chunker.Chunk(markdown);

        chunks.Should().ContainSingle();
        chunks[0].TitlePath.Should().Be("Titre Un > Titre Deux > Titre Trois");
    }

    [Fact]
    public void Chunk_ReturnToTheSameTitleLevel_ResetsThePath()
    {
        var markdown = """
            # Racine

            ## Premiere sous-section
            ### Sous-sous-section
            Contenu.

            ## Deuxieme sous-section
            Autre contenu.
            """;
        var chunker = new MarkdownChunker(CountWords, maxTokensPerChunk: 100);

        var chunks = chunker.Chunk(markdown);

        chunks.Should().HaveCount(2);
        chunks[0].TitlePath.Should().Be("Racine > Premiere sous-section > Sous-sous-section");
        chunks[1].TitlePath.Should().Be("Racine > Deuxieme sous-section");
    }

    [Fact]
    public void Chunk_SectionExceedingTheBudget_IsSplitIntoSeveralChunks()
    {
        var paragraphs = Enumerable.Range(1, 10).Select(i => $"Paragraphe numero {i} avec plusieurs mots dedans.");
        var markdown = "# Section longue\n\n" + string.Join("\n\n", paragraphs);
        var chunker = new MarkdownChunker(CountWords, maxTokensPerChunk: 30, overlapRatio: 0.2);

        var chunks = chunker.Chunk(markdown);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(c => c.TitlePath == "Section longue");
    }

    [Fact]
    public void Chunk_LongSection_ProducesOverlapBetweenConsecutiveChunks()
    {
        var paragraphs = Enumerable.Range(1, 8).Select(i => $"P{i} mot mot mot mot mot mot.");
        var markdown = "# Section\n\n" + string.Join("\n\n", paragraphs);
        var chunker = new MarkdownChunker(CountWords, maxTokensPerChunk: 20, overlapRatio: 0.3);

        var chunks = chunker.Chunk(markdown);

        chunks.Should().HaveCountGreaterThan(1);
        // Le dernier paragraphe du premier chunk doit reapparaitre dans le second (recouvrement)
        var lastParagraphChunk1 = chunks[0].Content.Split("\n\n").Last();
        chunks[1].Content.Should().Contain(lastParagraphChunk1);
    }

    [Fact]
    public void Chunk_SingleParagraphExceedingTheBudget_IsEmittedAsIsWithoutCrashing()
    {
        var hugeParagraph = string.Join(" ", Enumerable.Repeat("mot", 500));
        var markdown = $"# Section\n\n{hugeParagraph}";
        var chunker = new MarkdownChunker(CountWords, maxTokensPerChunk: 50);

        var act = () => chunker.Chunk(markdown);

        act.Should().NotThrow();
        var chunks = act();
        chunks.Should().ContainSingle();
        chunks[0].TokenCount.Should().BeGreaterThan(50);
    }

    [Fact]
    public void Chunk_SectionWithoutContent_IsNotIncluded()
    {
        var markdown = "# Titre vide\n\n## Sous-titre avec contenu\nDu texte ici.";
        var chunker = new MarkdownChunker(CountWords, maxTokensPerChunk: 100);

        var chunks = chunker.Chunk(markdown);

        chunks.Should().ContainSingle();
        chunks[0].TitlePath.Should().Be("Titre vide > Sous-titre avec contenu");
    }
}
