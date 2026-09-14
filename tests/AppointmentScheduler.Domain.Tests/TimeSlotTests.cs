using AppointmentScheduler.Domain.Common;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests;

public class TimeSlotTests
{
    // A fixed reference point: these tests are about interval arithmetic, not about "now".
    private static readonly DateTimeOffset Noon = new(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);

    private static TimeSlot Slot(int startHour, int endHour) =>
        TimeSlot.Create(Noon.AddHours(startHour), Noon.AddHours(endHour));

    /// <summary>
    /// The overlap truth table. The reference slot is [12:00, 14:00) in every row.
    /// </summary>
    [Theory]
    // description                       otherStart  otherEnd  expected
    [InlineData("entirely before",        8,          10,       false)]
    [InlineData("touching at the front",  10,         12,       false)]
    [InlineData("overlapping the front",  11,         13,       true)]
    [InlineData("contained",              12,         13,       true)]
    [InlineData("identical",              12,         14,       true)]
    [InlineData("overlapping the tail",   13,         15,       true)]
    [InlineData("enveloping",             11,         15,       true)]
    [InlineData("touching at the tail",   14,         16,       false)]
    [InlineData("entirely after",         16,         18,       false)]
    public void Overlaps_VariousPositionsRelativeToReferenceSlot_MatchesTheOverlapTruthTable(
        string description, int otherStart, int otherEnd, bool expected)
    {
        var reference = Slot(0, 2);
        var other = Slot(otherStart - 12, otherEnd - 12);

        reference.Overlaps(other).Should().Be(expected, because: description);
    }

    [Theory]
    [InlineData(8, 10)]
    [InlineData(10, 12)]
    [InlineData(11, 13)]
    [InlineData(12, 13)]
    [InlineData(13, 15)]
    [InlineData(11, 15)]
    [InlineData(14, 16)]
    public void Overlaps_CalledFromEitherSlot_ReturnsTheSameResult(int otherStart, int otherEnd)
    {
        var reference = Slot(0, 2);
        var other = Slot(otherStart - 12, otherEnd - 12);

        // Whichever side asks, the answer is the same — a one-sided overlap rule would let a
        // booking through depending only on which appointment the query started from.
        reference.Overlaps(other).Should().Be(other.Overlaps(reference));
    }

    [Fact]
    public void Overlaps_SameSlotComparedToItself_ReturnsTrue()
    {
        var slot = Slot(0, 2);

        slot.Overlaps(slot).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_SlotsThatTouchAtTheBoundary_ReturnsFalseSoBackToBackBookingsAreLegal()
    {
        // Called out separately from the theory: this single assertion is what protects
        // against writing >= in the predicate, which would reject every adjacent booking.
        var morning = Slot(0, 2);
        var afternoon = Slot(2, 4);

        morning.Overlaps(afternoon).Should().BeFalse();
        afternoon.Overlaps(morning).Should().BeFalse();
    }

    [Theory]
    // description                        start  end   expected
    [InlineData("strictly inside",         1,     2,    true)]
    [InlineData("flush with both ends",    0,     4,    true)]
    [InlineData("flush with the opening",  0,     2,    true)]
    [InlineData("flush with the closing",  2,     4,    true)]
    [InlineData("starts before opening",  -1,     2,    false)]
    [InlineData("runs past closing",       2,     5,    false)]
    [InlineData("envelops the window",    -1,     5,    false)]
    [InlineData("entirely outside",        5,     6,    false)]
    public void IsWithin_VariousPositionsRelativeToTheWindow_MatchesTheContainmentTruthTable(
        string description, int start, int end, bool expected)
    {
        var window = Slot(0, 4);
        var slot = Slot(start, end);

        slot.IsWithin(window).Should().Be(expected, because: description);
    }

    [Fact]
    public void Create_ValidStartAndEnd_ExposesTheIntervalItWasGiven()
    {
        var slot = TimeSlot.Create(Noon, Noon.AddHours(2));

        slot.Start.Should().Be(Noon);
        slot.End.Should().Be(Noon.AddHours(2));
        slot.Duration.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void Create_EndBeforeStart_ThrowsArgumentException()
    {
        var act = () => TimeSlot.Create(Noon, Noon.AddHours(-1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyInterval_ThrowsArgumentException()
    {
        // A zero-length appointment would occupy nothing and overlap nothing.
        var act = () => TimeSlot.Create(Noon, Noon);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromDuration_PositiveDuration_DerivesTheEndFromTheLength()
    {
        var slot = TimeSlot.FromDuration(Noon, TimeSpan.FromMinutes(90));

        slot.End.Should().Be(Noon.AddMinutes(90));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void FromDuration_NonPositiveDuration_ThrowsArgumentOutOfRangeException(int minutes)
    {
        var act = () => TimeSlot.FromDuration(Noon, TimeSpan.FromMinutes(minutes));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Equals_SameBoundsOnBothSlots_ReturnsTrue()
    {
        // A record struct: equality is by value, which is what lets tests and handlers
        // compare slots directly.
        Slot(0, 2).Should().Be(Slot(0, 2));
    }

    [Fact]
    public void Equals_DifferentBounds_ReturnsFalse()
    {
        Slot(0, 2).Should().NotBe(Slot(0, 3));
    }

    [Fact]
    public void Equals_SameInstantExpressedInADifferentOffset_ReturnsTrue()
    {
        // DateTimeOffset compares by instant, so a slot expressed in +07:00 equals the
        // identical instant expressed in UTC. Storage is UTC by convention (data model §3.6).
        var utc = TimeSlot.Create(Noon, Noon.AddHours(1));
        var offset = TimeSlot.Create(
            Noon.ToOffset(TimeSpan.FromHours(7)),
            Noon.AddHours(1).ToOffset(TimeSpan.FromHours(7)));

        utc.Should().Be(offset);
    }
}
