using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AppointmentScheduler.Infrastructure.Persistence;

/// <summary>
/// Compares stored instants by the moment they denote, not by their offset.
/// </summary>
public sealed class UtcDateTimeOffsetComparer : ValueComparer<DateTimeOffset>
{
    public UtcDateTimeOffsetComparer()
        : base(
            (left, right) => left.UtcDateTime == right.UtcDateTime,
            value => value.UtcDateTime.GetHashCode(),
            value => value.ToUniversalTime())
    {
    }
}
