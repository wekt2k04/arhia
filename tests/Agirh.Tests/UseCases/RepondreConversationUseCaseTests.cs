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
        Mock<ICollaborateurRepository> Collaborateurs,
        Mock<IWorkflowInstanceRepository> WorkflowInstances,
        RepondreConversationUseCase UseCase) CreerUseCase()
    {
        var router = new Mock<ILlmRouterPort>();
        var generateur = new Mock<ILlmGeneratorPort>();
        var embedding = new Mock<IEmbeddingPort>();
        var rechercheVectorielle = new Mock<IVectorSearchPort>();
        var reranker = new Mock<IRerankerPort>();
        var collaborateurs = new Mock<ICollaborateurRepository>();
        var workflowInstances = new Mock<IWorkflowInstanceRepository>();

        var useCase = new RepondreConversationUseCase(
            router.Object, generateur.Object, embedding.Object, rechercheVectorielle.Object,
            reranker.Object, collaborateurs.Object, workflowInstances.Object);

        return (router, generateur, embedding, rechercheVectorielle, reranker, collaborateurs, workflowInstances, useCase);
    }

    private static CompteUtilisateur CreerCollaborateurActeur(Guid id) =>
        new(id, "collab@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);

    [Fact]
    public async Task ExecuterAsync_QuestionVide_LeveArgumentException()
    {
        var (_, _, _, _, _, _, _, useCase) = CreerUseCase();
        var acteur = CreerCollaborateurActeur(Guid.NewGuid());

        var act = () => useCase.ExecuterAsync(acteur, "   ", null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuterAsync_IntentionHorsPerimetre_RetourneReponseCanneeSansAppelerLeGenerateur()
    {
        var (router, generateur, _, _, _, _, _, useCase) = CreerUseCase();
        var acteur = CreerCollaborateurActeur(Guid.NewGuid());
        router.Setup(r => r.ClassifierAsync("Quel temps fait-il ?", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.HorsPerimetre);

        var reponse = await useCase.ExecuterAsync(acteur, "Quel temps fait-il ?", null);

        reponse.Sourcee.Should().BeFalse();
        reponse.Texte.Should().Contain("RH");
        generateur.Verify(g => g.GenererReponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_QuestionDocumentaire_AucunCandidatTrouve_RefuseSansAppelerLeGenerateur()
    {
        var (router, generateur, embedding, rechercheVectorielle, _, _, _, useCase) = CreerUseCase();
        var acteur = CreerCollaborateurActeur(Guid.NewGuid());
        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.QuestionDocumentaire);
        embedding.Setup(e => e.GenererEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        rechercheVectorielle.Setup(r => r.RechercherAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChunkDocumentaire>());

        var reponse = await useCase.ExecuterAsync(acteur, "Question sans réponse dans le corpus", null);

        reponse.Sourcee.Should().BeFalse();
        reponse.Texte.Should().Contain("pas trouvé");
        generateur.Verify(g => g.GenererReponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_QuestionDocumentaire_ScoresSousLeSeuil_RefuseSansAppelerLeGenerateur()
    {
        var (router, generateur, embedding, rechercheVectorielle, reranker, _, _, useCase) = CreerUseCase();
        var acteur = CreerCollaborateurActeur(Guid.NewGuid());
        var candidat = new ChunkDocumentaire(Guid.NewGuid(), "doc.md", 0, "Titre", "Contenu peu pertinent", Score: 0f);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.QuestionDocumentaire);
        embedding.Setup(e => e.GenererEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        rechercheVectorielle.Setup(r => r.RechercherAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat });
        reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkDocumentaire>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidat with { Score = 0f } });

        var reponse = await useCase.ExecuterAsync(acteur, "Question hors sujet du corpus", null);

        reponse.Sourcee.Should().BeFalse();
        generateur.Verify(g => g.GenererReponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_QuestionDocumentaire_CandidatPertinent_AppelleLeGenerateurEtSourceLaReponse()
    {
        var (router, generateur, embedding, rechercheVectorielle, reranker, _, _, useCase) = CreerUseCase();
        var acteur = CreerCollaborateurActeur(Guid.NewGuid());
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

        var reponse = await useCase.ExecuterAsync(acteur, "Qui crée ma fiche collaborateur ?", null);

        reponse.Sourcee.Should().BeTrue();
        reponse.DocumentsSources.Should().Contain("01_politique_onboarding.md");
        reponse.Texte.Should().Be("Le RH de votre pôle crée votre fiche.");
    }

    [Fact]
    public async Task ExecuterAsync_StatutDossier_CollaborateurSansFicheLiee_RetourneReponseDossierIntrouvable()
    {
        var (router, _, _, _, _, collaborateurs, _, useCase) = CreerUseCase();
        var acteur = CreerCollaborateurActeur(Guid.NewGuid());
        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.StatutDossier);
        collaborateurs.Setup(c => c.ObtenirParCompteUtilisateurIdAsync(acteur.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collaborateur?)null);

        var reponse = await useCase.ExecuterAsync(acteur, "Où en est mon onboarding ?", null);

        reponse.Sourcee.Should().BeFalse();
        reponse.Texte.Should().Contain("pas trouvé");
    }

    [Fact]
    public async Task ExecuterAsync_StatutDossier_CollaborateurAvecDossierEnCours_RapporteLeStatutEtLesItemsRestants()
    {
        var (router, _, _, _, _, collaborateurs, workflowInstances, useCase) = CreerUseCase();
        var compteId = Guid.NewGuid();
        var acteur = CreerCollaborateurActeur(compteId);
        var poleId = Guid.NewGuid();
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant);
        collaborateur.LierCompte(compteId);

        var item1 = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item 1");
        var item2 = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item 2");
        var instance = new WorkflowInstance(Guid.NewGuid(), collaborateur.Id, Guid.NewGuid(), "T0", WorkflowType.Onboarding, new[] { item1, item2 }, Maintenant);
        instance.Cocher(item1.Id, ItemEtat.Ok, Guid.NewGuid(), Maintenant, null);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.StatutDossier);
        collaborateurs.Setup(c => c.ObtenirParCompteUtilisateurIdAsync(compteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collaborateur);
        collaborateurs.Setup(c => c.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collaborateur);
        workflowInstances.Setup(w => w.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instance);

        var reponse = await useCase.ExecuterAsync(acteur, "Où en est mon onboarding ?", null);

        reponse.Sourcee.Should().BeFalse();
        reponse.Texte.Should().Contain("EnCours").And.Contain("1").And.Contain("2");
    }

    [Fact]
    public async Task ExecuterAsync_StatutDossier_RHCiblantCollaborateurDunAutrePole_LeveAccesRefuseException()
    {
        var (router, _, _, _, _, collaborateurs, _, useCase) = CreerUseCase();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);
        var collaborateurAutrePole = new Collaborateur(Guid.NewGuid(), new Matricule("MAT002"), "Martin", "Léa", "Dev", Guid.NewGuid(), TypeContrat.CDI, Maintenant);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.StatutDossier);
        collaborateurs.Setup(c => c.ObtenirParIdAsync(collaborateurAutrePole.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collaborateurAutrePole);

        var act = () => useCase.ExecuterAsync(rh, "Où en est son onboarding ?", collaborateurAutrePole.Id);

        await act.Should().ThrowAsync<AccesRefuseException>();
    }

    [Fact]
    public async Task ExecuterAsync_StatutDossier_RHCiblantCollaborateurDeSonPole_EstAutorise()
    {
        var (router, _, _, _, _, collaborateurs, workflowInstances, useCase) = CreerUseCase();
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT003"), "Petit", "Sam", "Dev", poleId, TypeContrat.CDI, Maintenant);

        router.Setup(r => r.ClassifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentionConversation.StatutDossier);
        collaborateurs.Setup(c => c.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collaborateur);
        workflowInstances.Setup(w => w.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowInstance?)null);
        workflowInstances.Setup(w => w.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowInstance?)null);

        var reponse = await useCase.ExecuterAsync(rh, "Où en est son dossier ?", collaborateur.Id);

        reponse.Texte.Should().Contain("Aucun dossier");
    }
}
