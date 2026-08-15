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

public class InstancierWorkflowUseCaseTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static WorkflowTemplate CreerTemplateApprouve(params TemplateItem[] items)
    {
        var section = new TemplateSection(Guid.NewGuid(), "RH", 0, items);
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), new[] { section }, Maintenant);
        template.Soumettre();
        template.Verifier(Guid.NewGuid());
        template.Approuver(Guid.NewGuid());
        return template;
    }

    [Fact]
    public async Task ExecuterAsync_RHSurSonPole_InstancieLeWorkflowAvecItemsFiltres()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.Stage, Maintenant);
        var itemCommun = new TemplateItem(Guid.NewGuid(), "Bitlocker activé", 0);
        var itemCdiSeulement = new TemplateItem(Guid.NewGuid(), "Processus disciplinaire signé", 1, new[] { TypeContrat.CDI });
        var template = CreerTemplateApprouve(itemCommun, itemCdiSeulement);

        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var templates = new Mock<IWorkflowTemplateRepository>();
        templates.Setup(r => r.ObtenirDernierApprouveAsync(WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var instances = new Mock<IWorkflowInstanceRepository>();

        var useCase = new InstancierWorkflowUseCase(collaborateurs.Object, templates.Object, instances.Object);

        var resultat = await useCase.ExecuterAsync(rh, collaborateur.Id, WorkflowType.Onboarding, Maintenant);

        resultat.Items.Should().ContainSingle(i => i.Libelle == itemCommun.Libelle);
        instances.Verify(r => r.AjouterAsync(It.IsAny<WorkflowInstance>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuterAsync_RHSurAutrePole_LeveAccesRefuseException()
    {
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), TypeContrat.CDI, Maintenant);

        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var templates = new Mock<IWorkflowTemplateRepository>();
        var instances = new Mock<IWorkflowInstanceRepository>();
        var useCase = new InstancierWorkflowUseCase(collaborateurs.Object, templates.Object, instances.Object);

        var act = () => useCase.ExecuterAsync(rh, collaborateur.Id, WorkflowType.Onboarding, Maintenant);

        await act.Should().ThrowAsync<AccesRefuseException>();
        instances.Verify(r => r.AjouterAsync(It.IsAny<WorkflowInstance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuterAsync_AucunTemplateApprouve_LeveInvalidOperationException()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant);

        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(collaborateur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(collaborateur);
        var templates = new Mock<IWorkflowTemplateRepository>();
        templates.Setup(r => r.ObtenirDernierApprouveAsync(WorkflowType.Onboarding, It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowTemplate?)null);
        var instances = new Mock<IWorkflowInstanceRepository>();
        var useCase = new InstancierWorkflowUseCase(collaborateurs.Object, templates.Object, instances.Object);

        var act = () => useCase.ExecuterAsync(rh, collaborateur.Id, WorkflowType.Onboarding, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuterAsync_CollaborateurInexistant_LeveInvalidOperationException()
    {
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);
        var collaborateurs = new Mock<ICollaborateurRepository>();
        collaborateurs.Setup(r => r.ObtenirParIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Collaborateur?)null);
        var templates = new Mock<IWorkflowTemplateRepository>();
        var instances = new Mock<IWorkflowInstanceRepository>();
        var useCase = new InstancierWorkflowUseCase(collaborateurs.Object, templates.Object, instances.Object);

        var act = () => useCase.ExecuterAsync(rh, Guid.NewGuid(), WorkflowType.Onboarding, Maintenant);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
