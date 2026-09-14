using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Resources;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests.Resources;

public class ServiceBayTests
{
    [Fact]
    public void Create_ValidRequest_KeepsTheDetailsItWasGiven()
    {
        var dealershipId = Guid.NewGuid();

        var bay = ServiceBay.Create(Guid.NewGuid(), dealershipId, "BAY-1");

        bay.DealershipId.Should().Be(dealershipId);
        bay.Code.Should().Be("BAY-1");
        bay.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_IsActiveFalse_CreatesAnInactiveBay()
    {
        ServiceBay.Create(Guid.NewGuid(), Guid.NewGuid(), "BAY-1", isActive: false)
                  .IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingCode_ThrowsArgumentException(string? code)
    {
        var act = () => ServiceBay.Create(Guid.NewGuid(), Guid.NewGuid(), code!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Version_IncrementedThroughIVersioned_IsWritable()
    {
        // The booking guard needs to write this to force an UPDATE and take the row lock; a
        // store-generated rowversion could not serve (data model 3.5).
        var bay = ServiceBay.Create(Guid.NewGuid(), Guid.NewGuid(), "BAY-1");

        bay.Should().BeAssignableTo<IVersioned>();

        ((IVersioned)bay).Version++;

        bay.Version.Should().Be(1);
    }
}
