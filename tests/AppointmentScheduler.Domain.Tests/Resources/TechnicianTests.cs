using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Resources;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests.Resources;

public class TechnicianTests
{
    private static readonly Skill Diagnostics = Skill.Create(Guid.NewGuid(), "DIAG", "Diagnostics");
    private static readonly Skill Electrical = Skill.Create(Guid.NewGuid(), "ELEC", "Electrical");

    private static Technician Holding(params Skill[] skills) =>
        Technician.Create(Guid.NewGuid(), Guid.NewGuid(), "Sam Rivera", skills: skills);

    private static ServiceType Requiring(params Skill[] skills) =>
        ServiceType.Create(Guid.NewGuid(), "SVC", "Service", 60, requiredSkills: skills);

    [Fact]
    public void IsQualifiedFor_TechnicianHoldingEveryRequiredSkill_ReturnsTrue()
    {
        Holding(Diagnostics, Electrical)
            .IsQualifiedFor(Requiring(Diagnostics, Electrical)).Should().BeTrue();
    }

    [Fact]
    public void IsQualifiedFor_TechnicianOverQualified_ReturnsTrue()
    {
        Holding(Diagnostics, Electrical)
            .IsQualifiedFor(Requiring(Diagnostics)).Should().BeTrue();
    }

    [Fact]
    public void IsQualifiedFor_RequiredSkillMissing_ReturnsFalse()
    {
        // Requirement 1.5: the whole required set must be covered, not merely intersected.
        Holding(Diagnostics)
            .IsQualifiedFor(Requiring(Diagnostics, Electrical)).Should().BeFalse();
    }

    [Fact]
    public void IsQualifiedFor_ServiceRequiringNoSkills_ReturnsTrue()
    {
        Holding().IsQualifiedFor(Requiring()).Should().BeTrue();
    }

    [Fact]
    public void IsQualifiedFor_NullServiceType_ThrowsArgumentNullException()
    {
        var act = () => Holding(Diagnostics).IsQualifiedFor(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Skills_DuplicateSkillsGivenAtCreation_AreDeduplicated()
    {
        Holding(Diagnostics, Diagnostics).Skills.Should().ContainSingle();
    }

    [Fact]
    public void Skills_NoSkillsGivenAtCreation_DefaultsToEmpty()
    {
        Technician.Create(Guid.NewGuid(), Guid.NewGuid(), "Sam Rivera")
                  .Skills.Should().BeEmpty();
    }

    [Fact]
    public void Create_ValidRequest_DefaultsToActiveWithAnUnsetVersion()
    {
        var technician = Holding();

        technician.IsActive.Should().BeTrue();
        technician.Version.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingFullName_ThrowsArgumentException(string? fullName)
    {
        var act = () => Technician.Create(Guid.NewGuid(), Guid.NewGuid(), fullName!);

        act.Should().Throw<ArgumentException>();
    }
}
