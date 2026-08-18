using System.Threading;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Agirh.Tests.UseCases;

public class AnswerConversationUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (
        Mock<ILlmRouterPort> Router,
        Mock<ILlmGeneratorPort> Generator,
        Mock<IEmbeddingPort> Embedding,
        Mock<IVectorSearchPort> VectorSearch,
        Mock<IRerankerPort> Reranker,
        Mock<IEmployeeRepository> Employees,
        Mock<IWorkflowInstanceRepository> WorkflowInstances,
        AnswerConversationUseCase UseCase) CreateUseCase()
    {
        var router = new Mock<ILlmRouterPort>();
        var generator = new Mock<ILlmGeneratorPort>();
        var embedding = new Mock<IEmbeddingPort>();
        var vectorSearch = new Mock<IVectorSearchPort>();
        var reranker = new Mock<IRerankerPort>();
        var employees = new Mock<IEmployeeRepository>();
        var workflowInstances = new Mock<IWorkflowInstanceRepository>();

        var useCase = new AnswerConversationUseCase(
            router.Object, generator.Object, embedding.Object, vectorSearch.Object,
            reranker.Object, employees.Object, workflowInstances.Object);

        return (router, generator, embedding, vectorSearch, reranker, employees, workflowInstances, useCase);
    }

    private static UserAccount CreateEmployeeActor(Guid id) =>
        new(id, "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);

    [Fact]
    public async Task ExecuteAsync_EmptyQuestion_ThrowsArgumentException()
    {
        var (_, _, _, _, _, _, _, useCase) = CreateUseCase();
        var actor = CreateEmployeeActor(Guid.NewGuid());

        var act = () => useCase.ExecuteAsync(actor, "   ", null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_OutOfScopeIntent_ReturnsCannedResponseWithoutCallingTheGenerator()
    {
        var (router, generator, _, _, _, _, _, useCase) = CreateUseCase();
        var actor = CreateEmployeeActor(Guid.NewGuid());
        router.Setup(r => r.ClassifyAsync("Quel temps fait-il ?", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.OutOfScope);

        var response = await useCase.ExecuteAsync(actor, "Quel temps fait-il ?", null);

        response.Sourced.Should().BeFalse();
        response.Text.Should().Contain("RH");
        generator.Verify(g => g.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_DocumentaryQuestion_NoCandidateFound_RefusesWithoutCallingTheGenerator()
    {
        var (router, generator, embedding, vectorSearch, _, _, _, useCase) = CreateUseCase();
        var actor = CreateEmployeeActor(Guid.NewGuid());
        router.Setup(r => r.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.DocumentaryQuestion);
        embedding.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        vectorSearch.Setup(r => r.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DocumentChunk>());

        var response = await useCase.ExecuteAsync(actor, "Question sans réponse dans le corpus", null);

        response.Sourced.Should().BeFalse();
        response.Text.Should().Contain("pas trouvé");
        generator.Verify(g => g.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_DocumentaryQuestion_ScoresBelowThreshold_RefusesWithoutCallingTheGenerator()
    {
        var (router, generator, embedding, vectorSearch, reranker, _, _, useCase) = CreateUseCase();
        var actor = CreateEmployeeActor(Guid.NewGuid());
        var candidate = new DocumentChunk(Guid.NewGuid(), "doc.md", 0, "Titre", "Contenu peu pertinent", Score: 0f);

        router.Setup(r => r.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.DocumentaryQuestion);
        embedding.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        vectorSearch.Setup(r => r.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate with { Score = 0f } });

        var response = await useCase.ExecuteAsync(actor, "Question hors sujet du corpus", null);

        response.Sourced.Should().BeFalse();
        generator.Verify(g => g.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_DocumentaryQuestion_RelevantCandidate_CallsTheGeneratorAndSourcesTheResponse()
    {
        var (router, generator, embedding, vectorSearch, reranker, _, _, useCase) = CreateUseCase();
        var actor = CreateEmployeeActor(Guid.NewGuid());
        var candidate = new DocumentChunk(Guid.NewGuid(), "01_politique_onboarding.md", 0, "Onboarding", "Le RH crée la fiche.", Score: 0.9f);

        router.Setup(r => r.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.DocumentaryQuestion);
        embedding.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        vectorSearch.Setup(r => r.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate });
        generator.Setup(g => g.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Le RH de votre pôle crée votre fiche.");

        var response = await useCase.ExecuteAsync(actor, "Qui crée ma fiche collaborateur ?", null);

        response.Sourced.Should().BeTrue();
        response.Sources.Should().Contain("01_politique_onboarding.md");
        response.Text.Should().Be("Le RH de votre pôle crée votre fiche.");
    }

    [Fact]
    public async Task ExecuteAsync_DocumentaryQuestion_ScoreAboveThresholdButGeneratorRefuses_DoesNotSourceTheResponse()
    {
        // Le score de reranking mesure la proximité thématique, pas "la réponse est présente"
        // (constaté empiriquement, milestone 9) : un chunk du bon sujet peut passer le seuil
        // sans que la question précise y soit vraiment traitée. Le générateur reste alors le
        // seul signal fiable — s'il rend la phrase de refus imposée par le prompt, la réponse
        // ne doit pas être annoncée comme sourcée malgré un candidat retenu.
        var (router, generator, embedding, vectorSearch, reranker, _, _, useCase) = CreateUseCase();
        var actor = CreateEmployeeActor(Guid.NewGuid());
        var candidate = new DocumentChunk(Guid.NewGuid(), "01_politique_onboarding.md", 0, "Onboarding", "Contexte du bon sujet mais muet sur le detail precis demande.", Score: 0.75f);

        router.Setup(r => r.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.DocumentaryQuestion);
        embedding.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        vectorSearch.Setup(r => r.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate });
        generator.Setup(g => g.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Je n'ai pas trouvé cette information sur ce point précis.");

        var response = await useCase.ExecuteAsync(actor, "Quel est le délai exact pour X ?", null);

        response.Sourced.Should().BeFalse();
        response.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_CaseStatus_EmployeeWithoutLinkedRecord_ReturnsCaseNotFoundResponse()
    {
        var (router, _, _, _, _, employees, _, useCase) = CreateUseCase();
        var actor = CreateEmployeeActor(Guid.NewGuid());
        router.Setup(r => r.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.CaseStatus);
        employees.Setup(c => c.GetByUserAccountIdAsync(actor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var response = await useCase.ExecuteAsync(actor, "Où en est mon onboarding ?", null);

        response.Sourced.Should().BeFalse();
        response.Text.Should().Contain("pas trouvé");
    }

    [Fact]
    public async Task ExecuteAsync_CaseStatus_EmployeeWithInProgressCase_ReportsStatusAndRemainingItems()
    {
        var (router, _, _, _, _, employees, workflowInstances, useCase) = CreateUseCase();
        var accountId = Guid.NewGuid();
        var actor = CreateEmployeeActor(accountId);
        var departmentId = Guid.NewGuid();
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        employee.LinkUserAccount(accountId);

        var item1 = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item 1");
        var item2 = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item 2");
        var instance = new WorkflowInstance(Guid.NewGuid(), employee.Id, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item1, item2 }, Maintenant);
        instance.Check(item1.Id, ItemStatus.Done, Guid.NewGuid(), Maintenant, null);

        router.Setup(r => r.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.CaseStatus);
        employees.Setup(c => c.GetByUserAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        employees.Setup(c => c.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        workflowInstances.Setup(w => w.GetByEmployeeAsync(employee.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instance);

        var response = await useCase.ExecuteAsync(actor, "Où en est mon onboarding ?", null);

        response.Sourced.Should().BeFalse();
        response.Text.Should().Contain("en cours").And.Contain("1").And.Contain("2");
    }

    [Fact]
    public async Task ExecuteAsync_CaseStatus_HRTargetingEmployeeFromAnotherDepartment_ThrowsAccessDeniedException()
    {
        var (router, _, _, _, _, employees, _, useCase) = CreateUseCase();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employeeAnotherDepartment = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT002"), "Martin", "Léa", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);

        router.Setup(r => r.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.CaseStatus);
        employees.Setup(c => c.GetByIdAsync(employeeAnotherDepartment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeAnotherDepartment);

        var act = () => useCase.ExecuteAsync(rh, "Où en est son onboarding ?", employeeAnotherDepartment.Id);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    private static async IAsyncEnumerable<string> FragmentsAsync(params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            await Task.Yield();
            yield return fragment;
        }
    }

    [Fact]
    public async Task ExecuteStreamingAsync_OutOfScopeIntent_SingleFragmentThenNonSourcedCompletion()
    {
        var (router, generator, _, _, _, _, _, useCase) = CreateUseCase();
        var actor = CreateEmployeeActor(Guid.NewGuid());
        router.Setup(r => r.ClassifyAsync("Quel temps fait-il ?", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.OutOfScope);

        var events = new List<ConversationEvent>();
        await foreach (var ev in useCase.ExecuteStreamingAsync(actor, "Quel temps fait-il ?", null))
            events.Add(ev);

        events.Should().HaveCount(2);
        events[0].Should().BeOfType<TextFragment>().Which.Text.Should().Contain("RH");
        events[1].Should().BeOfType<ResponseCompleted>().Which.Sourced.Should().BeFalse();
        generator.Verify(g => g.GenerateResponseStreamingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_DocumentaryQuestion_RelevantCandidate_StreamsFragmentsThenSourcedCompletion()
    {
        var (router, generator, embedding, vectorSearch, reranker, _, _, useCase) = CreateUseCase();
        var actor = CreateEmployeeActor(Guid.NewGuid());
        var candidate = new DocumentChunk(Guid.NewGuid(), "01_politique_onboarding.md", 0, "Onboarding", "Le RH crée la fiche.", Score: 0.9f);

        router.Setup(r => r.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.DocumentaryQuestion);
        embedding.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        vectorSearch.Setup(r => r.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate });
        generator.Setup(g => g.GenerateResponseStreamingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(FragmentsAsync("Le RH ", "de votre pôle ", "crée votre fiche."));

        var events = new List<ConversationEvent>();
        await foreach (var ev in useCase.ExecuteStreamingAsync(actor, "Qui crée ma fiche collaborateur ?", null))
            events.Add(ev);

        var fragments = events.OfType<TextFragment>().ToList();
        fragments.Should().HaveCount(3);
        string.Concat(fragments.Select(f => f.Text)).Should().Be("Le RH de votre pôle crée votre fiche.");

        var completed = events.OfType<ResponseCompleted>().Should().ContainSingle().Which;
        completed.Sourced.Should().BeTrue();
        completed.Sources.Should().Contain("01_politique_onboarding.md");
    }

    [Fact]
    public async Task ExecuteStreamingAsync_GeneratorRefusesDespiteCandidate_NonSourcedCompletionWithoutSources()
    {
        var (router, generator, embedding, vectorSearch, reranker, _, _, useCase) = CreateUseCase();
        var actor = CreateEmployeeActor(Guid.NewGuid());
        var candidate = new DocumentChunk(Guid.NewGuid(), "doc.md", 0, "Titre", "Contexte du bon sujet mais muet sur le detail.", Score: 0.75f);

        router.Setup(r => r.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.DocumentaryQuestion);
        embedding.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        vectorSearch.Setup(r => r.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidate });
        generator.Setup(g => g.GenerateResponseStreamingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(FragmentsAsync("Je n'ai pas trouvé cette information."));

        var events = new List<ConversationEvent>();
        await foreach (var ev in useCase.ExecuteStreamingAsync(actor, "Question précise hors du contexte fourni ?", null))
            events.Add(ev);

        var completed = events.OfType<ResponseCompleted>().Should().ContainSingle().Which;
        completed.Sourced.Should().BeFalse();
        completed.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_CaseStatus_HRTargetingEmployeeFromOwnDepartment_IsAuthorized()
    {
        var (router, _, _, _, _, employees, workflowInstances, useCase) = CreateUseCase();
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT003"), "Petit", "Sam", "Dev", departmentId, ContractType.CDI, Maintenant);

        router.Setup(r => r.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversationIntent.CaseStatus);
        employees.Setup(c => c.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        workflowInstances.Setup(w => w.GetByEmployeeAsync(employee.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowInstance?)null);
        workflowInstances.Setup(w => w.GetByEmployeeAsync(employee.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowInstance?)null);

        var response = await useCase.ExecuteAsync(rh, "Où en est son dossier ?", employee.Id);

        response.Text.Should().Contain("Aucun dossier");
    }
}
