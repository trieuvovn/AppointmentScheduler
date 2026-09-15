using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Application.Features.Availability;

public sealed record ResourceOccupancy(
    Guid ResourceId,
    int SkillCount,
    IReadOnlyList<TimeSlot> Busy)
{
    public bool IsFreeDuring(TimeSlot slot) => !Busy.Any(busy => busy.Overlaps(slot));
}
