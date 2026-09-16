using System.Linq.Expressions;
using AppointmentScheduler.Application.Features.Availability;
using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IAvailabilityRepository" />
internal sealed class AvailabilityRepository : IAvailabilityRepository
{
    private readonly AppointmentDbContext _db;

    public AvailabilityRepository(AppointmentDbContext db) => _db = db;

    public Task<Dealership?> FindDealershipAsync(Guid dealershipId, CancellationToken ct) =>
        _db.Dealerships.SingleOrDefaultAsync(d => d.Id == dealershipId, ct);

    public Task<ServiceType?> FindServiceTypeAsync(Guid serviceTypeId, CancellationToken ct) =>
        _db.ServiceTypes
            .Include(s => s.RequiredSkills)
            .SingleOrDefaultAsync(s => s.Id == serviceTypeId, ct);

    public async Task<IReadOnlyList<ResourceOccupancy>> GetBayOccupancyAsync(
        Guid dealershipId, TimeSlot window, CancellationToken ct)
    {
        var bays = await _db.ServiceBays
            .Where(b => b.DealershipId == dealershipId && b.IsActive)
            .OrderBy(b => b.Code)
            .ThenBy(b => b.Id)
            .Select(b => new
            {
                b.Id,
                Busy = _db.Appointments
                    .Occupying()
                    .WithinWindow(window)
                    .Where(a => a.ServiceBayId == b.Id)
                    .Select(a => new { a.StartsAtUtc, a.EndsAtUtc })
                    .ToList(),
            })
            .ToListAsync(ct);

        return bays
            .Select(b => new ResourceOccupancy(
                b.Id,
                SkillCount: 0,
                Busy: b.Busy.Select(a => TimeSlot.Create(a.StartsAtUtc, a.EndsAtUtc)).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<ResourceOccupancy>> GetQualifiedTechnicianOccupancyAsync(
        Guid dealershipId, Guid serviceTypeId, TimeSlot window, CancellationToken ct)
    {
        var isQualified = IsQualifiedFor(serviceTypeId);

        var technicians = await _db.Technicians
            .Where(t => t.DealershipId == dealershipId && t.IsActive)
            .Where(isQualified)
            .OrderBy(t => t.Skills.Count)
            .ThenBy(t => t.Id)
            .Select(t => new
            {
                t.Id,
                SkillCount = t.Skills.Count,
                Busy = _db.Appointments
                    .Occupying()
                    .WithinWindow(window)
                    .Where(a => a.TechnicianId == t.Id)
                    .Select(a => new { a.StartsAtUtc, a.EndsAtUtc })
                    .ToList(),
            })
            .ToListAsync(ct);

        return technicians
            .Select(t => new ResourceOccupancy(
                t.Id,
                t.SkillCount,
                Busy: t.Busy.Select(a => TimeSlot.Create(a.StartsAtUtc, a.EndsAtUtc)).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<ServiceBay>> FindFreeBaysAsync(
        Guid dealershipId, TimeSlot slot, CancellationToken ct)
    {
        var busyBayIds = _db.Appointments
            .Occupying()
            .OverlappingWith(slot)
            .Select(a => a.ServiceBayId);

        return await _db.ServiceBays
            .Where(b => b.DealershipId == dealershipId && b.IsActive && !busyBayIds.Contains(b.Id))
            .OrderBy(b => b.Code)
            .ThenBy(b => b.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Technician>> FindFreeQualifiedTechniciansAsync(
        Guid dealershipId, Guid serviceTypeId, TimeSlot slot, CancellationToken ct)
    {
        var isQualified = IsQualifiedFor(serviceTypeId);

        var busyTechnicianIds = _db.Appointments
            .Occupying()
            .OverlappingWith(slot)
            .Select(a => a.TechnicianId);

        return await _db.Technicians
            .Where(t => t.DealershipId == dealershipId && t.IsActive && !busyTechnicianIds.Contains(t.Id))
            .Where(isQualified)
            .OrderBy(t => t.Skills.Count)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);
    }

    public async Task<bool> IsStillFreeAsync(
        Guid serviceBayId, Guid technicianId, TimeSlot slot, CancellationToken ct) =>
        !await _db.Appointments.Occupying().OverlappingWith(slot)
            .AnyAsync(a => a.ServiceBayId == serviceBayId || a.TechnicianId == technicianId, ct);

    /// <summary>
    /// True when the technician holds every skill <paramref name="serviceTypeId"/> requires.
    /// Relational division via double negation: there is no required skill the technician is
    /// missing. Translates to the double <c>NOT EXISTS</c> verified up front (stage3.md) — no
    /// client evaluation.
    /// </summary>
    private Expression<Func<Technician, bool>> IsQualifiedFor(Guid serviceTypeId)
    {
        var requiredSkillIds = _db.ServiceTypes
            .Where(s => s.Id == serviceTypeId)
            .SelectMany(s => s.RequiredSkills)
            .Select(sk => sk.Id);

        return technician => !requiredSkillIds.Any(
            requiredId => !technician.Skills.Any(held => held.Id == requiredId));
    }
}
