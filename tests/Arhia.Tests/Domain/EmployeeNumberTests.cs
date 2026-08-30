using Arhia.Domain.ValueObjects;
using FluentAssertions;

namespace Arhia.Tests.Domain;

public class EmployeeNumberTests
{
    [Fact]
    public void Constructor_ValidValue_TrimsAndStores()
    {
        var employeeNumber = new EmployeeNumber("  MAT-042  ");

        employeeNumber.Value.Should().Be("MAT-042");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyOrNullValue_ThrowsArgumentException(string? value)
    {
        var act = () => new EmployeeNumber(value!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_MoreThan20Characters_ThrowsArgumentException()
    {
        var act = () => new EmployeeNumber(new string('A', 21));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Exactly20Characters_IsAccepted()
    {
        var value = new string('A', 20);

        var employeeNumber = new EmployeeNumber(value);

        employeeNumber.Value.Should().Be(value);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new EmployeeNumber("MAT001");
        var b = new EmployeeNumber("MAT001");

        a.Should().Be(b);
    }
}
