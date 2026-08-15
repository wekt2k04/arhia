using Agirh.Domain;
using Agirh.Domain.Entities;
using FluentAssertions;

namespace Agirh.Tests.Domain;

public class CompteUtilisateurTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructeur_RoleRH_SansPole_LeveArgumentException()
    {
        var act = () => new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, null, Maintenant);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructeur_RoleRH_AvecPole_EstAccepte()
    {
        var poleId = Guid.NewGuid();

        var compte = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);

        compte.PoleId.Should().Be(poleId);
    }

    [Fact]
    public void Constructeur_RoleAdminQualite_AvecPole_LeveArgumentException()
    {
        var act = () => new CompteUtilisateur(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.AdminQualite, Guid.NewGuid(), Maintenant);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructeur_RoleCollaborateur_AvecPole_LeveArgumentException()
    {
        var act = () => new CompteUtilisateur(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Collaborateur, Guid.NewGuid(), Maintenant);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("pas-un-email")]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructeur_EmailInvalide_LeveArgumentException(string email)
    {
        var act = () => new CompteUtilisateur(Guid.NewGuid(), email, "hash", RoleType.AdminQualite, null, Maintenant);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ElevRole_DeCollaborateurVersRH_MetAJourRoleEtPole()
    {
        var compte = new CompteUtilisateur(Guid.NewGuid(), "user@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);
        var nouveauPole = Guid.NewGuid();

        compte.ElevRole(RoleType.RH, nouveauPole);

        compte.Role.Should().Be(RoleType.RH);
        compte.PoleId.Should().Be(nouveauPole);
    }

    [Fact]
    public void ElevRole_VersRHSansPole_LeveArgumentException()
    {
        var compte = new CompteUtilisateur(Guid.NewGuid(), "user@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);

        var act = () => compte.ElevRole(RoleType.RH, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Desactiver_PuisReactiver_BasculeEstActif()
    {
        var compte = new CompteUtilisateur(Guid.NewGuid(), "user@agirh.test", "hash", RoleType.AdminQualite, null, Maintenant);

        compte.Desactiver();
        compte.EstActif.Should().BeFalse();

        compte.Reactiver();
        compte.EstActif.Should().BeTrue();
    }
}
