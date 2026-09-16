using AppointmentScheduler.Domain.Appointments;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests.Appointments;

public class BookingRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan MinimumLeadTime = TimeSpan.Zero;
    private static readonly TimeSpan MaximumHorizon = TimeSpan.FromDays(90);

    [Fact]
    public void CanBeBookedAt_ExactlyAtNow_IsAllowed()
    {
        var error = BookingRules.CanBeBookedAt(Now, Now, MinimumLeadTime, MaximumHorizon);

        error.Should().BeNull();
    }

    [Fact]
    public void CanBeBookedAt_OneTickBeforeNow_IsStartsInThePast()
    {
        var error = BookingRules.CanBeBookedAt(Now.AddTicks(-1), Now, MinimumLeadTime, MaximumHorizon);

        error.Should().Be(BookingError.StartsInThePast);
    }

    [Fact]
    public void CanBeBookedAt_ExactlyAtTheHorizon_IsAllowed()
    {
        var startsAt = Now + MaximumHorizon;

        var error = BookingRules.CanBeBookedAt(startsAt, Now, MinimumLeadTime, MaximumHorizon);

        error.Should().BeNull();
    }

    [Fact]
    public void CanBeBookedAt_OneTickPastTheHorizon_IsBeyondBookingHorizon()
    {
        var startsAt = Now + MaximumHorizon + TimeSpan.FromTicks(1);

        var error = BookingRules.CanBeBookedAt(startsAt, Now, MinimumLeadTime, MaximumHorizon);

        error.Should().Be(BookingError.BeyondBookingHorizon);
    }

    [Fact]
    public void CanBeBookedAt_BeforeAMinimumLeadTime_IsStartsInThePast()
    {
        var leadTime = TimeSpan.FromHours(1);
        var startsAt = Now + leadTime - TimeSpan.FromTicks(1);

        var error = BookingRules.CanBeBookedAt(startsAt, Now, leadTime, MaximumHorizon);

        error.Should().Be(BookingError.StartsInThePast);
    }

    [Fact]
    public void CanBeBookedAt_ExactlyAtAMinimumLeadTime_IsAllowed()
    {
        var leadTime = TimeSpan.FromHours(1);
        var startsAt = Now + leadTime;

        var error = BookingRules.CanBeBookedAt(startsAt, Now, leadTime, MaximumHorizon);

        error.Should().BeNull();
    }
}
