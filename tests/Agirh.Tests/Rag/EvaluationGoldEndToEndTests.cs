using Agirh.Core.Ports;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Llm;
using FluentAssertions;
using Moq;
using Xunit;

namespace Agirh.Tests.Rag;

/// <summary>
/// Évaluation de bout en bout du jeu de Q/R gold (eval/gold_qa.json, milestone 9) : chaîne
/// réelle Router → RAG → Generator via Ollama (phi4-mini:3.8b), pas de mock. Mesure à la fois le
/// routage (documentaire / hors périmètre) et la fidélité de la réponse générée.
///
/// Volontairement EXCLUE de la suite par défaut (dotnet test sans filtre) : dépend d'Ollama,
/// ~48 appels LLM donc lente, et non déterministe par nature (contrairement à
/// <see cref="EvaluationGoldRetrievalTests"/> qui ne dépend que d'ONNX/Qdrant). À lancer à la
/// demande : dotnet test --filter "Category=Evaluation". Se termine sans assertion si Ollama
/// n'est pas accessible, même convention que OllamaRouterAdapterTests.
/// </summary>
[Trait("Category", "Evaluation")]
public class EvaluationGoldEndToEndTests : IClassFixture<GoldCorpusFixture>
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);
    private readonly GoldCorpusFixture _fixture;

    public EvaluationGoldEndToEndTests(GoldCorpusFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<bool> OllamaDisponibleAsync()
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(3) };
            var reponse = await http.GetAsync("/api/tags");
            return reponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private RepondreConversationUseCase CreerUseCase()
    {
        var ollamaHttp = new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromSeconds(120) };
        var ollamaClient = new OllamaClient(ollamaHttp);
        var router = new OllamaRouterAdapter(ollamaClient);
        var generateur = new OllamaGeneratorAdapter(ollamaClient);

        return new RepondreConversationUseCase(
            router, generateur, _fixture.Embedder!, _fixture.VectorSearch!, _fixture.Reranker!,
            Mock.Of<ICollaborateurRepository>(), Mock.Of<IWorkflowInstanceRepository>());
    }

    private static CompteUtilisateur CreerActeur() =>
        new(Guid.NewGuid(), "eval@agirh.test", "hash", RoleType.AdminQualite, null, Maintenant);

    public static IEnumerable<object[]> ToutesLesQuestions() => GoldQa.Charger().Select(q => new object[] { q });

    [Theory]
    [MemberData(nameof(ToutesLesQuestions))]
    public async Task Evaluation_QuestionGold_RoutageEtFideliteConformesAuxAttentes(GoldQuestion gold)
    {
        if (!_fixture.PrerequisDisponibles) return;
        if (!await OllamaDisponibleAsync()) return;

        var useCase = CreerUseCase();
        var acteur = CreerActeur();

        var reponse = await useCase.ExecuterAsync(acteur, gold.Question, null);

        reponse.Sourcee.Should().Be(gold.SourceeAttendu,
            $"{gold.Id} ({gold.Question}) — catégorie {gold.Categorie} : \"{reponse.Texte}\"");

        if (!gold.SourceeAttendu) return;

        reponse.DocumentsSources.Should().IntersectWith(gold.SourcesAcceptees,
            $"{gold.Id} devrait citer une source parmi {string.Join(" ou ", gold.SourcesAcceptees)}, a cité {string.Join(", ", reponse.DocumentsSources)}");

        if (gold.MotsClesAttendus.Length == 0) return;
        var texteMinuscule = reponse.Texte.ToLowerInvariant();
        gold.MotsClesAttendus.Any(mc => texteMinuscule.Contains(mc.ToLowerInvariant())).Should().BeTrue(
            $"{gold.Id} — réponse \"{reponse.Texte}\" devrait contenir un des mots-clés attendus : {string.Join(", ", gold.MotsClesAttendus)}");
    }
}
