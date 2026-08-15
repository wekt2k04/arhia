using Agirh.Infrastructure.Rag;
using FluentAssertions;

namespace Agirh.Tests.Rag;

public class MarkdownChunkerTests
{
    private static int CompterMots(string texte) =>
        texte.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    [Fact]
    public void Constructeur_MaxTokensNegatifOuZero_LeveArgumentOutOfRangeException()
    {
        var act = () => new MarkdownChunker(CompterMots, maxTokensParChunk: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void Constructeur_TauxRecouvrementHorsBornes_LeveArgumentOutOfRangeException(double taux)
    {
        var act = () => new MarkdownChunker(CompterMots, tauxRecouvrement: taux);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Decouper_TexteVideOuNull_RetourneAucunChunk()
    {
        var chunker = new MarkdownChunker(CompterMots);

        chunker.Decouper("").Should().BeEmpty();
        chunker.Decouper("   \n  ").Should().BeEmpty();
    }

    [Fact]
    public void Decouper_UneSectionCourte_ProduitUnSeulChunkPrefixeParLeTitre()
    {
        var markdown = "# Politique d'Onboarding\n\nCeci est un court paragraphe d'introduction.";
        var chunker = new MarkdownChunker(CompterMots, maxTokensParChunk: 100);

        var chunks = chunker.Decouper(markdown);

        chunks.Should().ContainSingle();
        chunks[0].CheminTitres.Should().Be("Politique d'Onboarding");
        chunks[0].Contenu.Should().Contain("Politique d'Onboarding").And.Contain("court paragraphe");
    }

    [Fact]
    public void Decouper_PlusieursSectionsH2_ProduitUnChunkParSection()
    {
        var markdown = """
            # Politique

            ## Section A
            Contenu de la section A.

            ## Section B
            Contenu de la section B.
            """;
        var chunker = new MarkdownChunker(CompterMots, maxTokensParChunk: 100);

        var chunks = chunker.Decouper(markdown);

        chunks.Should().HaveCount(2);
        chunks[0].CheminTitres.Should().Be("Politique > Section A");
        chunks[1].CheminTitres.Should().Be("Politique > Section B");
    }

    [Fact]
    public void Decouper_TitresImbriquesH1H2H3_ConstruitLeCheminComplet()
    {
        var markdown = """
            # Titre Un

            ## Titre Deux

            ### Titre Trois
            Contenu profond.
            """;
        var chunker = new MarkdownChunker(CompterMots, maxTokensParChunk: 100);

        var chunks = chunker.Decouper(markdown);

        chunks.Should().ContainSingle();
        chunks[0].CheminTitres.Should().Be("Titre Un > Titre Deux > Titre Trois");
    }

    [Fact]
    public void Decouper_RetourAuMemeNiveauDeTitre_ReinitialiseLeChemin()
    {
        var markdown = """
            # Racine

            ## Premiere sous-section
            ### Sous-sous-section
            Contenu.

            ## Deuxieme sous-section
            Autre contenu.
            """;
        var chunker = new MarkdownChunker(CompterMots, maxTokensParChunk: 100);

        var chunks = chunker.Decouper(markdown);

        chunks.Should().HaveCount(2);
        chunks[0].CheminTitres.Should().Be("Racine > Premiere sous-section > Sous-sous-section");
        chunks[1].CheminTitres.Should().Be("Racine > Deuxieme sous-section");
    }

    [Fact]
    public void Decouper_SectionDepassantLeBudget_EstSousDecoupeeEnPlusieursChunks()
    {
        var paragraphes = Enumerable.Range(1, 10).Select(i => $"Paragraphe numero {i} avec plusieurs mots dedans.");
        var markdown = "# Section longue\n\n" + string.Join("\n\n", paragraphes);
        var chunker = new MarkdownChunker(CompterMots, maxTokensParChunk: 30, tauxRecouvrement: 0.2);

        var chunks = chunker.Decouper(markdown);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(c => c.CheminTitres == "Section longue");
    }

    [Fact]
    public void Decouper_SectionLongue_ProduitUnRecouvrementEntreChunksConsecutifs()
    {
        var paragraphes = Enumerable.Range(1, 8).Select(i => $"P{i} mot mot mot mot mot mot.");
        var markdown = "# Section\n\n" + string.Join("\n\n", paragraphes);
        var chunker = new MarkdownChunker(CompterMots, maxTokensParChunk: 20, tauxRecouvrement: 0.3);

        var chunks = chunker.Decouper(markdown);

        chunks.Should().HaveCountGreaterThan(1);
        // Le dernier paragraphe du premier chunk doit reapparaitre dans le second (recouvrement)
        var dernierParagrapheChunk1 = chunks[0].Contenu.Split("\n\n").Last();
        chunks[1].Contenu.Should().Contain(dernierParagrapheChunk1);
    }

    [Fact]
    public void Decouper_UnSeulParagrapheDepassantLeBudget_EstEmisTelQuelSansPlanter()
    {
        var paragrapheEnorme = string.Join(" ", Enumerable.Repeat("mot", 500));
        var markdown = $"# Section\n\n{paragrapheEnorme}";
        var chunker = new MarkdownChunker(CompterMots, maxTokensParChunk: 50);

        var act = () => chunker.Decouper(markdown);

        act.Should().NotThrow();
        var chunks = act();
        chunks.Should().ContainSingle();
        chunks[0].NombreTokens.Should().BeGreaterThan(50);
    }

    [Fact]
    public void Decouper_SectionSansContenu_NestPasIncluse()
    {
        var markdown = "# Titre vide\n\n## Sous-titre avec contenu\nDu texte ici.";
        var chunker = new MarkdownChunker(CompterMots, maxTokensParChunk: 100);

        var chunks = chunker.Decouper(markdown);

        chunks.Should().ContainSingle();
        chunks[0].CheminTitres.Should().Be("Titre vide > Sous-titre avec contenu");
    }
}
