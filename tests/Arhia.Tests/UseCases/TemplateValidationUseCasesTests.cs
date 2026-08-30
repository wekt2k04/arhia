using System.Threading;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Arhia.Domain;
using Arhia.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Arhia.Tests.UseCases;

public class TemplateValidationUseCasesTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static IReadOnlyCollection<TemplateSection> SectionsMinimales() =>
        new[] { new TemplateSection(Guid.NewGuid(), "RH", 0, new[] { new TemplateItem(Guid.NewGuid(), "Compte SELFRH créé", 0) }) };

    [Fact]
    public async Task ProposeTemplateUseCase_HRActor_CreatesATemplateInReview()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var repo = new Mock<IWorkflowTemplateRepository>();
        var useCase = new ProposeTemplateUseCase(repo.Object);

        var template = await useCase.ExecuteAsync(rh, WorkflowType.Onboarding, "T0", SectionsMinimales(), Maintenant);

        template.Status.Should().Be(TemplateStatus.InReview);
        template.AuthorId.Should().Be(rh.Id);
        repo.Verify(r => r.AddAsync(template, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProposeTemplateUseCase_QualityAdminActor_ThrowsAccessDeniedException()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var repo = new Mock<IWorkflowTemplateRepository>();
        var useCase = new ProposeTemplateUseCase(repo.Object);

        var act = () => useCase.ExecuteAsync(admin, WorkflowType.Onboarding, "T0", SectionsMinimales(), Maintenant);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task VerifyTemplateUseCase_QualityAdminActor_VerifiesTheTemplate()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), SectionsMinimales(), Maintenant);
        template.Submit();
        var repo = new Mock<IWorkflowTemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var useCase = new VerifyTemplateUseCase(repo.Object);

        await useCase.ExecuteAsync(admin, template.Id);

        template.VerifierId.Should().Be(admin.Id);
        repo.Verify(r => r.UpdateAsync(template, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyTemplateUseCase_HRActor_ThrowsAccessDeniedException()
    {
        var rh = new UserAccount(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.HR, Guid.NewGuid(), Maintenant);
        var repo = new Mock<IWorkflowTemplateRepository>();
        var useCase = new VerifyTemplateUseCase(repo.Object);

        var act = () => useCase.ExecuteAsync(rh, Guid.NewGuid());

        await act.Should().ThrowAsync<AccessDeniedException>();
        repo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveTemplateUseCase_AfterVerification_ApprovesTheTemplate()
    {
        var verifier = new UserAccount(Guid.NewGuid(), "verif@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var approver = new UserAccount(Guid.NewGuid(), "approb@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), SectionsMinimales(), Maintenant);
        template.Submit();
        template.Verify(verifier.Id);
        var repo = new Mock<IWorkflowTemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var useCase = new ApproveTemplateUseCase(repo.Object);

        await useCase.ExecuteAsync(approver, template.Id);

        template.Status.Should().Be(TemplateStatus.Approved);
    }

    [Fact]
    public async Task RejectTemplateUseCase_QualityAdminActor_RejectsTheTemplate()
    {
        var admin = new UserAccount(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.QualityAdmin, null, Maintenant);
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), SectionsMinimales(), Maintenant);
        template.Submit();
        var repo = new Mock<IWorkflowTemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var useCase = new RejectTemplateUseCase(repo.Object);

        await useCase.ExecuteAsync(admin, template.Id, "Items incohérents");

        template.Status.Should().Be(TemplateStatus.Rejected);
    }
}
