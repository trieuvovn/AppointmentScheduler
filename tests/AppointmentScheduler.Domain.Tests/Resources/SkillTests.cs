using AppointmentScheduler.Domain.Resources;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests.Resources;

public class SkillTests
{
    [Fact]
    public void Create_ValidCodeAndName_KeepsTheCodeAndNameItWasGiven()
    {
        var skill = Skill.Create(Guid.NewGuid(), "DIAG", "Diagnostics");

        skill.Code.Should().Be("DIAG");
        skill.Name.Should().Be("Diagnostics");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingCode_ThrowsArgumentException(string? code)
    {
        var act = () => Skill.Create(Guid.NewGuid(), code!, "Diagnostics");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingName_ThrowsArgumentException(string? name)
    {
        var act = () => Skill.Create(Guid.NewGuid(), "DIAG", name!);

        act.Should().Throw<ArgumentException>();
    }
}
