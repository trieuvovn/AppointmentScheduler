namespace AppointmentScheduler.Domain.Appointments;

public enum BookingError
{
    /// <summary>The requested slot falls outside the dealership's opening hours for that day.</summary>
    OutsideOpeningHours = 1,

    /// <summary>The service would run past closing time.</summary>
    CrossesClosingTime = 2,

    /// <summary>Every bay is taken for the requested slot.</summary>
    NoServiceBayAvailable = 3,

    /// <summary>No technician holding every required skill is free for the requested slot.</summary>
    NoQualifiedTechnicianAvailable = 4,

    /// <summary>The vehicle already has an appointment overlapping the requested slot.</summary>
    VehicleAlreadyBooked = 5,

    /// <summary>The requested start is in the past.</summary>
    StartsInThePast = 6,

    /// <summary>The requested start is beyond the booking horizon.</summary>
    BeyondBookingHorizon = 7,

    /// <summary>The requested status change is not legal from the current status.</summary>
    InvalidStatusTransition = 8,
}
