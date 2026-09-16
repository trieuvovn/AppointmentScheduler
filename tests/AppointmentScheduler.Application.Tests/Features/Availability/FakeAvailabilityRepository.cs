using AppointmentScheduler.Application.Features.Availability;
using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Resources;

namespace AppointmentScheduler.Application.Tests.Features.Availability;

public sealed class FakeAvailabilityRepository : IAvailabilityRepository
{
    private readonly List<Dealership> _dealerships = [];
    private readonly List<ServiceType> _serviceTypes = [];
    private readonly List<ServiceBay> _bays = [];
    private readonly List<Technician> _technicians = [];
    private readonly List<(Guid ServiceBayId, Guid TechnicianId, TimeSlot Slot)> _occupied = [];

    public FakeAvailabilityRepository AddDealership(Dealership dealership)
    {
        _dealerships.Add(dealership);
        return this;
    }

    public FakeAvailabilityRepository AddServiceType(ServiceType serviceType)
    {
        _serviceTypes.Add(serviceType);
        return this;
    }

    public FakeAvailabilityRepository AddBay(ServiceBay bay)
    {
        _bays.Add(bay);
        return this;
    }

    public FakeAvailabilityRepository AddTechnician(Technician technician)
    {
        _technicians.Add(technician);
        return this;
    }

    /// <summary>Occupies a bay and technician for the given slot, as a booked appointment would.</summary>
    public FakeAvailabilityRepository Occupy(Guid serviceBayId, Guid technicianId, TimeSlot slot)
    {
        _occupied.Add((serviceBayId, technicianId, slot));
        return this;
    }

    public Task<Dealership?> FindDealershipAsync(Guid dealershipId, CancellationToken ct) =>
        Task.FromResult(_dealerships.SingleOrDefault(d => d.Id == dealershipId));

    public Task<ServiceType?> FindServiceTypeAsync(Guid serviceTypeId, CancellationToken ct) =>
        Task.FromResult(_serviceTypes.SingleOrDefault(s => s.Id == serviceTypeId));

    public Task<IReadOnlyList<ResourceOccupancy>> GetBayOccupancyAsync(
        Guid dealershipId, TimeSlot window, CancellationToken ct)
    {
        IReadOnlyList<ResourceOccupancy> result = _bays
            .Where(b => b.DealershipId == dealershipId && b.IsActive)
            .OrderBy(b => b.Code)
            .ThenBy(b => b.Id)
            .Select(b => new ResourceOccupancy(
                b.Id,
                SkillCount: 0,
                Busy: BusyIntervalsForBay(b.Id, window)))
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ResourceOccupancy>> GetQualifiedTechnicianOccupancyAsync(
        Guid dealershipId, Guid serviceTypeId, TimeSlot window, CancellationToken ct)
    {
        var serviceType = _serviceTypes.Single(s => s.Id == serviceTypeId);

        IReadOnlyList<ResourceOccupancy> result = _technicians
            .Where(t => t.DealershipId == dealershipId && t.IsActive && t.IsQualifiedFor(serviceType))
            .OrderBy(t => t.Skills.Count)
            .ThenBy(t => t.Id)
            .Select(t => new ResourceOccupancy(
                t.Id,
                t.Skills.Count,
                Busy: BusyIntervalsForTechnician(t.Id, window)))
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ServiceBay>> FindFreeBaysAsync(
        Guid dealershipId, TimeSlot slot, CancellationToken ct)
    {
        var busy = _occupied.Where(o => o.Slot.Overlaps(slot)).Select(o => o.ServiceBayId).ToHashSet();

        IReadOnlyList<ServiceBay> result = _bays
            .Where(b => b.DealershipId == dealershipId && b.IsActive && !busy.Contains(b.Id))
            .OrderBy(b => b.Code)
            .ThenBy(b => b.Id)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Technician>> FindFreeQualifiedTechniciansAsync(
        Guid dealershipId, Guid serviceTypeId, TimeSlot slot, CancellationToken ct)
    {
        var serviceType = _serviceTypes.Single(s => s.Id == serviceTypeId);
        var busy = _occupied.Where(o => o.Slot.Overlaps(slot)).Select(o => o.TechnicianId).ToHashSet();

        IReadOnlyList<Technician> result = _technicians
            .Where(t => t.DealershipId == dealershipId && t.IsActive
                && t.IsQualifiedFor(serviceType) && !busy.Contains(t.Id))
            .OrderBy(t => t.Skills.Count)
            .ThenBy(t => t.Id)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<bool> IsStillFreeAsync(
        Guid serviceBayId, Guid technicianId, TimeSlot slot, CancellationToken ct)
    {
        var stillFree = !_occupied.Any(o =>
            (o.ServiceBayId == serviceBayId || o.TechnicianId == technicianId) && o.Slot.Overlaps(slot));

        return Task.FromResult(stillFree);
    }

    private List<TimeSlot> BusyIntervalsForBay(Guid bayId, TimeSlot window) =>
        _occupied
            .Where(o => o.ServiceBayId == bayId && o.Slot.Overlaps(window))
            .Select(o => o.Slot)
            .ToList();

    private List<TimeSlot> BusyIntervalsForTechnician(Guid technicianId, TimeSlot window) =>
        _occupied
            .Where(o => o.TechnicianId == technicianId && o.Slot.Overlaps(window))
            .Select(o => o.Slot)
            .ToList();
}
