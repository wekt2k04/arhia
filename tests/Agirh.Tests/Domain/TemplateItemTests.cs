using Agirh.Domain;
using Agirh.Domain.Entities;
using FluentAssertions;

namespace Agirh.Tests.Domain;

public class TemplateItemTests
{
    [Fact]
    public void ApplicablePour_SansCondition_EstApplicableATousLesContrats()
    {
        var item = new TemplateItem(Guid.NewGuid(), "Compte SELFRH créé", 0);

        item.ApplicablePour(TypeContrat.CDI).Should().BeTrue();
        item.ApplicablePour(TypeContrat.Stage).Should().BeTrue();
    }

    [Fact]
    public void ApplicablePour_AvecCondition_EstApplicableUniquementAuxContratsListes()
    {
        var item = new TemplateItem(Guid.NewGuid(), "Processus disciplinaire signé", 0, new[] { TypeContrat.CDI, TypeContrat.CDD });

        item.ApplicablePour(TypeContrat.CDI).Should().BeTrue();
        item.ApplicablePour(TypeContrat.Stage).Should().BeFalse();
    }

    [Fact]
    public void Constructeur_OrdreNegatif_LeveArgumentException()
    {
        var act = () => new TemplateItem(Guid.NewGuid(), "Item", -1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructeur_LibelleVide_LeveArgumentException()
    {
        var act = () => new TemplateItem(Guid.NewGuid(), "  ", 0);

        act.Should().Throw<ArgumentException>();
    }
}
