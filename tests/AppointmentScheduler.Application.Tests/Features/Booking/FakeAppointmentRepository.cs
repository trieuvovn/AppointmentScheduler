using AppointmentScheduler.Application.Features.Booking;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Application.Tests.Features.Booking;

public sealed class FakeAppointmentRepository : IAppointmentRepository
{
    private readonly List<Appointment> _added = [];
    private readonly List<Appointment> _existing = [];
    private readonly Dictionary<Guid, Guid> _vehicleOwners = [];
    private readonly List<string> _calls = [];

    public IReadOnlyList<Appointment> Added => _added;

    public IReadOnlyList<string> Calls => _calls;

    public FakeAppointmentRepository WithVehicle(Guid vehicleId, Guid customerId)
    {
        _vehicleOwners[vehicleId] = customerId;
        return this;
    }

    public FakeAppointmentRepository WithExisting(Appointment appointment)
    {
        _existing.Add(appointment);
        return this;
    }

    public void Add(Appointment appointment)
    {
        _calls.Add(nameof(Add));
        _added.Add(appointment);
    }

    public Task<bool> HasOverlappingAppointmentAsync(Guid vehicleId, TimeSlot slot, CancellationToken ct)
    {
        _calls.Add(nameof(HasOverlappingAppointmentAsync));

        var overlaps = _existing.Any(a =>
            a.VehicleId == vehicleId && a.Occupies && a.Slot.Overlaps(slot));

        return Task.FromResult(overlaps);
    }

    public Task<bool> VehicleBelongsToCustomerAsync(Guid vehicleId, Guid customerId, CancellationToken ct)
    {
        var belongs = _vehicleOwners.TryGetValue(vehicleId, out var ownerId) && ownerId == customerId;
        return Task.FromResult(belongs);
    }

    public Task LockResourcesAsync(Guid serviceBayId, Guid technicianId, CancellationToken ct)
    {
        _calls.Add(nameof(LockResourcesAsync));
        return Task.CompletedTask;
    }
}
