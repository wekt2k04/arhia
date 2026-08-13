using Agirh.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Agirh.Tests.Unit;

/// <summary>
/// Behavior tests for <see cref="PreFlightValidator"/> — validation C# des
/// paramètres avant dispatch (R4 checklist, R5 pose de congés).
/// </summary>
public class PreFlightValidatorTests
{
    private readonly PreFlightValidator _validator = new();

    [Fact]
    public void Should_Reject_Unknown_Checklist_Category()
    {
        // Given une catégorie hors liste fermée (Administratif, IT, RH, Management)
        var result = _validator.ValidateParameters("GenererChecklistAsync",
            new Dictionary<string, string?> { ["category"] = "stagiaire" });

        // Then la clarification liste les catégories disponibles (R4)
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("catégorie inconnue");
        result.ErrorMessage.Should().Contain("Administratif");
    }

    [Fact]
    public void Should_Accept_Valid_Checklist_Category()
    {
        var result = _validator.ValidateParameters("GenererChecklistAsync",
            new Dictionary<string, string?> { ["category"] = "IT" });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Accept_Checklist_Without_Category()
    {
        var result = _validator.ValidateParameters("GenererChecklistAsync", new Dictionary<string, string?>());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Reject_LeavePose_Without_Start_Date()
    {
        // Given une pose de congés sans date de début
        var result = _validator.ValidateParameters("PoserDemandeCongesAsync",
            new Dictionary<string, string?> { ["days"] = "2" });

        // Then le pipeline demande la date (jamais un historique vide)
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("date de début");
    }

    [Fact]
    public void Should_Reject_LeavePose_Without_Days()
    {
        var result = _validator.ValidateParameters("PoserDemandeCongesAsync",
            new Dictionary<string, string?> { ["date_reference"] = "15/08/2026" });
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("nombre de jours");
    }

    [Fact]
    public void Should_Accept_LeavePose_With_Date_And_Days()
    {
        var result = _validator.ValidateParameters("PoserDemandeCongesAsync",
            new Dictionary<string, string?> { ["date_reference"] = "15/08/2026", ["days"] = "2" });
        result.IsValid.Should().BeTrue();
    }
}
