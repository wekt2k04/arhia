using System.Threading;
using Agirh.Core.Ports;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Agirh.Tests.UseCases;

public class ObtenirNotificationsUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static (
        Mock<ICollaborateurRepository> Collaborateurs,
        Mock<IWorkflowInstanceRepository> Instances,
        Mock<IWorkflowTemplateRepository> Templates,
        ObtenirNotificationsUseCase UseCase) CreerUseCase()
    {
        var collaborateurs = new Mock<ICollaborateurRepository>();
        var instances = new Mock<IWorkflowInstanceRepository>();
        var templates = new Mock<IWorkflowTemplateRepository>();
        var useCase = new ObtenirNotificationsUseCase(collaborateurs.Object, instances.Object, templates.Object);
        return (collaborateurs, instances, templates, useCase);
    }

    private static WorkflowInstance CreerInstanceEnCours(Guid collaborateurId, WorkflowType type, DateTime dateCreation)
    {
        var item = new ChecklistItemStatus(Guid.NewGuid(), Guid.NewGuid(), "Item");
        return new WorkflowInstance(Guid.NewGuid(), collaborateurId, Guid.NewGuid(), "T0", type, new[] { item }, dateCreation);
    }

    [Fact]
    public async Task ExecuterAsync_RH_ItemEnAttenteDepuisPlusDe3Jours_ProduitUneNotification()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant.AddDays(-10));
        var instance = CreerInstanceEnCours(collaborateur.Id, WorkflowType.Onboarding, Maintenant.AddDays(-4));

        var (collaborateurs, instances, _, useCase) = CreerUseCase();
        collaborateurs.Setup(c => c.ListerParPoleAsync(poleId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { collaborateur });
        instances.Setup(i => i.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        instances.Setup(i => i.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);

        var resultat = await useCase.ExecuterAsync(rh, Maintenant);

        resultat.Should().ContainSingle(n => n.Type == TypeNotification.ItemEnAttenteDepuisLongtemps);
    }

    [Fact]
    public async Task ExecuterAsync_RH_ItemEnAttenteDepuisMoinsDe3Jours_NeProduitRien()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant.AddDays(-1));
        var instance = CreerInstanceEnCours(collaborateur.Id, WorkflowType.Onboarding, Maintenant.AddDays(-1));

        var (collaborateurs, instances, _, useCase) = CreerUseCase();
        collaborateurs.Setup(c => c.ListerParPoleAsync(poleId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { collaborateur });
        instances.Setup(i => i.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        instances.Setup(i => i.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);

        var resultat = await useCase.ExecuterAsync(rh, Maintenant);

        resultat.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuterAsync_RH_EcheanceDepartDansMoinsDe3JoursSansOffboardingDemarre_ProduitUneNotification()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant.AddYears(-1));
        collaborateur.EnregistrerDepart(Maintenant.AddDays(2));

        var (collaborateurs, instances, _, useCase) = CreerUseCase();
        collaborateurs.Setup(c => c.ListerParPoleAsync(poleId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { collaborateur });
        instances.Setup(i => i.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);
        instances.Setup(i => i.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);

        var resultat = await useCase.ExecuterAsync(rh, Maintenant);

        resultat.Should().ContainSingle(n => n.Type == TypeNotification.EcheanceDepartApprochante);
    }

    [Fact]
    public async Task ExecuterAsync_RH_EcheanceDepartApprochanteMaisOffboardingDejaDemarre_NeProduitRien()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant.AddYears(-1));
        collaborateur.EnregistrerDepart(Maintenant.AddDays(2));
        var offboarding = CreerInstanceEnCours(collaborateur.Id, WorkflowType.Offboarding, Maintenant.AddDays(-1));

        var (collaborateurs, instances, _, useCase) = CreerUseCase();
        collaborateurs.Setup(c => c.ListerParPoleAsync(poleId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { collaborateur });
        instances.Setup(i => i.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowInstance?)null);
        instances.Setup(i => i.ObtenirParCollaborateurAsync(collaborateur.Id, WorkflowType.Offboarding, It.IsAny<CancellationToken>())).ReturnsAsync(offboarding);

        var resultat = await useCase.ExecuterAsync(rh, Maintenant);

        resultat.Should().NotContain(n => n.Type == TypeNotification.EcheanceDepartApprochante);
    }

    [Fact]
    public async Task ExecuterAsync_Collaborateur_RetourneListeVide()
    {
        var collaborateurActeur = new CompteUtilisateur(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);
        var (_, _, _, useCase) = CreerUseCase();

        var resultat = await useCase.ExecuterAsync(collaborateurActeur, Maintenant);

        resultat.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuterAsync_AdminQualite_TemplateEnValidation_ProduitUneNotification()
    {
        var admin = new CompteUtilisateur(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.AdminQualite, null, Maintenant);
        var section = new TemplateSection(Guid.NewGuid(), "Section", 0, new[] { new TemplateItem(Guid.NewGuid(), "Item", 0) });
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T1", Guid.NewGuid(), new[] { section }, Maintenant);
        template.Soumettre();

        var (_, _, templates, useCase) = CreerUseCase();
        templates.Setup(t => t.ListerParStatutAsync(TemplateStatut.EnValidation, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { template });

        var resultat = await useCase.ExecuterAsync(admin, Maintenant);

        resultat.Should().ContainSingle(n => n.Type == TypeNotification.TemplateEnAttenteValidation);
    }

    [Fact]
    public async Task ExecuterAsync_AdminQualite_AucunTemplateEnValidation_RetourneListeVide()
    {
        var admin = new CompteUtilisateur(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.AdminQualite, null, Maintenant);
        var (_, _, templates, useCase) = CreerUseCase();
        templates.Setup(t => t.ListerParStatutAsync(TemplateStatut.EnValidation, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<WorkflowTemplate>());

        var resultat = await useCase.ExecuterAsync(admin, Maintenant);

        resultat.Should().BeEmpty();
    }
}
