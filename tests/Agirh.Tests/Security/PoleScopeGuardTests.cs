using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using FluentAssertions;

namespace Agirh.Tests.Security;

public class PoleScopeGuardTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    [Fact]
    public void PeutAccederAuPole_AdminQualite_ToujoursAutorise()
    {
        var admin = new CompteUtilisateur(Guid.NewGuid(), "admin@agirh.test", "hash", RoleType.AdminQualite, null, Maintenant);

        PoleScopeGuard.PeutAccederAuPole(admin, Guid.NewGuid()).Should().BeTrue();
    }

    [Fact]
    public void PeutAccederAuPole_RHSurSonProprePole_EstAutorise()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);

        PoleScopeGuard.PeutAccederAuPole(rh, poleId).Should().BeTrue();
    }

    [Fact]
    public void PeutAccederAuPole_RHSurUnAutrePole_EstRefuse()
    {
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);

        PoleScopeGuard.PeutAccederAuPole(rh, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void PeutAccederAuCollaborateur_RHMemePole_EstAutorise()
    {
        var poleId = Guid.NewGuid();
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, poleId, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", poleId, TypeContrat.CDI, Maintenant);

        PoleScopeGuard.PeutAccederAuCollaborateur(rh, collaborateur).Should().BeTrue();
    }

    [Fact]
    public void PeutAccederAuCollaborateur_RHAutrePole_EstRefuse()
    {
        var rh = new CompteUtilisateur(Guid.NewGuid(), "rh@agirh.test", "hash", RoleType.RH, Guid.NewGuid(), Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), TypeContrat.CDI, Maintenant);

        PoleScopeGuard.PeutAccederAuCollaborateur(rh, collaborateur).Should().BeFalse();
    }

    [Fact]
    public void PeutAccederAuCollaborateur_CollaborateurSurSonPropreDossier_EstAutorise()
    {
        var compteId = Guid.NewGuid();
        var acteur = new CompteUtilisateur(compteId, "collab@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), TypeContrat.CDI, Maintenant);
        collaborateur.LierCompte(compteId);

        PoleScopeGuard.PeutAccederAuCollaborateur(acteur, collaborateur).Should().BeTrue();
    }

    [Fact]
    public void PeutAccederAuCollaborateur_CollaborateurSurDossierDUnAutre_EstRefuse()
    {
        var acteur = new CompteUtilisateur(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), TypeContrat.CDI, Maintenant);
        collaborateur.LierCompte(Guid.NewGuid());

        PoleScopeGuard.PeutAccederAuCollaborateur(acteur, collaborateur).Should().BeFalse();
    }

    [Fact]
    public void PeutAccederAuCollaborateur_CollaborateurSansCompteLie_EstRefuse()
    {
        var acteur = new CompteUtilisateur(Guid.NewGuid(), "collab@agirh.test", "hash", RoleType.Collaborateur, null, Maintenant);
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT001"), "Dupont", "Jean", "Dev", Guid.NewGuid(), TypeContrat.CDI, Maintenant);

        PoleScopeGuard.PeutAccederAuCollaborateur(acteur, collaborateur).Should().BeFalse();
    }
}
