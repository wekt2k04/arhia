using Agirh.Domain.ValueObjects;
using FluentAssertions;

namespace Agirh.Tests.Domain;

public class MatriculeTests
{
    [Fact]
    public void Constructeur_ValeurValide_TrimEtConserve()
    {
        var matricule = new Matricule("  MAT-042  ");

        matricule.Valeur.Should().Be("MAT-042");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructeur_ValeurVideOuNulle_LeveArgumentException(string? valeur)
    {
        var act = () => new Matricule(valeur!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructeur_PlusDe20Caracteres_LeveArgumentException()
    {
        var act = () => new Matricule(new string('A', 21));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructeur_Exactement20Caracteres_EstAccepte()
    {
        var valeur = new string('A', 20);

        var matricule = new Matricule(valeur);

        matricule.Valeur.Should().Be(valeur);
    }

    [Fact]
    public void Egalite_MemeValeur_SontEgales()
    {
        var a = new Matricule("MAT001");
        var b = new Matricule("MAT001");

        a.Should().Be(b);
    }
}
