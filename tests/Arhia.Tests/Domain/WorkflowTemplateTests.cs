using Arhia.Domain;
using Arhia.Domain.Entities;
using FluentAssertions;

namespace Arhia.Tests.Domain;

public class WorkflowTemplateTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static WorkflowTemplate CreateDraft(Guid authorId) =>
        new(
            Guid.NewGuid(),
            WorkflowType.Onboarding,
            "T0",
            authorId,
            new[] { new TemplateSection(Guid.NewGuid(), "RH", 0, new[] { new TemplateItem(Guid.NewGuid(), "Compte SELFRH créé", 0) }) },
            Maintenant);

    [Fact]
    public void Constructor_NoSections_ThrowsArgumentException()
    {
        var act = () => new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), Array.Empty<TemplateSection>(), Maintenant);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_InitialStatus_IsDraft()
    {
        var template = CreateDraft(Guid.NewGuid());

        template.Status.Should().Be(TemplateStatus.Draft);
    }

    [Fact]
    public void Verify_OnDraft_ThrowsInvalidOperationException()
    {
        var template = CreateDraft(Guid.NewGuid());

        var act = () => template.Verify(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FullCircuit_Submit_Verify_Approve_EndsUpApproved()
    {
        var author = Guid.NewGuid();
        var verifier = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var template = CreateDraft(author);

        template.Submit();
        template.Verify(verifier);
        template.Approve(approver);

        template.Status.Should().Be(TemplateStatus.Approved);
        template.VerifierId.Should().Be(verifier);
        template.ApproverId.Should().Be(approver);
    }

    [Fact]
    public void Verify_ByTheAuthorThemself_ThrowsInvalidOperationException()
    {
        var author = Guid.NewGuid();
        var template = CreateDraft(author);
        template.Submit();

        var act = () => template.Verify(author);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_BeforeVerification_ThrowsInvalidOperationException()
    {
        var template = CreateDraft(Guid.NewGuid());
        template.Submit();

        var act = () => template.Approve(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_ByTheVerifierThemself_ThrowsInvalidOperationException()
    {
        var verifier = Guid.NewGuid();
        var template = CreateDraft(Guid.NewGuid());
        template.Submit();
        template.Verify(verifier);

        var act = () => template.Approve(verifier);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_ByTheAuthorThemself_ThrowsInvalidOperationException()
    {
        var author = Guid.NewGuid();
        var template = CreateDraft(author);
        template.Submit();
        template.Verify(Guid.NewGuid());

        var act = () => template.Approve(author);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Verify_AlreadyVerified_ThrowsInvalidOperationException()
    {
        var template = CreateDraft(Guid.NewGuid());
        template.Submit();
        template.Verify(Guid.NewGuid());

        var act = () => template.Verify(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reject_WithoutReason_ThrowsArgumentException()
    {
        var template = CreateDraft(Guid.NewGuid());
        template.Submit();

        var act = () => template.Reject(Guid.NewGuid(), "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reject_WithReason_MovesToRejectedStatus()
    {
        var template = CreateDraft(Guid.NewGuid());
        template.Submit();

        template.Reject(Guid.NewGuid(), "Item ambigu, à préciser");

        template.Status.Should().Be(TemplateStatus.Rejected);
        template.RejectionReason.Should().Be("Item ambigu, à préciser");
    }

    [Fact]
    public void Approve_OnRejectedTemplate_ThrowsInvalidOperationException()
    {
        var template = CreateDraft(Guid.NewGuid());
        template.Submit();
        template.Reject(Guid.NewGuid(), "motif");

        var act = () => template.Approve(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveApplicableItems_FiltersByContractType()
    {
        var itemCommun = new TemplateItem(Guid.NewGuid(), "Bitlocker activé", 0);
        var itemCdiUniquement = new TemplateItem(Guid.NewGuid(), "Processus disciplinaire signé", 1, new[] { ContractType.CDI });
        var section = new TemplateSection(Guid.NewGuid(), "IT", 0, new[] { itemCommun, itemCdiUniquement });
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), new[] { section }, Maintenant);

        var itemsStage = template.ResolveApplicableItems(ContractType.Stage);

        itemsStage.Should().ContainSingle().Which.Should().Be(itemCommun);
    }
}
