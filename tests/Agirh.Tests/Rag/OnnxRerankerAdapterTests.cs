using Agirh.Core.Ports;
using Agirh.Infrastructure.Rag;
using FluentAssertions;

namespace Agirh.Tests.Rag;

/// <summary>
/// Test d'integration reel contre le modele ONNX de reranking (~570 Mo, non commite -
/// voir .claude/scripts/download-models.ps1). Se termine sans assertion si le modele est absent.
/// </summary>
public class OnnxRerankerAdapterTests
{
    private static string OnnxPath => Path.Combine(RepoPaths.ModelesReranker, "model_quantized.onnx");
    private static string SpmPath => Path.Combine(RepoPaths.ModelesReranker, "sentencepiece.bpe.model");

    [Fact]
    public async Task RerankAsync_RelevantAndIrrelevantDocument_RanksTheRelevantOneFirst()
    {
        if (!File.Exists(OnnxPath) || !File.Exists(SpmPath)) return;

        using var reranker = new OnnxRerankerAdapter(OnnxPath, SpmPath);

        var relevant = new DocumentChunk(
            Guid.NewGuid(), "test.md", 0, "Onboarding",
            "Le RH du pôle crée la fiche du collaborateur et déclenche l'instanciation de la checklist d'onboarding lors de son arrivée.");
        var irrelevant = new DocumentChunk(
            Guid.NewGuid(), "test.md", 1, "Cuisine",
            "La recette de la tarte aux pommes nécessite du beurre, du sucre et des pommes coupées en fines lamelles.");

        var results = await reranker.RerankAsync(
            "Comment se déroule l'intégration d'un nouveau collaborateur ?",
            new[] { irrelevant, relevant }); // volontairement dans le mauvais ordre en entree

        results.Should().HaveCount(2);
        results[0].ChunkIndex.Should().Be(0, "le chunk sur l'onboarding est sémantiquement pertinent pour la requête");
        results[0].Score.Should().BeGreaterThan(results[1].Score);
    }

    [Fact]
    public async Task RerankAsync_NoCandidate_ReturnsAnEmptyList()
    {
        if (!File.Exists(OnnxPath) || !File.Exists(SpmPath)) return;

        using var reranker = new OnnxRerankerAdapter(OnnxPath, SpmPath);

        var results = await reranker.RerankAsync("requête", Array.Empty<DocumentChunk>());

        results.Should().BeEmpty();
    }
}
