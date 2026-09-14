using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Resources;

/// <summary>
/// A dealership location: the tenant, its time zone, and its business calendar.
/// </summary>
public sealed class Dealership
{
    private readonly Dictionary<DayOfWeek, OpeningHours> _openingHours = [];

    private Dealership(Guid id, string name, string timeZoneId)
    {
        Id = id;
        Name = name;
        TimeZoneId = timeZoneId;
    }

    /// <summary>Rehydration constructor for EF Core.</summary>
    private Dealership()
    {
        Name = string.Empty;
        TimeZoneId = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>An IANA time zone id, for example <c>Asia/Ho_Chi_Minh</c>.</summary>
    public string TimeZoneId { get; private set; }

    public IReadOnlyCollection<OpeningHours> OpeningHours => _openingHours.Values;

    public static Dealership Create(
        Guid id,
        string name,
        string timeZoneId,
        IEnumerable<OpeningHours>? openingHours = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        var dealership = new Dealership(id, name, timeZoneId);

        foreach (var hours in openingHours ?? [])
        {
            dealership.SetOpeningHours(hours);
        }

        return dealership;
    }

    /// <summary>Sets the hours for a day, replacing any already recorded for that day.</summary>
    public void SetOpeningHours(OpeningHours hours) => _openingHours[hours.DayOfWeek] = hours;

    /// <summary>The hours for <paramref name="dayOfWeek"/>, or null when closed that day.</summary>
    public OpeningHours? HoursOn(DayOfWeek dayOfWeek) =>
        _openingHours.TryGetValue(dayOfWeek, out var hours) ? hours : null;

    /// <summary>
    /// The opening window for <paramref name="localDate"/> as an absolute interval, or null when
    /// the dealership is closed that day.
    /// </summary>
    public TimeSlot? OpeningWindowOn(DateOnly localDate, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var hours = HoursOn(localDate.DayOfWeek);

        if (hours is null)
        {
            return null;
        }

        var opens = ToInstant(localDate, hours.Value.OpensAt, timeZone);
        var closes = ToInstant(localDate, hours.Value.ClosesAt, timeZone);

        return TimeSlot.Create(opens, closes);
    }

    /// <summary>True when <paramref name="slot"/> fits entirely inside that day's opening window.</summary>
    public bool IsWithinOpeningHours(TimeSlot slot, DateOnly localDate, TimeZoneInfo timeZone) =>
        OpeningWindowOn(localDate, timeZone) is { } window && slot.IsWithin(window);

    private static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        // TimeZoneInfo resolves a time that a DST spring-forward skipped to the following instant,
        // and an ambiguous autumn time to standard offset. Both are acceptable here: opening hours
        // are business times, not instants a customer picks.
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
    }
}
