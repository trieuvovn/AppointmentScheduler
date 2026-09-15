using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Resources;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests.Catalogue;

public class ServiceTypeTests
{
    private static readonly Skill Diagnostics = Skill.Create(Guid.NewGuid(), "DIAG", "Diagnostics");
    private static readonly Skill Electrical = Skill.Create(Guid.NewGuid(), "ELEC", "Electrical");
    private static readonly Skill Bodywork = Skill.Create(Guid.NewGuid(), "BODY", "Bodywork");

    private static ServiceType Requiring(params Skill[] skills) =>
        ServiceType.Create(Guid.NewGuid(), "SVC", "Service", 60, requiredSkills: skills);

    [Fact]
    public void Duration_ServiceTypeCreatedWithNinetyMinutes_IsExposedAsATimeSpan()
    {
        var serviceType = ServiceType.Create(Guid.NewGuid(), "SVC", "Service", 90);

        serviceType.Duration.Should().Be(TimeSpan.FromMinutes(90));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void Create_ZeroOrNegativeDuration_ThrowsArgumentOutOfRangeException(int minutes)
    {
        var act = () => ServiceType.Create(Guid.NewGuid(), "SVC", "Service", minutes);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_DurationBeyondTheCatalogueMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Mirrors CK_ServiceTypes_Duration, so the domain refuses what the database would.
        var act = () => ServiceType.Create(
            Guid.NewGuid(), "SVC", "Service", ServiceType.MaximumDurationMinutes + 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_DurationExactlyAtTheCatalogueMaximum_DoesNotThrow()
    {
        var act = () => ServiceType.Create(
            Guid.NewGuid(), "SVC", "Service", ServiceType.MaximumDurationMinutes);

        act.Should().NotThrow();
    }

    [Fact]
    public void IsSatisfiedBy_ServiceRequiringNoSkills_ReturnsTrueForAnyTechnician()
    {
        Requiring().IsSatisfiedBy([]).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_TechnicianHoldingExactlyTheRequiredSkills_ReturnsTrue()
    {
        Requiring(Diagnostics, Electrical)
            .IsSatisfiedBy([Diagnostics, Electrical]).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_TechnicianHoldingMoreThanRequired_ReturnsTrue()
    {
        // A superset qualifies: extra skills never disqualify anyone.
        Requiring(Diagnostics)
            .IsSatisfiedBy([Diagnostics, Electrical, Bodywork]).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_OneRequiredSkillMissing_ReturnsFalse()
    {
        Requiring(Diagnostics, Electrical)
            .IsSatisfiedBy([Diagnostics]).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_OnlyUnrelatedSkillsHeld_ReturnsFalse()
    {
        Requiring(Diagnostics).IsSatisfiedBy([Bodywork]).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_TechnicianWithNoSkills_ReturnsFalse()
    {
        Requiring(Diagnostics).IsSatisfiedBy([]).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_NullSkillSet_ThrowsArgumentNullException()
    {
        var act = () => Requiring(Diagnostics).IsSatisfiedBy(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequiredSkills_DuplicateSkillsGivenAtCreation_AreDeduplicated()
    {
        var serviceType = Requiring(Diagnostics, Diagnostics, Electrical);

        serviceType.RequiredSkills.Should().BeEquivalentTo([Diagnostics, Electrical]);
    }

    [Fact]
    public void RequiredSkills_NoSkillsGivenAtCreation_DefaultsToEmpty()
    {
        ServiceType.Create(Guid.NewGuid(), "SVC", "Service", 60)
                   .RequiredSkills.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingCode_ThrowsArgumentException(string? code)
    {
        var act = () => ServiceType.Create(Guid.NewGuid(), code!, "Service", 60);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingName_ThrowsArgumentException(string? name)
    {
        var act = () => ServiceType.Create(Guid.NewGuid(), "SVC", name!, 60);

        act.Should().Throw<ArgumentException>();
    }
}
