using Agirh.Core.Ports;
using Agirh.Infrastructure.Rag;
using FluentAssertions;

namespace Agirh.Tests.Rag;

/// <summary>
/// Test d'integration reel contre le modele ONNX de reranking (~570 Mo, non commite -
/// voir scripts/download-models.ps1). Se termine sans assertion si le modele est absent.
/// </summary>
public class OnnxRerankerAdapterTests
{
    private static string CheminOnnx => Path.Combine(RepoPaths.ModelesReranker, "model_quantized.onnx");
    private static string CheminSpm => Path.Combine(RepoPaths.ModelesReranker, "sentencepiece.bpe.model");

    [Fact]
    public async Task RerankAsync_DocumentPertinentEtNonPertinent_ClasseLePertinentEnPremier()
    {
        if (!File.Exists(CheminOnnx) || !File.Exists(CheminSpm)) return;

        using var reranker = new OnnxRerankerAdapter(CheminOnnx, CheminSpm);

        var pertinent = new ChunkDocumentaire(
            Guid.NewGuid(), "test.md", 0, "Onboarding",
            "Le RH du pôle crée la fiche du collaborateur et déclenche l'instanciation de la checklist d'onboarding lors de son arrivée.");
        var nonPertinent = new ChunkDocumentaire(
            Guid.NewGuid(), "test.md", 1, "Cuisine",
            "La recette de la tarte aux pommes nécessite du beurre, du sucre et des pommes coupées en fines lamelles.");

        var resultats = await reranker.RerankAsync(
            "Comment se déroule l'intégration d'un nouveau collaborateur ?",
            new[] { nonPertinent, pertinent }); // volontairement dans le mauvais ordre en entree

        resultats.Should().HaveCount(2);
        resultats[0].ChunkIndex.Should().Be(0, "le chunk sur l'onboarding est sémantiquement pertinent pour la requête");
        resultats[0].Score.Should().BeGreaterThan(resultats[1].Score);
    }

    [Fact]
    public async Task RerankAsync_AucunCandidat_RetourneUneListeVide()
    {
        if (!File.Exists(CheminOnnx) || !File.Exists(CheminSpm)) return;

        using var reranker = new OnnxRerankerAdapter(CheminOnnx, CheminSpm);

        var resultats = await reranker.RerankAsync("requête", Array.Empty<ChunkDocumentaire>());

        resultats.Should().BeEmpty();
    }
}
