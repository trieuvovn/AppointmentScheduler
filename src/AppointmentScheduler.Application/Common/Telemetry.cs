using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AppointmentScheduler.Application.Common;

public static class Telemetry
{
    public const string SourceName = "AppointmentScheduler.Booking";

    public static readonly ActivitySource Source = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> BookingsConfirmed =
        Meter.CreateCounter<long>("bookings_confirmed_total");

    public static readonly Counter<long> BookingConflicts =
        Meter.CreateCounter<long>("booking_conflicts_total");
}
