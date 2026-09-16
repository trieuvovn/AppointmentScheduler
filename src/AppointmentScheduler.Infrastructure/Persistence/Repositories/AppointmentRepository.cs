using AppointmentScheduler.Application.Features.Booking;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IAppointmentRepository" />
internal sealed class AppointmentRepository : IAppointmentRepository
{
    private readonly AppointmentDbContext _db;

    public AppointmentRepository(AppointmentDbContext db) => _db = db;

    public void Add(Appointment appointment) => _db.Appointments.Add(appointment);

    public Task<bool> HasOverlappingAppointmentAsync(Guid vehicleId, TimeSlot slot, CancellationToken ct) =>
        _db.Appointments.Occupying().OverlappingWith(slot).AnyAsync(a => a.VehicleId == vehicleId, ct);

    public Task<bool> VehicleBelongsToCustomerAsync(Guid vehicleId, Guid customerId, CancellationToken ct) =>
        _db.Vehicles.AnyAsync(v => v.Id == vehicleId && v.CustomerId == customerId, ct);

    public async Task LockResourcesAsync(Guid serviceBayId, Guid technicianId, CancellationToken ct)
    {
        // Ascending id order, so two concurrent transactions can never lock them in opposite
        // directions and deadlock. Marking Modified with no property change still bumps Version
        // via AppointmentDbContext.SaveChangesAsync, which is what issues the deliberate UPDATE and
        // takes the exclusive row lock.
        Guid firstId, secondId;
        bool firstIsBay;

        if (serviceBayId.CompareTo(technicianId) <= 0)
        {
            (firstId, secondId, firstIsBay) = (serviceBayId, technicianId, true);
        }
        else
        {
            (firstId, secondId, firstIsBay) = (technicianId, serviceBayId, false);
        }

        await MarkModifiedAsync(firstId, firstIsBay, ct);
        await MarkModifiedAsync(secondId, !firstIsBay, ct);

        await _db.SaveChangesAsync(ct);
    }

    private async Task MarkModifiedAsync(Guid id, bool isBay, CancellationToken ct)
    {
        if (isBay)
        {
            var bay = await _db.ServiceBays.SingleAsync(b => b.Id == id, ct);
            _db.Entry(bay).State = EntityState.Modified;
        }
        else
        {
            var technician = await _db.Technicians.SingleAsync(t => t.Id == id, ct);
            _db.Entry(technician).State = EntityState.Modified;
        }
    }
}
