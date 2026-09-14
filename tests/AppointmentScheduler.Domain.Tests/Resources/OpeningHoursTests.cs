using AppointmentScheduler.Domain.Resources;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests.Resources;

public class OpeningHoursTests
{
    [Fact]
    public void Create_ValidDayAndTimes_KeepsTheDayAndTimesItWasGiven()
    {
        var hours = OpeningHours.Create(DayOfWeek.Friday, new TimeOnly(8, 30), new TimeOnly(17, 30));

        hours.DayOfWeek.Should().Be(DayOfWeek.Friday);
        hours.OpensAt.Should().Be(new TimeOnly(8, 30));
        hours.ClosesAt.Should().Be(new TimeOnly(17, 30));
    }

    [Fact]
    public void Create_ClosingBeforeOpening_ThrowsArgumentException()
    {
        var act = () => OpeningHours.Create(DayOfWeek.Friday, new TimeOnly(17, 0), new TimeOnly(8, 0));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_OpeningEqualToClosing_ThrowsArgumentException()
    {
        // Mirrors CK_OpeningHours_Order. A day that opens and closes at the same instant is closed,
        // and is expressed by recording no hours for it at all.
        var act = () => OpeningHours.Create(DayOfWeek.Friday, new TimeOnly(8, 0), new TimeOnly(8, 0));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equals_SameDayAndTimesOnBothInstances_ReturnsTrue()
    {
        var first = OpeningHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0));
        var second = OpeningHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0));

        first.Should().Be(second);
    }
}
