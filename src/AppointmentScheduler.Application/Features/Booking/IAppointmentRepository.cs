using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Application.Features.Booking;

public interface IAppointmentRepository
{
    /// <summary>Stages an insert. Does not save — the handler owns the transaction.</summary>
    void Add(Appointment appointment);

    Task<bool> HasOverlappingAppointmentAsync(Guid vehicleId, TimeSlot slot, CancellationToken ct);

    /// <summary>True when the vehicle exists and belongs to that customer — the C# half of FK_Appointments_Vehicle.</summary>
    Task<bool> VehicleBelongsToCustomerAsync(Guid vehicleId, Guid customerId, CancellationToken ct);

    /// <summary>Takes exclusive row locks on the bay and technician, in ascending id order (plan Stage 7).</summary>
    Task LockResourcesAsync(Guid serviceBayId, Guid technicianId, CancellationToken ct);
}
