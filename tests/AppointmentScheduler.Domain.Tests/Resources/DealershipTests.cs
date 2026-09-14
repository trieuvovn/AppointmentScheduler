using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Resources;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests.Resources;

public class DealershipTests
{
    // A zone with a large positive offset, so any accidental UTC-vs-local confusion shows up as a
    // seven-hour error rather than hiding behind a small one.
    private static readonly TimeZoneInfo Saigon = FindTimeZone("Asia/Ho_Chi_Minh", "SE Asia Standard Time");

    // A zone that observes DST, for the transition cases.
    private static readonly TimeZoneInfo London = FindTimeZone("Europe/London", "GMT Standard Time");

    private static readonly DateOnly Monday = new(2026, 3, 16);
    private static readonly DateOnly Sunday = new(2026, 3, 15);

    private static TimeZoneInfo FindTimeZone(string ianaId, string windowsId)
    {
        // .NET on Windows accepts IANA ids from .NET 6 onward, but fall back so the suite does not
        // depend on the ICU data being present.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }

    private static Dealership OpenWeekdays(TimeZoneInfo? zone = null) =>
        Dealership.Create(
            Guid.NewGuid(),
            "Central Motors",
            (zone ?? Saigon).Id,
            [
                OpeningHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                OpeningHours.Create(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
            ]);

    private static TimeSlot LocalSlot(DateOnly date, int startHour, int durationMinutes, TimeZoneInfo zone)
    {
        var localStart = date.ToDateTime(new TimeOnly(startHour, 0), DateTimeKind.Unspecified);
        var start = new DateTimeOffset(localStart, zone.GetUtcOffset(localStart));

        return TimeSlot.FromDuration(start, TimeSpan.FromMinutes(durationMinutes));
    }

    [Fact]
    public void OpeningWindowOn_DayWithHoursRecorded_ResolvesLocalHoursAgainstTheDealershipTimeZone()
    {
        // 08:00 local in a +07:00 zone is 01:00 UTC the same day.
        var window = OpenWeekdays().OpeningWindowOn(Monday, Saigon);

        window.Should().NotBeNull();
        window!.Value.Start.UtcDateTime.Should().Be(new DateTime(2026, 3, 16, 1, 0, 0));
        window.Value.End.UtcDateTime.Should().Be(new DateTime(2026, 3, 16, 10, 0, 0));
    }

    [Fact]
    public void OpeningWindowOn_DayWithNoHoursRecorded_ReturnsNull()
    {
        OpenWeekdays().OpeningWindowOn(Sunday, Saigon).Should().BeNull();
    }

    [Fact]
    public void OpeningWindowOn_DayWithHoursRecorded_SpansTheFullBusinessDay()
    {
        var window = OpenWeekdays().OpeningWindowOn(Monday, Saigon);

        window!.Value.Duration.Should().Be(TimeSpan.FromHours(9));
    }

    [Fact]
    public void OpeningWindowOn_NullTimeZone_ThrowsArgumentNullException()
    {
        var act = () => OpenWeekdays().OpeningWindowOn(Monday, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsWithinOpeningHours_SlotInsideTheWorkingDay_ReturnsTrue()
    {
        var slot = LocalSlot(Monday, 10, 60, Saigon);

        OpenWeekdays().IsWithinOpeningHours(slot, Monday, Saigon).Should().BeTrue();
    }

    [Fact]
    public void IsWithinOpeningHours_SlotStartingExactlyAtOpening_ReturnsTrue()
    {
        var slot = LocalSlot(Monday, 8, 60, Saigon);

        OpenWeekdays().IsWithinOpeningHours(slot, Monday, Saigon).Should().BeTrue();
    }

    [Fact]
    public void IsWithinOpeningHours_SlotEndingExactlyAtClosing_ReturnsTrue()
    {
        var slot = LocalSlot(Monday, 16, 60, Saigon);

        OpenWeekdays().IsWithinOpeningHours(slot, Monday, Saigon).Should().BeTrue();
    }

    [Fact]
    public void IsWithinOpeningHours_SlotStartingBeforeOpening_ReturnsFalse()
    {
        var slot = LocalSlot(Monday, 7, 60, Saigon);

        OpenWeekdays().IsWithinOpeningHours(slot, Monday, Saigon).Should().BeFalse();
    }

    [Fact]
    public void IsWithinOpeningHours_ServiceThatCrossesClosingTime_ReturnsFalse()
    {
        // Starts inside the day but runs past 17:00 — the rejection the plan calls out for
        // Stage 3, decided here in the domain.
        var slot = LocalSlot(Monday, 16, 90, Saigon);

        OpenWeekdays().IsWithinOpeningHours(slot, Monday, Saigon).Should().BeFalse();
    }

    [Fact]
    public void IsWithinOpeningHours_AnySlotOnAClosedDay_ReturnsFalse()
    {
        var slot = LocalSlot(Sunday, 10, 60, Saigon);

        OpenWeekdays().IsWithinOpeningHours(slot, Sunday, Saigon).Should().BeFalse();
    }

    [Fact]
    public void IsWithinOpeningHours_SlotWhoseUtcHourLooksOutOfHours_IsDecidedInLocalTimeNotUtc()
    {
        // 10:00 local in Saigon is 03:00 UTC. Judged as a UTC wall clock it would fall before
        // an 08:00 opening and be rejected; judged locally it is mid-morning and accepted.
        var slot = LocalSlot(Monday, 10, 60, Saigon);

        slot.Start.UtcDateTime.Hour.Should().Be(3, because: "the test needs the UTC hour to be misleading");
        OpenWeekdays().IsWithinOpeningHours(slot, Monday, Saigon).Should().BeTrue();
    }

    [Fact]
    public void OpeningWindowOn_DatesEitherSideOfADstChange_FollowsTheOffsetInForceOnEachDate()
    {
        // London opens at 08:00 local all year. In January that is 08:00 UTC; in July, 07:00 UTC.
        // Both must be accepted, which only holds if the offset is read per date.
        var dealership = Dealership.Create(
            Guid.NewGuid(),
            "London Motors",
            London.Id,
            [OpeningHours.Create(DayOfWeek.Wednesday, new TimeOnly(8, 0), new TimeOnly(17, 0))]);

        var winter = new DateOnly(2026, 1, 14);
        var summer = new DateOnly(2026, 7, 15);

        var winterWindow = dealership.OpeningWindowOn(winter, London)!.Value;
        var summerWindow = dealership.OpeningWindowOn(summer, London)!.Value;

        winterWindow.Start.UtcDateTime.Hour.Should().Be(8);
        summerWindow.Start.UtcDateTime.Hour.Should().Be(7);
    }

    [Fact]
    public void HoursOn_DayWithHoursRecorded_ReturnsThem()
    {
        OpenWeekdays().HoursOn(DayOfWeek.Monday).Should().NotBeNull();
    }

    [Fact]
    public void HoursOn_DayWithNoHoursRecorded_ReturnsNull()
    {
        OpenWeekdays().HoursOn(DayOfWeek.Sunday).Should().BeNull();
    }

    [Fact]
    public void SetOpeningHours_CalledTwiceForTheSameDay_ReplacesRatherThanAccumulates()
    {
        var dealership = OpenWeekdays();

        dealership.SetOpeningHours(
            OpeningHours.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)));

        dealership.HoursOn(DayOfWeek.Monday)!.Value.ClosesAt.Should().Be(new TimeOnly(13, 0));
        dealership.OpeningHours.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingName_ThrowsArgumentException(string? name)
    {
        var act = () => Dealership.Create(Guid.NewGuid(), name!, "UTC");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingTimeZoneId_ThrowsArgumentException(string? timeZoneId)
    {
        var act = () => Dealership.Create(Guid.NewGuid(), "Central Motors", timeZoneId!);

        act.Should().Throw<ArgumentException>();
    }
}
