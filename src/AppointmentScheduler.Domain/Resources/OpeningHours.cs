namespace AppointmentScheduler.Domain.Resources;

/// <summary>
/// The opening and closing wall-clock times for one day of the week at one dealership.
/// </summary>
public readonly record struct OpeningHours
{
    private OpeningHours(DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        DayOfWeek = dayOfWeek;
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    public DayOfWeek DayOfWeek { get; }

    public TimeOnly OpensAt { get; }

    public TimeOnly ClosesAt { get; }

    /// <exception cref="ArgumentException">Closing is not after opening.</exception>
    public static OpeningHours Create(DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        if (closesAt <= opensAt)
        {
            // Mirrors CK_OpeningHours_Order. A dealership open past midnight is out of scope.
            throw new ArgumentException(
                $"Closing time {closesAt} must be after opening time {opensAt} on {dayOfWeek}.",
                nameof(closesAt));
        }

        return new OpeningHours(dayOfWeek, opensAt, closesAt);
    }
}
