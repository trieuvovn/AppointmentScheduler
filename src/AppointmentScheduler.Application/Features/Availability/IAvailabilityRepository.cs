using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Resources;

namespace AppointmentScheduler.Application.Features.Availability;

public interface IAvailabilityRepository
{
    Task<Dealership?> FindDealershipAsync(Guid dealershipId, CancellationToken ct);

    Task<ServiceType?> FindServiceTypeAsync(Guid serviceTypeId, CancellationToken ct);

    /// <summary>Every active bay at the dealership, with its busy intervals inside <paramref name="window"/>, ordered by the selection policy.</summary>
    Task<IReadOnlyList<ResourceOccupancy>> GetBayOccupancyAsync(
        Guid dealershipId, TimeSlot window, CancellationToken ct);

    /// <summary>Every active technician holding every skill the service requires, with busy intervals inside <paramref name="window"/>, ordered by the selection policy.</summary>
    Task<IReadOnlyList<ResourceOccupancy>> GetQualifiedTechnicianOccupancyAsync(
        Guid dealershipId, Guid serviceTypeId, TimeSlot window, CancellationToken ct);

    Task<IReadOnlyList<ServiceBay>> FindFreeBaysAsync(
        Guid dealershipId, TimeSlot slot, CancellationToken ct);

    Task<IReadOnlyList<Technician>> FindFreeQualifiedTechniciansAsync(
        Guid dealershipId, Guid serviceTypeId, TimeSlot slot, CancellationToken ct);
    Task<bool> IsStillFreeAsync(
        Guid serviceBayId, Guid technicianId, TimeSlot slot, CancellationToken ct);
}
