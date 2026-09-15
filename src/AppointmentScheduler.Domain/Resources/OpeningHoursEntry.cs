using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Resources;

/// <summary>
/// One <c>DealershipOpeningHours</c> row: a day's hours, as a reference type.
/// </summary>
public sealed class OpeningHoursEntry
{
    public OpeningHoursEntry(OpeningHours hours)
    {
        DayOfWeek = hours.DayOfWeek;
        OpensAt = hours.OpensAt;
        ClosesAt = hours.ClosesAt;
    }

    /// <summary>Rehydration constructor for EF Core.</summary>
    private OpeningHoursEntry()
    {
    }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly OpensAt { get; private set; }

    public TimeOnly ClosesAt { get; private set; }

    /// <summary>The value object this row carries.</summary>
    public OpeningHours ToOpeningHours() => OpeningHours.Create(DayOfWeek, OpensAt, ClosesAt);
}
