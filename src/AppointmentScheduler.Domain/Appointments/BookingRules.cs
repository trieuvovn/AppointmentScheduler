namespace AppointmentScheduler.Domain.Appointments;

/// <summary>Rules comparing a requested start against the present. Pure — no config, no provider.</summary>
public static class BookingRules
{
    /// <summary>
    /// Rejects a start that is too soon (<paramref name="minimumLeadTime"/>) or too far out
    /// (<paramref name="maximumHorizon"/>). Null means "no objection".
    /// </summary>
    public static BookingError? CanBeBookedAt(
        DateTimeOffset startsAt, DateTimeOffset now, TimeSpan minimumLeadTime, TimeSpan maximumHorizon)
    {
        if (startsAt < now + minimumLeadTime)
        {
            return BookingError.StartsInThePast;
        }

        if (startsAt > now + maximumHorizon)
        {
            return BookingError.BeyondBookingHorizon;
        }

        return null;
    }
}
