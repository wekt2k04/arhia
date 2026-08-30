using Arhia.Domain;
using Arhia.Domain.Entities;
using FluentAssertions;

namespace Arhia.Tests.Domain;

public class TemplateItemTests
{
    [Fact]
    public void IsApplicableFor_NoCondition_IsApplicableToAllContractTypes()
    {
        var item = new TemplateItem(Guid.NewGuid(), "Compte SELFRH créé", 0);

        item.IsApplicableFor(ContractType.CDI).Should().BeTrue();
        item.IsApplicableFor(ContractType.Stage).Should().BeTrue();
    }

    [Fact]
    public void IsApplicableFor_WithCondition_IsApplicableOnlyToListedContractTypes()
    {
        var item = new TemplateItem(Guid.NewGuid(), "Processus disciplinaire signé", 0, new[] { ContractType.CDI, ContractType.CDD });

        item.IsApplicableFor(ContractType.CDI).Should().BeTrue();
        item.IsApplicableFor(ContractType.Stage).Should().BeFalse();
    }

    [Fact]
    public void Constructor_NegativeOrder_ThrowsArgumentException()
    {
        var act = () => new TemplateItem(Guid.NewGuid(), "Item", -1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyLabel_ThrowsArgumentException()
    {
        var act = () => new TemplateItem(Guid.NewGuid(), "  ", 0);

        act.Should().Throw<ArgumentException>();
    }
}
