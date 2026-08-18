using System.Threading;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Agirh.Tests.UseCases;

public class TemplateValidationUseCasesTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static IReadOnlyCollection<TemplateSection> SectionsMinimales() =>
        new[] { new TemplateSection(Guid.NewGuid(), "RH", 0, new[] { new TemplateItem(Guid.NewGuid(), "Compte SELFRH créé", 0) }) };

    [Fact]
    public async Task ProposerTemplateUseCase_ActeurRH_CreeUnTemplateEnValidation()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var repo = new Mock<IWorkflowTemplateRepository>();
        var useCase = new ProposerTemplateUseCase(repo.Object);

        var template = await useCase.ExecuteAsync(rh, WorkflowType.Onboarding, "T0", SectionsMinimales(), Maintenant);

        template.Statut.Should().Be(TemplateStatut.EnValidation);
        template.RedacteurId.Should().Be(rh.Id);
        repo.Verify(r => r.AjouterAsync(template, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProposerTemplateUseCase_ActeurAdminQualite_LeveAccesRefuseException()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var repo = new Mock<IWorkflowTemplateRepository>();
        var useCase = new ProposerTemplateUseCase(repo.Object);

        var act = () => useCase.ExecuteAsync(admin, WorkflowType.Onboarding, "T0", SectionsMinimales(), Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task VerifierTemplateUseCase_ActeurAdminQualite_VerifieLeTemplate()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), SectionsMinimales(), Maintenant);
        template.Soumettre();
        var repo = new Mock<IWorkflowTemplateRepository>();
        repo.Setup(r => r.ObtenirParIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var useCase = new VerifierTemplateUseCase(repo.Object);

        await useCase.ExecuteAsync(admin, template.Id);

        template.VerificateurId.Should().Be(admin.Id);
        repo.Verify(r => r.MettreAJourAsync(template, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifierTemplateUseCase_ActeurRH_LeveAccesRefuseException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var repo = new Mock<IWorkflowTemplateRepository>();
        var useCase = new VerifierTemplateUseCase(repo.Object);

        var act = () => useCase.ExecuteAsync(rh, Guid.NewGuid());

        await act.Should().ThrowAsync<AccessDeniedException>();
        repo.Verify(r => r.ObtenirParIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApprouverTemplateUseCase_ApresVerification_ApprouveLeTemplate()
    {
        var verificateur = new UserAccount(Guid.NewGuid(), "verif@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var approbateur = new UserAccount(Guid.NewGuid(), "approb@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), SectionsMinimales(), Maintenant);
        template.Soumettre();
        template.Verifier(verificateur.Id);
        var repo = new Mock<IWorkflowTemplateRepository>();
        repo.Setup(r => r.ObtenirParIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var useCase = new ApprouverTemplateUseCase(repo.Object);

        await useCase.ExecuteAsync(approbateur, template.Id);

        template.Statut.Should().Be(TemplateStatut.Approuve);
    }

    [Fact]
    public async Task RejeterTemplateUseCase_ActeurAdminQualite_RejetteLeTemplate()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), SectionsMinimales(), Maintenant);
        template.Soumettre();
        var repo = new Mock<IWorkflowTemplateRepository>();
        repo.Setup(r => r.ObtenirParIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var useCase = new RejeterTemplateUseCase(repo.Object);

        await useCase.ExecuteAsync(admin, template.Id, "Items incohérents");

        template.Statut.Should().Be(TemplateStatut.Rejete);
    }
}
