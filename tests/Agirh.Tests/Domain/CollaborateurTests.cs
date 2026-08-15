using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using FluentAssertions;

namespace Agirh.Tests.Domain;

public class CollaborateurTests
{
    private static readonly DateTime DateIntegration = new(2026, 1, 15);

    private static Collaborateur CreerCollaborateur() =>
        new(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Développeur", Guid.NewGuid(), TypeContrat.CDI, DateIntegration);

    [Fact]
    public void Constructeur_PoleVide_LeveArgumentException()
    {
        var act = () => new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Développeur", Guid.Empty, TypeContrat.CDI, DateIntegration);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructeur_NomVide_LeveArgumentException(string nom)
    {
        var act = () => new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), nom, "Jean", "Développeur", Guid.NewGuid(), TypeContrat.CDI, DateIntegration);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EnregistrerDepart_DateApresIntegration_EstAcceptee()
    {
        var collaborateur = CreerCollaborateur();

        collaborateur.EnregistrerDepart(DateIntegration.AddYears(1));

        collaborateur.DateDepart.Should().Be(DateIntegration.AddYears(1));
    }

    [Fact]
    public void EnregistrerDepart_DateAvantIntegration_LeveInvalidOperationException()
    {
        var collaborateur = CreerCollaborateur();

        var act = () => collaborateur.EnregistrerDepart(DateIntegration.AddDays(-1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnregistrerDepart_MemeDateQueIntegration_EstAcceptee()
    {
        var collaborateur = CreerCollaborateur();

        var act = () => collaborateur.EnregistrerDepart(DateIntegration);

        act.Should().NotThrow();
    }

    [Fact]
    public void ChangerDePole_NouveauPoleValide_MetAJourPoleId()
    {
        var collaborateur = CreerCollaborateur();
        var nouveauPole = Guid.NewGuid();

        collaborateur.ChangerDePole(nouveauPole);

        collaborateur.PoleId.Should().Be(nouveauPole);
    }

    [Fact]
    public void ChangerDePole_PoleVide_LeveArgumentException()
    {
        var collaborateur = CreerCollaborateur();

        var act = () => collaborateur.ChangerDePole(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LierCompte_IdentifiantValide_MetAJourCompteUtilisateurId()
    {
        var collaborateur = CreerCollaborateur();
        var compteId = Guid.NewGuid();

        collaborateur.LierCompte(compteId);

        collaborateur.CompteUtilisateurId.Should().Be(compteId);
    }
}
