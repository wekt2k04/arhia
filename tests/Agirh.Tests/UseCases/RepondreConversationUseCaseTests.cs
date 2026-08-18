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

public class RepondreConversationUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (
        Mock<ILlmRouterPort> Router,
        Mock<ILlmGeneratorPort> Generateur,
        Mock<IEmbeddingPort> Embedding,
        Mock<IVectorSearchPort> RechercheVectorielle,
        Mock<IRerankerPort> Reranker,
        Mock<IEmployeeRepository> Employees,
        Mock<IWorkflowInstanceRepository> WorkflowInstances,
        RepondreConversationUseCase UseCase) CreerUseCase()
    {
        var router = new Mock<ILlmRouterPort>();
        var generateur = new Mock<ILlmGeneratorPort>();
        var embedding = new Mock<IEmbeddingPort>();
        var rechercheVectorielle = new Mock<IVectorSearchPort>();
        var reranker = new Mock<IRerankerPort>();
        var employees = new Mock<IEmployeeRepository>();
        var workflowInstances = new Mock<IWorkflowInstanceRepository>();

        var useCase = new RepondreConversationUseCase(
            router.Object, generateur.Object, embedding.Object, rechercheVectorielle.Object,
            reranker.Object, employees.Object, workflowInstances.Object);

        return (router, generateur, embedding, rechercheVectorielle, reranker, employees, workflowInstances, useCase);
    }

    private static UserAccount CreerCollaborateurActeur(Guid id) =>
        new(id, "collab@agirh.test", "hash", RoleType.Employee, null, Maintenant);

    [Fact]
    public async Task ExecuterAsync_QuestionVide_LeveArgumentException()
    {
        var (_, _, _, _, _, _, _, useCase) = CreerUseCase();
        var actor = CreerCollaborateurActeur(Guid.NewGuid());

        var act = () => useCase.ExecuteAsync(actor, "   ", null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuterAsync_IntentionHorsPerimetre_RetourneReponseCanneeSansAppelerLeGenerateur()
    {
        var (router, generateur, _, _, _, _, _, useCase) = CreerUseCase();
        var actor = CreerCollaborateurActeur(Guid.NewGuid());
        router.Setup(r => r.ClassifierAsync("Quel temps fait-il ?", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.HorsPerimetre);

        var reponse = await useCase.ExecuteAsync(actor, "Quel temps fait-il ?", null);

        reponse.Sourcee.Should().BeFalse();
        reponse.Texte.Should().Contain("RH");
        generateur.Verify(g => g.GenererReponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_QuestionDocumentaire_AucunCandidatTrouve_RefuseSansAppelerLeGenerateur()
    {
        var (router, generateur, embedding, rechercheVectorielle, _, _, _, useCase) = CreerUseCase();
        var actor = CreerCollaborateurActeur(Guid.NewGuid());
        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.QuestionDocumentaire);
        embedding.Setup(e => e.GenererEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        rechercheVectorielle.Setup(r => r.RechercherAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChunkDocumentaire>());

        var reponse = await useCase.ExecuteAsync(actor, "Question sans réponse dans le corpus", null);

        reponse.Sourcee.Should().BeFalse();
        reponse.Texte.Should().Contain("pas trouvé");
        generateur.Verify(g => g.GenererReponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_QuestionDocumentaire_ScoresSousLeSeuil_RefuseSansAppelerLeGenerateur()
    {
        var (router, generateur, embedding, rechercheVectorielle, reranker, _, _, useCase) = CreerUseCase();
        var actor = CreerCollaborateurActeur(Guid.NewGuid());
        var candidat = new ChunkDocumentaire(Guid.NewGuid(), "doc.md", 0, "Titre", "Contenu peu pertinent", Score: 0f);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.QuestionDocumentaire);
        embedding.Setup(e => e.GenererEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        rechercheVectorielle.Setup(r => r.RechercherAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkDocumentaire>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat with { Score = 0f } });

        var reponse = await useCase.ExecuteAsync(actor, "Question hors sujet du corpus", null);

        reponse.Sourcee.Should().BeFalse();
        generateur.Verify(g => g.GenererReponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_QuestionDocumentaire_CandidatPertinent_AppelleLeGenerateurEtSourceLaReponse()
    {
        var (router, generateur, embedding, rechercheVectorielle, reranker, _, _, useCase) = CreerUseCase();
        var actor = CreerCollaborateurActeur(Guid.NewGuid());
        var candidat = new ChunkDocumentaire(Guid.NewGuid(), "01_politique_onboarding.md", 0, "Onboarding", "Le RH crée la fiche.", Score: 0.9f);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.QuestionDocumentaire);
        embedding.Setup(e => e.GenererEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        rechercheVectorielle.Setup(r => r.RechercherAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkDocumentaire>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat });
        generateur.Setup(g => g.GenererReponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Le RH de votre pôle crée votre fiche.");

        var reponse = await useCase.ExecuteAsync(actor, "Qui crée ma fiche collaborateur ?", null);

        reponse.Sourcee.Should().BeTrue();
        reponse.DocumentsSources.Should().Contain("01_politique_onboarding.md");
        reponse.Texte.Should().Be("Le RH de votre pôle crée votre fiche.");
    }

    [Fact]
    public async Task ExecuterAsync_QuestionDocumentaire_ScoreAuDessusDuSeuilMaisGenerateurRefuse_NePasSourcerLaReponse()
    {
        // Le score de reranking mesure la proximité thématique, pas "la réponse est présente"
        // (constaté empiriquement, milestone 9) : un chunk du bon sujet peut passer le seuil
        // sans que la question précise y soit vraiment traitée. Le générateur reste alors le
        // seul signal fiable — s'il rend la phrase de refus imposée par le prompt, la réponse
        // ne doit pas être annoncée comme sourcée malgré un candidat retenu.
        var (router, generateur, embedding, rechercheVectorielle, reranker, _, _, useCase) = CreerUseCase();
        var actor = CreerCollaborateurActeur(Guid.NewGuid());
        var candidat = new ChunkDocumentaire(Guid.NewGuid(), "01_politique_onboarding.md", 0, "Onboarding", "Contexte du bon sujet mais muet sur le detail precis demande.", Score: 0.75f);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.QuestionDocumentaire);
        embedding.Setup(e => e.GenererEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        rechercheVectorielle.Setup(r => r.RechercherAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkDocumentaire>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat });
        generateur.Setup(g => g.GenererReponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Je n'ai pas trouvé cette information sur ce point précis.");

        var reponse = await useCase.ExecuteAsync(actor, "Quel est le délai exact pour X ?", null);

        reponse.Sourcee.Should().BeFalse();
        reponse.DocumentsSources.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuterAsync_StatutDossier_CollaborateurSansFicheLiee_RetourneReponseDossierIntrouvable()
    {
        var (router, _, _, _, _, employees, _, useCase) = CreerUseCase();
        var actor = CreerCollaborateurActeur(Guid.NewGuid());
        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.StatutDossier);
        employees.Setup(c => c.GetByUserAccountIdAsync(actor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var reponse = await useCase.ExecuteAsync(actor, "Où en est mon onboarding ?", null);

        reponse.Sourcee.Should().BeFalse();
        reponse.Texte.Should().Contain("pas trouvé");
    }

    [Fact]
    public async Task ExecuterAsync_StatutDossier_CollaborateurAvecDossierEnCours_RapporteLeStatutEtLesItemsRestants()
    {
        var (router, _, _, _, _, employees, workflowInstances, useCase) = CreerUseCase();
        var accountId = Guid.NewGuid();
        var actor = CreerCollaborateurActeur(accountId);
        var departmentId = Guid.NewGuid();
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT001"), "Dupont", "Jean", "Dev", departmentId, ContractType.CDI, Maintenant);
        employee.LinkUserAccount(accountId);

        var item1 = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item 1");
        var item2 = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item 2");
        var instance = new WorkflowInstance(Guid.NewGuid(), employee.Id, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item1, item2 }, Maintenant);
        instance.Cocher(item1.Id, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.StatutDossier);
        employees.Setup(c => c.GetByUserAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        employees.Setup(c => c.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        workflowInstances.Setup(w => w.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instance);

        var reponse = await useCase.ExecuteAsync(actor, "Où en est mon onboarding ?", null);

        reponse.Sourcee.Should().BeFalse();
        reponse.Texte.Should().Contain("EnCours").And.Contain("1").And.Contain("2");
    }

    [Fact]
    public async Task ExecuterAsync_StatutDossier_RHCiblantCollaborateurDunAutrePole_LeveAccesRefuseException()
    {
        var (router, _, _, _, _, employees, _, useCase) = CreerUseCase();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var employeeAutreDepartement = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT002"), "Martin", "Léa", "Dev", Guid.NewGuid(), ContractType.CDI, Maintenant);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.StatutDossier);
        employees.Setup(c => c.GetByIdAsync(employeeAutreDepartement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeAutreDepartement);

        var act = () => useCase.ExecuteAsync(rh, "Où en est son onboarding ?", employeeAutreDepartement.Id);

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
    public async Task ExecuterEnStreamingAsync_IntentionHorsPerimetre_UnSeulFragmentPuisTermineNonSourcee()
    {
        var (router, generateur, _, _, _, _, _, useCase) = CreerUseCase();
        var actor = CreerCollaborateurActeur(Guid.NewGuid());
        router.Setup(r => r.ClassifierAsync("Quel temps fait-il ?", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.HorsPerimetre);

        var evenements = new List<EvenementConversation>();
        await foreach (var ev in useCase.ExecuterEnStreamingAsync(actor, "Quel temps fait-il ?", null))
            evenements.Add(ev);

        evenements.Should().HaveCount(2);
        evenements[0].Should().BeOfType<FragmentTexte>().Which.Texte.Should().Contain("RH");
        evenements[1].Should().BeOfType<ReponseTerminee>().Which.Sourcee.Should().BeFalse();
        generateur.Verify(g => g.GenererReponseEnStreamingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterEnStreamingAsync_QuestionDocumentaire_CandidatPertinent_StreamLesFragmentsPuisTermineSourcee()
    {
        var (router, generateur, embedding, rechercheVectorielle, reranker, _, _, useCase) = CreerUseCase();
        var actor = CreerCollaborateurActeur(Guid.NewGuid());
        var candidat = new ChunkDocumentaire(Guid.NewGuid(), "01_politique_onboarding.md", 0, "Onboarding", "Le RH crée la fiche.", Score: 0.9f);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.QuestionDocumentaire);
        embedding.Setup(e => e.GenererEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        rechercheVectorielle.Setup(r => r.RechercherAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkDocumentaire>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat });
        generateur.Setup(g => g.GenererReponseEnStreamingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(FragmentsAsync("Le RH ", "de votre pôle ", "crée votre fiche."));

        var evenements = new List<EvenementConversation>();
        await foreach (var ev in useCase.ExecuterEnStreamingAsync(actor, "Qui crée ma fiche collaborateur ?", null))
            evenements.Add(ev);

        var fragments = evenements.OfType<FragmentTexte>().ToList();
        fragments.Should().HaveCount(3);
        string.Concat(fragments.Select(f => f.Texte)).Should().Be("Le RH de votre pôle crée votre fiche.");

        var terminee = evenements.OfType<ReponseTerminee>().Should().ContainSingle().Which;
        terminee.Sourcee.Should().BeTrue();
        terminee.DocumentsSources.Should().Contain("01_politique_onboarding.md");
    }

    [Fact]
    public async Task ExecuterEnStreamingAsync_GenerateurRefuseMalgreCandidat_TermineNonSourceeSansSources()
    {
        var (router, generateur, embedding, rechercheVectorielle, reranker, _, _, useCase) = CreerUseCase();
        var actor = CreerCollaborateurActeur(Guid.NewGuid());
        var candidat = new ChunkDocumentaire(Guid.NewGuid(), "doc.md", 0, "Titre", "Contexte du bon sujet mais muet sur le detail.", Score: 0.75f);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.QuestionDocumentaire);
        embedding.Setup(e => e.GenererEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        rechercheVectorielle.Setup(r => r.RechercherAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkDocumentaire>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat });
        generateur.Setup(g => g.GenererReponseEnStreamingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(FragmentsAsync("Je n'ai pas trouvé cette information."));

        var evenements = new List<EvenementConversation>();
        await foreach (var ev in useCase.ExecuterEnStreamingAsync(actor, "Question précise hors du contexte fourni ?", null))
            evenements.Add(ev);

        var terminee = evenements.OfType<ReponseTerminee>().Should().ContainSingle().Which;
        terminee.Sourcee.Should().BeFalse();
        terminee.DocumentsSources.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuterAsync_StatutDossier_RHCiblantCollaborateurDeSonPole_EstAutorise()
    {
        var (router, _, _, _, _, employees, workflowInstances, useCase) = CreerUseCase();
        var departmentId = Guid.NewGuid();
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, departmentId, Maintenant);
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT003"), "Petit", "Sam", "Dev", departmentId, ContractType.CDI, Maintenant);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.StatutDossier);
        employees.Setup(c => c.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        workflowInstances.Setup(w => w.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowInstance?)null);
        workflowInstances.Setup(w => w.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowInstance?)null);

        var reponse = await useCase.ExecuteAsync(rh, "Où en est son dossier ?", employee.Id);

        reponse.Texte.Should().Contain("Aucun dossier");
    }
}
