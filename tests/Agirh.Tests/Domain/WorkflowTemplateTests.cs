using Agirh.Domain;
using Agirh.Domain.Entities;
using FluentAssertions;

namespace Agirh.Tests.Domain;

public class WorkflowTemplateTests
{
    private static readonly DateTime Maintenant = new(2026, 8, 15);

    private static WorkflowTemplate CreerBrouillon(Guid redacteurId) =>
        new(
            Guid.NewGuid(),
            WorkflowType.Onboarding,
            "T0",
            redacteurId,
            new[] { new TemplateSection(Guid.NewGuid(), "RH", 0, new[] { new TemplateItem(Guid.NewGuid(), "Compte SELFRH créé", 0) }) },
            Maintenant);

    [Fact]
    public void Constructeur_SansSections_LeveArgumentException()
    {
        var act = () => new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), Array.Empty<TemplateSection>(), Maintenant);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructeur_EtatInitial_EstBrouillon()
    {
        var template = CreerBrouillon(Guid.NewGuid());

        template.Statut.Should().Be(TemplateStatut.Brouillon);
    }

    [Fact]
    public void Verifier_SurBrouillon_LeveInvalidOperationException()
    {
        var template = CreerBrouillon(Guid.NewGuid());

        var act = () => template.Verifier(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CircuitComplet_Soumettre_Verifier_Approuver_AboutitAApprouve()
    {
        var redacteur = Guid.NewGuid();
        var verificateur = Guid.NewGuid();
        var approbateur = Guid.NewGuid();
        var template = CreerBrouillon(redacteur);

        template.Soumettre();
        template.Verifier(verificateur);
        template.Approuver(approbateur);

        template.Statut.Should().Be(TemplateStatut.Approuve);
        template.VerificateurId.Should().Be(verificateur);
        template.ApprobateurId.Should().Be(approbateur);
    }

    [Fact]
    public void Verifier_ParLeRedacteurLuiMeme_LeveInvalidOperationException()
    {
        var redacteur = Guid.NewGuid();
        var template = CreerBrouillon(redacteur);
        template.Soumettre();

        var act = () => template.Verifier(redacteur);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approuver_AvantVerification_LeveInvalidOperationException()
    {
        var template = CreerBrouillon(Guid.NewGuid());
        template.Soumettre();

        var act = () => template.Approuver(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approuver_ParLeVerificateurLuiMeme_LeveInvalidOperationException()
    {
        var verificateur = Guid.NewGuid();
        var template = CreerBrouillon(Guid.NewGuid());
        template.Soumettre();
        template.Verifier(verificateur);

        var act = () => template.Approuver(verificateur);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approuver_ParLeRedacteurLuiMeme_LeveInvalidOperationException()
    {
        var redacteur = Guid.NewGuid();
        var template = CreerBrouillon(redacteur);
        template.Soumettre();
        template.Verifier(Guid.NewGuid());

        var act = () => template.Approuver(redacteur);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Verifier_DejaVerifie_LeveInvalidOperationException()
    {
        var template = CreerBrouillon(Guid.NewGuid());
        template.Soumettre();
        template.Verifier(Guid.NewGuid());

        var act = () => template.Verifier(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rejeter_SansMotif_LeveArgumentException()
    {
        var template = CreerBrouillon(Guid.NewGuid());
        template.Soumettre();

        var act = () => template.Rejeter(Guid.NewGuid(), "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejeter_AvecMotif_PasseAuStatutRejete()
    {
        var template = CreerBrouillon(Guid.NewGuid());
        template.Soumettre();

        template.Rejeter(Guid.NewGuid(), "Item ambigu, à préciser");

        template.Statut.Should().Be(TemplateStatut.Rejete);
        template.MotifRejet.Should().Be("Item ambigu, à préciser");
    }

    [Fact]
    public void Approuver_SurTemplateRejete_LeveInvalidOperationException()
    {
        var template = CreerBrouillon(Guid.NewGuid());
        template.Soumettre();
        template.Rejeter(Guid.NewGuid(), "motif");

        var act = () => template.Approuver(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResoudreItemsApplicables_FiltreParTypeContrat()
    {
        var itemCommun = new TemplateItem(Guid.NewGuid(), "Bitlocker activé", 0);
        var itemCdiUniquement = new TemplateItem(Guid.NewGuid(), "Processus disciplinaire signé", 1, new[] { ContractType.CDI });
        var section = new TemplateSection(Guid.NewGuid(), "IT", 0, new[] { itemCommun, itemCdiUniquement });
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), new[] { section }, Maintenant);

        var itemsStage = template.ResoudreItemsApplicables(ContractType.Stage);

        itemsStage.Should().ContainSingle().Which.Should().Be(itemCommun);
    }
}
