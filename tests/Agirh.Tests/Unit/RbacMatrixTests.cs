using Agirh.Core.Models;
using FluentAssertions;
using Xunit;

namespace Agirh.Tests.Unit;

/// <summary>
/// Behavior tests for <see cref="RbacMatrix.Default"/> — la table de routage
/// intention → outil (R2/R5).
/// </summary>
public class RbacMatrixTests
{
    [Fact]
    public void Should_Resolve_Every_Intention_To_Expected_Tool()
    {
        // When/Then — chaque intention est mappée sur son outil métier
        RbacMatrix.Default.ResolveTool("LeaveBalance").Should().Be("ConsulterSoldeAsync");
        RbacMatrix.Default.ResolveTool("LeaveRequest").Should().Be("PoserDemandeCongesAsync");
        RbacMatrix.Default.ResolveTool("PayrollSettlement").Should().Be("GenererSoldeToutCompteAsync");
        RbacMatrix.Default.ResolveTool("OnboardingChecklist").Should().Be("GenererChecklistAsync");
        RbacMatrix.Default.ResolveTool("KnowledgeSearch").Should().Be("RechercherInformationRagAsync");
        RbacMatrix.Default.ResolveTool("ITAccessRevocation").Should().Be("RevoquerAccesITAsync");
        RbacMatrix.Default.ResolveTool("ManagerAlert").Should().Be("EnvoyerAlerteManagerAsync");
        RbacMatrix.Default.ResolveTool("LeaveApproval").Should().Be("ApprouverDemandeCongesAsync");
        RbacMatrix.Default.ResolveTool("SalaryAdvance").Should().Be("DemanderAvanceSalaireAsync");
    }

    [Fact]
    public void Should_Not_Route_LeaveRequest_To_Read_Only_History_Tool()
    {
        // Given l'intention "poser un congé"
        // Then elle n'est PLUS routée vers l'outil de consultation d'historique (R5)
        RbacMatrix.Default.ResolveTool("LeaveRequest").Should().NotBe("ConsulterHistoriqueCongesAsync");
    }

    [Fact]
    public void Should_Return_Null_For_Unmapped_Intentions()
    {
        // When/Then — les intentions non routables (bavardage, inconnu) → null (GeneralChat)
        RbacMatrix.Default.ResolveTool("GeneralInquiry").Should().BeNull();
        RbacMatrix.Default.ResolveTool("Unknown").Should().BeNull();
    }
}
