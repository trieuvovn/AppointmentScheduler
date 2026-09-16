namespace AppointmentScheduler.Application.Common;

/// <summary>How far ahead a booking may be made. Bound from configuration via <see cref="SectionName"/>.</summary>
public sealed record BookingHorizon
{
    public const string SectionName = "Booking";

    /// <summary>How far ahead a booking must be made. Default zero: any future instant is bookable.</summary>
    public TimeSpan MinimumLeadTime { get; init; } = TimeSpan.Zero;

    /// <summary>How far ahead a booking may be made. Default ninety days.</summary>
    public TimeSpan MaximumHorizon { get; init; } = TimeSpan.FromDays(90);
}
