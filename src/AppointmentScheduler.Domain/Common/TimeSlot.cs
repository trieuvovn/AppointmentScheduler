namespace AppointmentScheduler.Domain.Common;

/// <summary>
/// A half-open interval <c>[Start, End)</c> on the timeline.
/// </summary>
public readonly record struct TimeSlot
{
    private TimeSlot(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan Duration => End - Start;

    /// <summary>Creates a slot, rejecting an end that does not follow its start.</summary>
    /// <exception cref="ArgumentException">The interval is empty or inverted.</exception>
    public static TimeSlot Create(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new ArgumentException(
                $"A time slot must end after it starts, but got start {start:O} and end {end:O}.",
                nameof(end));
        }

        return new TimeSlot(start, end);
    }

    /// <summary>Creates a slot of the given length, as a service type's duration does.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The duration is zero or negative.</exception>
    public static TimeSlot FromDuration(DateTimeOffset start, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration), duration, "A time slot must have a positive duration.");
        }

        return new TimeSlot(start, start + duration);
    }

    /// <summary>
    /// True when this slot and <paramref name="other"/> share any instant.
    /// </summary>
    public bool Overlaps(TimeSlot other) => Start < other.End && End > other.Start;

    /// <summary>True when this slot falls entirely inside <paramref name="window"/>, touching ends allowed.</summary>
    public bool IsWithin(TimeSlot window) => Start >= window.Start && End <= window.End;

    public override string ToString() => $"[{Start:O}, {End:O})";
}
